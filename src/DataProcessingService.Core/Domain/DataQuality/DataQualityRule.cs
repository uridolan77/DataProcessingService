using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DataProcessingService.Core.Domain.Entities.Base;

namespace DataProcessingService.Core.Domain.DataQuality;

public class DataQualityRule : AuditableEntity
{
    public string Name { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public RuleType Type { get; private set; }
    public string TargetField { get; private set; } = null!;
    public string? RelatedField { get; private set; }
    public Dictionary<string, object> Parameters { get; private set; } = new();
    public RuleSeverity Severity { get; private set; }
    public bool IsActive { get; private set; }
    
    // For EF Core
    private DataQualityRule() { }
    
    public DataQualityRule(
        string name,
        string description,
        RuleType type,
        string targetField,
        string? relatedField,
        Dictionary<string, object> parameters,
        RuleSeverity severity)
    {
        Name = name;
        Description = description;
        Type = type;
        TargetField = targetField;
        RelatedField = relatedField;
        Parameters = parameters;
        Severity = severity;
        IsActive = true;
    }
    
    public void Update(
        string description,
        Dictionary<string, object> parameters,
        RuleSeverity severity)
    {
        Description = description;
        Parameters = parameters;
        Severity = severity;
    }
    
    public void Activate() => IsActive = true;
    
    public void Deactivate() => IsActive = false;
    
    public RuleValidationResult Validate(IDictionary<string, object?> data)
    {
        if (!data.TryGetValue(TargetField, out var fieldValue))
        {
            return new RuleValidationResult(
                this,
                false,
                $"Field '{TargetField}' not found in data");
        }
        
        bool isValid = Type switch
        {
            RuleType.NotNull => ValidateNotNull(fieldValue),
            RuleType.Regex => ValidateRegex(fieldValue),
            RuleType.Range => ValidateRange(fieldValue),
            RuleType.Unique => true, // Unique validation requires context of all records
            RuleType.Comparison => ValidateComparison(fieldValue, data),
            RuleType.Custom => ValidateCustom(fieldValue, data),
            _ => false
        };
        
        string message = isValid 
            ? $"Validation passed for rule '{Name}'"
            : $"Validation failed for rule '{Name}': {GetFailureMessage(fieldValue, data)}";
        
        return new RuleValidationResult(this, isValid, message);
    }
    
    private bool ValidateNotNull(object? value)
    {
        return value != null;
    }
    
    private bool ValidateRegex(object? value)
    {
        if (value == null || !Parameters.TryGetValue("pattern", out var patternObj))
            return false;
        
        string pattern = patternObj.ToString() ?? string.Empty;
        string stringValue = value.ToString() ?? string.Empty;
        
        return System.Text.RegularExpressions.Regex.IsMatch(stringValue, pattern);
    }
    
    private bool ValidateRange(object? value)
    {
        if (value == null)
            return false;
        
        if (value is int intValue)
        {
            int min = Parameters.TryGetValue("min", out var minObj) ? Convert.ToInt32(minObj) : int.MinValue;
            int max = Parameters.TryGetValue("max", out var maxObj) ? Convert.ToInt32(maxObj) : int.MaxValue;
            
            return intValue >= min && intValue <= max;
        }
        else if (value is decimal decimalValue)
        {
            decimal min = Parameters.TryGetValue("min", out var minObj) ? Convert.ToDecimal(minObj) : decimal.MinValue;
            decimal max = Parameters.TryGetValue("max", out var maxObj) ? Convert.ToDecimal(maxObj) : decimal.MaxValue;
            
            return decimalValue >= min && decimalValue <= max;
        }
        else if (value is DateTime dateTimeValue)
        {
            DateTime min = Parameters.TryGetValue("min", out var minObj) ? 
                DateTime.Parse(minObj.ToString() ?? string.Empty) : DateTime.MinValue;
            DateTime max = Parameters.TryGetValue("max", out var maxObj) ? 
                DateTime.Parse(maxObj.ToString() ?? string.Empty) : DateTime.MaxValue;
            
            return dateTimeValue >= min && dateTimeValue <= max;
        }
        
        return false;
    }
    
    private bool ValidateComparison(object? value, IDictionary<string, object?> data)
    {
        if (value == null || string.IsNullOrEmpty(RelatedField) || !data.TryGetValue(RelatedField, out var relatedValue))
            return false;
        
        string op = Parameters.TryGetValue("operator", out var opObj) ? opObj.ToString() ?? "eq" : "eq";
        
        return op switch
        {
            "eq" => Equals(value, relatedValue),
            "ne" => !Equals(value, relatedValue),
            "gt" => CompareValues(value, relatedValue) > 0,
            "ge" => CompareValues(value, relatedValue) >= 0,
            "lt" => CompareValues(value, relatedValue) < 0,
            "le" => CompareValues(value, relatedValue) <= 0,
            _ => false
        };
    }
    
    private bool ValidateCustom(object? value, IDictionary<string, object?> data)
    {
        // In a real implementation, this would execute a custom validation script or expression
        // For now, we'll just return true
        return true;
    }
    
    private int CompareValues(object? value1, object? value2)
    {
        if (value1 == null && value2 == null)
            return 0;
        
        if (value1 == null)
            return -1;
        
        if (value2 == null)
            return 1;
        
        if (value1 is IComparable comparable1 && value2.GetType() == value1.GetType())
            return comparable1.CompareTo(value2);
        
        return string.Compare(value1.ToString(), value2?.ToString());
    }
    
    private string GetFailureMessage(object? fieldValue, IDictionary<string, object?> data)
    {
        return Type switch
        {
            RuleType.NotNull => $"Field '{TargetField}' must not be null",
            RuleType.Regex => $"Field '{TargetField}' does not match the required pattern",
            RuleType.Range => $"Field '{TargetField}' is outside the allowed range",
            RuleType.Unique => $"Field '{TargetField}' must be unique",
            RuleType.Comparison => $"Field '{TargetField}' fails comparison with '{RelatedField}'",
            RuleType.Custom => $"Field '{TargetField}' fails custom validation",
            _ => $"Unknown validation type for field '{TargetField}'"
        };
    }
}

public enum RuleType
{
    NotNull,
    Regex,
    Range,
    Unique,
    Comparison,
    Custom
}

public enum RuleSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

public class RuleValidationResult
{
    public DataQualityRule Rule { get; }
    public bool IsValid { get; }
    public string Message { get; }
    
    public RuleValidationResult(DataQualityRule rule, bool isValid, string message)
    {
        Rule = rule;
        IsValid = isValid;
        Message = message;
    }
}
