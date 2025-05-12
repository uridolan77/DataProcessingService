using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.Core.Domain.DataQuality;

namespace DataProcessingService.Core.Interfaces.Services.DataQuality;

public interface IDataContractService
{
    Task<DataContract?> GetContractByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    Task<DataContract?> GetContractByNameAsync(
        string name, 
        string version, 
        CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<DataContract>> GetAllContractsAsync(CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<DataContract>> GetContractsByProducerIdAsync(
        Guid producerId, 
        CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<DataContract>> GetContractsByConsumerIdAsync(
        Guid consumerId, 
        CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<DataContract>> GetContractsByStatusAsync(
        ContractStatus status, 
        CancellationToken cancellationToken = default);
    
    Task<DataContract> CreateContractAsync(
        string name,
        string description,
        string version,
        Guid producerId,
        List<Guid> consumerIds,
        Guid schemaId,
        List<Guid> qualityRuleIds,
        DateTimeOffset? validFrom = null,
        DateTimeOffset? validTo = null,
        CancellationToken cancellationToken = default);
    
    Task UpdateContractAsync(
        Guid id,
        string description,
        List<Guid> consumerIds,
        List<Guid> qualityRuleIds,
        DateTimeOffset? validFrom,
        DateTimeOffset? validTo,
        CancellationToken cancellationToken = default);
    
    Task DeleteContractAsync(Guid id, CancellationToken cancellationToken = default);
    
    Task<bool> PublishContractAsync(Guid id, CancellationToken cancellationToken = default);
    
    Task<bool> DeprecateContractAsync(Guid id, CancellationToken cancellationToken = default);
    
    Task<bool> RetireContractAsync(Guid id, CancellationToken cancellationToken = default);
    
    Task<DataContractViolation> CheckComplianceAsync(
        Guid contractId,
        IDictionary<string, object?> data,
        CancellationToken cancellationToken = default);
    
    Task<DataContractViolation> CheckComplianceAsync(
        Guid contractId,
        ExpandoObject data,
        CancellationToken cancellationToken = default);
    
    Task<List<DataContractViolation>> CheckComplianceBatchAsync(
        Guid contractId,
        IEnumerable<IDictionary<string, object?>> dataBatch,
        CancellationToken cancellationToken = default);
    
    Task<Dictionary<string, object>> GenerateContractDocumentationAsync(
        Guid contractId,
        CancellationToken cancellationToken = default);
}
