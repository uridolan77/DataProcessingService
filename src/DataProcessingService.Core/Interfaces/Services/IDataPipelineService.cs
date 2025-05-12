using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.Core.Domain.Entities;
using DataProcessingService.Core.Domain.Enums;
using DataProcessingService.Core.Domain.ValueObjects;

namespace DataProcessingService.Core.Interfaces.Services;

public interface IDataPipelineService
{
    Task<DataPipeline?> GetPipelineByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    Task<DataPipeline?> GetPipelineByNameAsync(string name, CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<DataPipeline>> GetAllPipelinesAsync(CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<DataPipeline>> GetPipelinesByStatusAsync(
        PipelineStatus status, 
        CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<DataPipeline>> GetPipelinesByTypeAsync(
        PipelineType type, 
        CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<DataPipeline>> GetPipelinesBySourceIdAsync(
        Guid sourceId, 
        CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<DataPipeline>> GetPipelinesDueForExecutionAsync(
        DateTimeOffset currentTime, 
        CancellationToken cancellationToken = default);
    
    Task<DataPipeline> CreatePipelineAsync(
        string name,
        string description,
        PipelineType type,
        Guid sourceId,
        ExecutionSchedule schedule,
        TransformationRules transformationRules,
        Guid? destinationId = null,
        CancellationToken cancellationToken = default);
    
    Task UpdatePipelineAsync(
        Guid id,
        string? description = null,
        ExecutionSchedule? schedule = null,
        TransformationRules? transformationRules = null,
        CancellationToken cancellationToken = default);
    
    Task DeletePipelineAsync(Guid id, CancellationToken cancellationToken = default);
    
    Task<PipelineExecution> ExecutePipelineAsync(Guid id, CancellationToken cancellationToken = default);
    
    Task PausePipelineAsync(Guid id, CancellationToken cancellationToken = default);
    
    Task ResumePipelineAsync(Guid id, CancellationToken cancellationToken = default);
}
