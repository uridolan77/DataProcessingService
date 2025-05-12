using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DataProcessingService.Core.Domain.Enums;

namespace DataProcessingService.Core.Domain.ValueObjects;

public class TransformationRules
{
    public List<TransformationRule> Rules { get; private set; } = new();
    
    private TransformationRules() { }
    
    public TransformationRules(IEnumerable<TransformationRule> rules)
    {
        Rules = rules.ToList();
    }
    
    public static TransformationRules Empty => new(Enumerable.Empty<TransformationRule>());
    
    public static TransformationRules FromJson(string json)
    {
        if (string.IsNullOrEmpty(json))
            return Empty;
            
        var rules = JsonSerializer.Deserialize<List<TransformationRule>>(json) ?? new List<TransformationRule>();
        return new TransformationRules(rules);
    }
    
    public string ToJson()
    {
        return JsonSerializer.Serialize(Rules);
    }
    
    public void AddRule(TransformationRule rule)
    {
        Rules.Add(rule);
    }
    
    public void RemoveRule(Guid ruleId)
    {
        Rules.RemoveAll(r => r.Id == ruleId);
    }
    
    public TransformationRule? FindRule(Guid ruleId)
    {
        return Rules.FirstOrDefault(r => r.Id == ruleId);
    }
}

public class TransformationRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string SourceField { get; set; } = null!;
    public string? TargetField { get; set; }
    public TransformationType Type { get; set; }
    public string? FormatString { get; set; }
    public Dictionary<string, string> Parameters { get; set; } = new();
    public int Order { get; set; }
    
    // For JSON deserialization
    public TransformationRule() { }
    
    public static TransformationRule CreateCopy(string sourceField, string targetField, int order = 0)
    {
        return new TransformationRule
        {
            SourceField = sourceField,
            TargetField = targetField,
            Type = TransformationType.Copy,
            Order = order
        };
    }
    
    public static TransformationRule CreateFormat(string sourceField, string targetField, string formatString, int order = 0)
    {
        return new TransformationRule
        {
            SourceField = sourceField,
            TargetField = targetField,
            Type = TransformationType.Format,
            FormatString = formatString,
            Order = order
        };
    }
    
    public static TransformationRule CreateConcatenate(
        IEnumerable<string> sourceFields, 
        string targetField, 
        string separator = " ", 
        int order = 0)
    {
        return new TransformationRule
        {
            SourceField = string.Join(",", sourceFields),
            TargetField = targetField,
            Type = TransformationType.Concatenate,
            Parameters = new Dictionary<string, string> { ["separator"] = separator },
            Order = order
        };
    }
    
    public static TransformationRule CreateCustom(
        string sourceField, 
        string targetField, 
        Dictionary<string, string> parameters, 
        int order = 0)
    {
        return new TransformationRule
        {
            SourceField = sourceField,
            TargetField = targetField,
            Type = TransformationType.Custom,
            Parameters = parameters,
            Order = order
        };
    }
}
