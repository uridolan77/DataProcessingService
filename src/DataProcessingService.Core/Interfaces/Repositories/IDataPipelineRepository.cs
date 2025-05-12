using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.Core.Domain.Entities;
using DataProcessingService.Core.Domain.Enums;

namespace DataProcessingService.Core.Interfaces.Repositories;

public interface IDataPipelineRepository : IRepository<DataPipeline>
{
    Task<IReadOnlyList<DataPipeline>> GetPipelinesByStatusAsync(
        PipelineStatus status, 
        CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<DataPipeline>> GetPipelinesByTypeAsync(
        PipelineType type, 
        CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<DataPipeline>> GetPipelinesDueForExecutionAsync(
        DateTimeOffset currentTime, 
        CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<DataPipeline>> GetPipelinesBySourceIdAsync(
        Guid sourceId, 
        CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<DataPipeline>> GetPipelinesByDestinationIdAsync(
        Guid destinationId, 
        CancellationToken cancellationToken = default);
    
    Task<DataPipeline?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    
    Task<DataPipeline?> GetWithRelatedDataAsync(Guid id, CancellationToken cancellationToken = default);
}
