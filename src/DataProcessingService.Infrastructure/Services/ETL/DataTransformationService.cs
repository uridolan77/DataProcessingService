using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using DataProcessingService.Core.Domain.Enums;
using DataProcessingService.Core.Domain.ValueObjects;
using DataProcessingService.Core.Interfaces.Services.ETL;

namespace DataProcessingService.Infrastructure.Services.ETL;

public class DataTransformationService : IDataTransformationService
{
    private readonly ILogger<DataTransformationService> _logger;
    
    public DataTransformationService(ILogger<DataTransformationService> logger)
    {
        _logger = logger;
    }
    
    public async Task<IEnumerable<ExpandoObject>> TransformDataAsync(
        IEnumerable<ExpandoObject> data,
        TransformationRules rules,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Transforming data using {RuleCount} rules", rules.Rules.Count);
        
        var result = new List<ExpandoObject>();
        
        foreach (var record in data)
        {
            var transformedRecord = await TransformRecordAsync(record, rules, cancellationToken);
            result.Add(transformedRecord);
        }
        
        return result;
    }
    
    public async Task<IAsyncEnumerable<ExpandoObject>> TransformDataStreamAsync(
        IAsyncEnumerable<ExpandoObject> dataStream,
        TransformationRules rules,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Streaming transformation using {RuleCount} rules", rules.Rules.Count);
        
        return new TransformingAsyncEnumerable(dataStream, this, rules, cancellationToken);
    }
    
    public async Task<ExpandoObject> TransformRecordAsync(
        ExpandoObject record,
        TransformationRules rules,
        CancellationToken cancellationToken = default)
    {
        var result = new ExpandoObject();
        var resultDict = (IDictionary<string, object>)result;
        var recordDict = (IDictionary<string, object>)record;
        
        // First, copy all fields from the source record
        foreach (var kvp in recordDict)
        {
            resultDict[kvp.Key] = kvp.Value;
        }
        
        // Apply each transformation rule
        foreach (var rule in rules.Rules.OrderBy(r => r.Order))
        {
            await ApplyTransformationRuleAsync(rule, recordDict, resultDict, cancellationToken);
        }
        
        return result;
    }
    
    public IReadOnlyList<string> ValidateRules(TransformationRules rules)
    {
        var errors = new List<string>();
        
        foreach (var rule in rules.Rules)
        {
            if (string.IsNullOrEmpty(rule.SourceField))
            {
                errors.Add($"Rule {rule.Id}: Source field is required");
            }
            
            if (rule.Type != TransformationType.Custom && string.IsNullOrEmpty(rule.TargetField))
            {
                errors.Add($"Rule {rule.Id}: Target field is required for {rule.Type} transformation");
            }
            
            if (rule.Type == TransformationType.Format && string.IsNullOrEmpty(rule.FormatString))
            {
                errors.Add($"Rule {rule.Id}: Format string is required for Format transformation");
            }
        }
        
        return errors;
    }
    
    private async Task ApplyTransformationRuleAsync(
        TransformationRule rule,
        IDictionary<string, object> sourceDict,
        IDictionary<string, object> targetDict,
        CancellationToken cancellationToken)
    {
        // In a real implementation, this would apply the transformation rule based on its type
        // For now, we'll just implement a few basic transformations
        
        switch (rule.Type)
        {
            case TransformationType.Copy:
                if (sourceDict.TryGetValue(rule.SourceField, out var value))
                {
                    targetDict[rule.TargetField!] = value;
                }
                break;
                
            case TransformationType.Format:
                if (sourceDict.TryGetValue(rule.SourceField, out var formatValue) && 
                    formatValue != null && 
                    !string.IsNullOrEmpty(rule.FormatString))
                {
                    targetDict[rule.TargetField!] = string.Format(rule.FormatString, formatValue);
                }
                break;
                
            case TransformationType.Concatenate:
                var sourceFields = rule.SourceField.Split(',');
                var values = new List<string>();
                
                foreach (var field in sourceFields)
                {
                    if (sourceDict.TryGetValue(field.Trim(), out var fieldValue) && fieldValue != null)
                    {
                        values.Add(fieldValue.ToString() ?? string.Empty);
                    }
                }
                
                var separator = rule.Parameters.TryGetValue("separator", out var sep) ? sep : " ";
                targetDict[rule.TargetField!] = string.Join(separator, values);
                break;
                
            // Add more transformation types as needed
                
            default:
                _logger.LogWarning("Transformation type {TransformationType} not implemented", rule.Type);
                break;
        }
        
        await Task.CompletedTask; // For async consistency
    }
    
    private class TransformingAsyncEnumerable : IAsyncEnumerable<ExpandoObject>
    {
        private readonly IAsyncEnumerable<ExpandoObject> _source;
        private readonly DataTransformationService _service;
        private readonly TransformationRules _rules;
        private readonly CancellationToken _cancellationToken;
        
        public TransformingAsyncEnumerable(
            IAsyncEnumerable<ExpandoObject> source,
            DataTransformationService service,
            TransformationRules rules,
            CancellationToken cancellationToken)
        {
            _source = source;
            _service = service;
            _rules = rules;
            _cancellationToken = cancellationToken;
        }
        
        public IAsyncEnumerator<ExpandoObject> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new TransformingAsyncEnumerator(
                _source.GetAsyncEnumerator(cancellationToken),
                _service,
                _rules,
                _cancellationToken);
        }
        
        private class TransformingAsyncEnumerator : IAsyncEnumerator<ExpandoObject>
        {
            private readonly IAsyncEnumerator<ExpandoObject> _sourceEnumerator;
            private readonly DataTransformationService _service;
            private readonly TransformationRules _rules;
            private readonly CancellationToken _cancellationToken;
            
            public ExpandoObject Current { get; private set; } = new ExpandoObject();
            
            public TransformingAsyncEnumerator(
                IAsyncEnumerator<ExpandoObject> sourceEnumerator,
                DataTransformationService service,
                TransformationRules rules,
                CancellationToken cancellationToken)
            {
                _sourceEnumerator = sourceEnumerator;
                _service = service;
                _rules = rules;
                _cancellationToken = cancellationToken;
            }
            
            public async ValueTask<bool> MoveNextAsync()
            {
                if (await _sourceEnumerator.MoveNextAsync())
                {
                    Current = await _service.TransformRecordAsync(
                        _sourceEnumerator.Current, 
                        _rules, 
                        _cancellationToken);
                    
                    return true;
                }
                
                return false;
            }
            
            public ValueTask DisposeAsync()
            {
                return _sourceEnumerator.DisposeAsync();
            }
        }
    }
}
