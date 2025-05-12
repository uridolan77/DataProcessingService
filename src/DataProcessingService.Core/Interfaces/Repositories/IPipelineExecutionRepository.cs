using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.Core.Domain.Entities;
using DataProcessingService.Core.Domain.Enums;

namespace DataProcessingService.Core.Interfaces.Repositories;

public interface IPipelineExecutionRepository : IRepository<PipelineExecution>
{
    Task<IReadOnlyList<PipelineExecution>> GetExecutionsByPipelineIdAsync(
        Guid pipelineId, 
        CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<PipelineExecution>> GetExecutionsByStatusAsync(
        ExecutionStatus status, 
        CancellationToken cancellationToken = default);
    
    Task<PipelineExecution?> GetLatestExecutionByPipelineIdAsync(
        Guid pipelineId, 
        CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<PipelineExecution>> GetExecutionsInDateRangeAsync(
        DateTimeOffset start, 
        DateTimeOffset end, 
        CancellationToken cancellationToken = default);
}
