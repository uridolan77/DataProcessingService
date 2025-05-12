using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.Core.Domain.DataQuality;

namespace DataProcessingService.Core.Interfaces.Services.DataQuality;

public interface IDataQualityRuleService
{
    Task<DataQualityRule?> GetRuleByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    Task<DataQualityRule?> GetRuleByNameAsync(string name, CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<DataQualityRule>> GetAllRulesAsync(CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<DataQualityRule>> GetRulesByTargetFieldAsync(
        string targetField, 
        CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<DataQualityRule>> GetRulesBySeverityAsync(
        RuleSeverity severity, 
        CancellationToken cancellationToken = default);
    
    Task<DataQualityRule> CreateRuleAsync(
        string name,
        string description,
        RuleType type,
        string targetField,
        string? relatedField,
        Dictionary<string, object> parameters,
        RuleSeverity severity,
        CancellationToken cancellationToken = default);
    
    Task UpdateRuleAsync(
        Guid id,
        string description,
        Dictionary<string, object> parameters,
        RuleSeverity severity,
        CancellationToken cancellationToken = default);
    
    Task DeleteRuleAsync(Guid id, CancellationToken cancellationToken = default);
    
    Task<List<RuleValidationResult>> ValidateDataAsync(
        Guid ruleId,
        IDictionary<string, object?> data,
        CancellationToken cancellationToken = default);
    
    Task<List<RuleValidationResult>> ValidateDataAsync(
        Guid ruleId,
        ExpandoObject data,
        CancellationToken cancellationToken = default);
    
    Task<List<RuleValidationResult>> ValidateDataAsync(
        DataQualityRule rule,
        IDictionary<string, object?> data,
        CancellationToken cancellationToken = default);
    
    Task<List<RuleValidationResult>> ValidateDataWithRulesAsync(
        IEnumerable<DataQualityRule> rules,
        IDictionary<string, object?> data,
        CancellationToken cancellationToken = default);
    
    Task<List<RuleValidationResult>> ValidateDataBatchAsync(
        Guid ruleId,
        IEnumerable<IDictionary<string, object?>> dataBatch,
        CancellationToken cancellationToken = default);
    
    Task<List<RuleValidationResult>> ValidateDataBatchWithRulesAsync(
        IEnumerable<DataQualityRule> rules,
        IEnumerable<IDictionary<string, object?>> dataBatch,
        CancellationToken cancellationToken = default);
}
