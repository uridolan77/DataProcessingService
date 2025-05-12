using System;
using System.Collections.Generic;
using System.Linq;
using DataProcessingService.Core.Domain.Entities.Base;

namespace DataProcessingService.Core.Domain.DataQuality;

public class DataSchema : AuditableEntity
{
    public string Name { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public string Version { get; private set; } = null!;
    public List<SchemaField> Fields { get; private set; } = new();
    public bool IsActive { get; private set; }
    
    // For EF Core
    private DataSchema() { }
    
    public DataSchema(
        string name,
        string description,
        string version,
        List<SchemaField> fields)
    {
        Name = name;
        Description = description;
        Version = version;
        Fields = fields;
        IsActive = true;
    }
    
    public void Update(string description, string version, List<SchemaField> fields)
    {
        Description = description;
        Version = version;
        Fields = fields;
    }
    
    public void Activate() => IsActive = true;
    
    public void Deactivate() => IsActive = false;
    
    public List<SchemaValidationError> Validate(IDictionary<string, object?> data)
    {
        var errors = new List<SchemaValidationError>();
        
        // Check for required fields
        foreach (var field in Fields.Where(f => f.IsRequired))
        {
            if (!data.ContainsKey(field.Name) || data[field.Name] == null)
            {
                errors.Add(new SchemaValidationError(
                    field.Name,
                    $"Required field '{field.Name}' is missing or null"));
            }
        }
        
        // Validate field types and constraints
        foreach (var (key, value) in data)
        {
            var field = Fields.FirstOrDefault(f => f.Name == key);
            if (field == null)
            {
                if (!field?.AllowUnknownFields ?? true)
                {
                    errors.Add(new SchemaValidationError(
                        key,
                        $"Field '{key}' is not defined in the schema"));
                }
                continue;
            }
            
            if (value != null)
            {
                var typeErrors = field.ValidateType(value);
                errors.AddRange(typeErrors);
                
                var constraintErrors = field.ValidateConstraints(value);
                errors.AddRange(constraintErrors);
            }
        }
        
        return errors;
    }
}

public class SchemaField
{
    public string Name { get; set; } = null!;
    public string Description { get; set; } = string.Empty;
    public SchemaFieldType Type { get; set; }
    public bool IsRequired { get; set; }
    public bool AllowUnknownFields { get; set; }
    public Dictionary<string, object> Constraints { get; set; } = new();
    
    public List<SchemaValidationError> ValidateType(object value)
    {
        var errors = new List<SchemaValidationError>();
        
        bool isValid = Type switch
        {
            SchemaFieldType.String => value is string,
            SchemaFieldType.Integer => value is int or long,
            SchemaFieldType.Decimal => value is decimal or double or float,
            SchemaFieldType.Boolean => value is bool,
            SchemaFieldType.DateTime => value is DateTime or DateTimeOffset,
            SchemaFieldType.Array => value is System.Collections.IEnumerable,
            SchemaFieldType.Object => value is IDictionary<string, object>,
            _ => false
        };
        
        if (!isValid)
        {
            errors.Add(new SchemaValidationError(
                Name,
                $"Field '{Name}' has invalid type. Expected {Type}, got {value.GetType().Name}"));
        }
        
        return errors;
    }
    
    public List<SchemaValidationError> ValidateConstraints(object value)
    {
        var errors = new List<SchemaValidationError>();
        
        switch (Type)
        {
            case SchemaFieldType.String when value is string stringValue:
                ValidateStringConstraints(stringValue, errors);
                break;
            case SchemaFieldType.Integer when value is int or long:
                ValidateNumericConstraints(Convert.ToInt64(value), errors);
                break;
            case SchemaFieldType.Decimal when value is decimal or double or float:
                ValidateNumericConstraints(Convert.ToDecimal(value), errors);
                break;
            case SchemaFieldType.Array when value is System.Collections.IEnumerable enumerable:
                ValidateArrayConstraints(enumerable, errors);
                break;
        }
        
        return errors;
    }
    
    private void ValidateStringConstraints(string value, List<SchemaValidationError> errors)
    {
        if (Constraints.TryGetValue("minLength", out var minLengthObj) && 
            minLengthObj is int minLength && 
            value.Length < minLength)
        {
            errors.Add(new SchemaValidationError(
                Name,
                $"Field '{Name}' length is less than minimum length of {minLength}"));
        }
        
        if (Constraints.TryGetValue("maxLength", out var maxLengthObj) && 
            maxLengthObj is int maxLength && 
            value.Length > maxLength)
        {
            errors.Add(new SchemaValidationError(
                Name,
                $"Field '{Name}' length exceeds maximum length of {maxLength}"));
        }
        
        if (Constraints.TryGetValue("pattern", out var patternObj) && 
            patternObj is string pattern)
        {
            var regex = new System.Text.RegularExpressions.Regex(pattern);
            if (!regex.IsMatch(value))
            {
                errors.Add(new SchemaValidationError(
                    Name,
                    $"Field '{Name}' does not match pattern '{pattern}'"));
            }
        }
        
        if (Constraints.TryGetValue("enum", out var enumObj) && 
            enumObj is System.Collections.IEnumerable enumValues)
        {
            bool found = false;
            foreach (var enumValue in enumValues)
            {
                if (value == enumValue.ToString())
                {
                    found = true;
                    break;
                }
            }
            
            if (!found)
            {
                errors.Add(new SchemaValidationError(
                    Name,
                    $"Field '{Name}' value is not in the allowed values list"));
            }
        }
    }
    
    private void ValidateNumericConstraints<T>(T value, List<SchemaValidationError> errors) 
        where T : IComparable
    {
        if (Constraints.TryGetValue("minimum", out var minObj))
        {
            var min = Convert.ChangeType(minObj, typeof(T));
            if (value.CompareTo(min) < 0)
            {
                errors.Add(new SchemaValidationError(
                    Name,
                    $"Field '{Name}' value is less than minimum of {min}"));
            }
        }
        
        if (Constraints.TryGetValue("maximum", out var maxObj))
        {
            var max = Convert.ChangeType(maxObj, typeof(T));
            if (value.CompareTo(max) > 0)
            {
                errors.Add(new SchemaValidationError(
                    Name,
                    $"Field '{Name}' value exceeds maximum of {max}"));
            }
        }
    }
    
    private void ValidateArrayConstraints(System.Collections.IEnumerable value, List<SchemaValidationError> errors)
    {
        int count = 0;
        foreach (var _ in value)
        {
            count++;
        }
        
        if (Constraints.TryGetValue("minItems", out var minItemsObj) && 
            minItemsObj is int minItems && 
            count < minItems)
        {
            errors.Add(new SchemaValidationError(
                Name,
                $"Field '{Name}' has fewer items than minimum of {minItems}"));
        }
        
        if (Constraints.TryGetValue("maxItems", out var maxItemsObj) && 
            maxItemsObj is int maxItems && 
            count > maxItems)
        {
            errors.Add(new SchemaValidationError(
                Name,
                $"Field '{Name}' has more items than maximum of {maxItems}"));
            
        }
    }
}

public enum SchemaFieldType
{
    String,
    Integer,
    Decimal,
    Boolean,
    DateTime,
    Array,
    Object
}

public class SchemaValidationError
{
    public string FieldName { get; }
    public string Message { get; }
    
    public SchemaValidationError(string fieldName, string message)
    {
        FieldName = fieldName;
        Message = message;
    }
}
