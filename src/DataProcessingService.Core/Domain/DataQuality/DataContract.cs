using System;
using System.Collections.Generic;
using DataProcessingService.Core.Domain.Entities.Base;

namespace DataProcessingService.Core.Domain.DataQuality;

public class DataContract : AuditableEntity
{
    public string Name { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public string Version { get; private set; } = null!;
    public Guid ProducerId { get; private set; }
    public List<Guid> ConsumerIds { get; private set; } = new();
    public Guid SchemaId { get; private set; }
    public List<Guid> QualityRuleIds { get; private set; } = new();
    public ContractStatus Status { get; private set; }
    public DateTimeOffset? ValidFrom { get; private set; }
    public DateTimeOffset? ValidTo { get; private set; }
    
    // For EF Core
    private DataContract() { }
    
    public DataContract(
        string name,
        string description,
        string version,
        Guid producerId,
        List<Guid> consumerIds,
        Guid schemaId,
        List<Guid> qualityRuleIds,
        DateTimeOffset? validFrom = null,
        DateTimeOffset? validTo = null)
    {
        Name = name;
        Description = description;
        Version = version;
        ProducerId = producerId;
        ConsumerIds = consumerIds;
        SchemaId = schemaId;
        QualityRuleIds = qualityRuleIds;
        Status = ContractStatus.Draft;
        ValidFrom = validFrom;
        ValidTo = validTo;
    }
    
    public void Update(
        string description,
        List<Guid> consumerIds,
        List<Guid> qualityRuleIds,
        DateTimeOffset? validFrom,
        DateTimeOffset? validTo)
    {
        Description = description;
        ConsumerIds = consumerIds;
        QualityRuleIds = qualityRuleIds;
        ValidFrom = validFrom;
        ValidTo = validTo;
    }
    
    public void Publish()
    {
        if (Status == ContractStatus.Draft)
        {
            Status = ContractStatus.Published;
        }
    }
    
    public void Deprecate()
    {
        if (Status == ContractStatus.Published)
        {
            Status = ContractStatus.Deprecated;
        }
    }
    
    public void Retire()
    {
        if (Status == ContractStatus.Deprecated)
        {
            Status = ContractStatus.Retired;
        }
    }
    
    public DataContractViolation CheckCompliance(IDictionary<string, object?> data, DataSchema schema, List<DataQualityRule> rules)
    {
        var schemaErrors = schema.Validate(data);
        var ruleResults = new List<RuleValidationResult>();
        
        foreach (var rule in rules)
        {
            var result = rule.Validate(data);
            if (!result.IsValid)
            {
                ruleResults.Add(result);
            }
        }
        
        bool isCompliant = schemaErrors.Count == 0 && 
                          !ruleResults.Exists(r => r.Rule.Severity == RuleSeverity.Error || 
                                                  r.Rule.Severity == RuleSeverity.Critical);
        
        return new DataContractViolation(
            this.Id,
            isCompliant,
            schemaErrors,
            ruleResults);
    }
}

public enum ContractStatus
{
    Draft,
    Published,
    Deprecated,
    Retired
}

public class DataContractViolation
{
    public Guid ContractId { get; }
    public bool IsCompliant { get; }
    public List<SchemaValidationError> SchemaErrors { get; }
    public List<RuleValidationResult> RuleViolations { get; }
    public DateTimeOffset Timestamp { get; }
    
    public DataContractViolation(
        Guid contractId,
        bool isCompliant,
        List<SchemaValidationError> schemaErrors,
        List<RuleValidationResult> ruleViolations)
    {
        ContractId = contractId;
        IsCompliant = isCompliant;
        SchemaErrors = schemaErrors;
        RuleViolations = ruleViolations;
        Timestamp = DateTimeOffset.UtcNow;
    }
}
