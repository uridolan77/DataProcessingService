using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DataProcessingService.Core.Domain.Entities;
using DataProcessingService.Core.Domain.Enums;
using DataProcessingService.Core.Interfaces.Repositories;

namespace DataProcessingService.Infrastructure.Data.Repositories;

public class PipelineExecutionRepository : RepositoryBase<PipelineExecution>, IPipelineExecutionRepository
{
    public PipelineExecutionRepository(AppDbContext dbContext) : base(dbContext)
    {
    }
    
    public async Task<IReadOnlyList<PipelineExecution>> GetExecutionsByPipelineIdAsync(
        Guid pipelineId, 
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(pe => pe.PipelineId == pipelineId)
            .OrderByDescending(pe => pe.StartTime)
            .ToListAsync(cancellationToken);
    }
    
    public async Task<IReadOnlyList<PipelineExecution>> GetExecutionsByStatusAsync(
        ExecutionStatus status, 
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(pe => pe.Status == status)
            .OrderByDescending(pe => pe.StartTime)
            .ToListAsync(cancellationToken);
    }
    
    public async Task<PipelineExecution?> GetLatestExecutionByPipelineIdAsync(
        Guid pipelineId, 
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(pe => pe.PipelineId == pipelineId)
            .OrderByDescending(pe => pe.StartTime)
            .FirstOrDefaultAsync(cancellationToken);
    }
    
    public async Task<IReadOnlyList<PipelineExecution>> GetExecutionsInDateRangeAsync(
        DateTimeOffset start, 
        DateTimeOffset end, 
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(pe => pe.StartTime >= start && pe.StartTime <= end)
            .OrderByDescending(pe => pe.StartTime)
            .ToListAsync(cancellationToken);
    }
}
