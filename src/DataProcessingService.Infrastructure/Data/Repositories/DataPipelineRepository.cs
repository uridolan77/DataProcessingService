using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DataProcessingService.Core.Domain.Entities;
using DataProcessingService.Core.Domain.Enums;
using DataProcessingService.Core.Interfaces.Repositories;

namespace DataProcessingService.Infrastructure.Data.Repositories;

public class DataPipelineRepository : RepositoryBase<DataPipeline>, IDataPipelineRepository
{
    public DataPipelineRepository(AppDbContext dbContext) : base(dbContext)
    {
    }
    
    public async Task<IReadOnlyList<DataPipeline>> GetPipelinesByStatusAsync(
        PipelineStatus status, 
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(dp => dp.Status == status)
            .ToListAsync(cancellationToken);
    }
    
    public async Task<IReadOnlyList<DataPipeline>> GetPipelinesByTypeAsync(
        PipelineType type, 
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(dp => dp.Type == type)
            .ToListAsync(cancellationToken);
    }
    
    public async Task<IReadOnlyList<DataPipeline>> GetPipelinesDueForExecutionAsync(
        DateTimeOffset currentTime, 
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(dp => dp.Status == PipelineStatus.Idle && 
                    dp.NextExecutionTime.HasValue && 
                    dp.NextExecutionTime.Value <= currentTime)
            .ToListAsync(cancellationToken);
    }
    
    public async Task<IReadOnlyList<DataPipeline>> GetPipelinesBySourceIdAsync(
        Guid sourceId, 
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(dp => dp.SourceId == sourceId)
            .ToListAsync(cancellationToken);
    }
    
    public async Task<IReadOnlyList<DataPipeline>> GetPipelinesByDestinationIdAsync(
        Guid destinationId, 
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(dp => dp.DestinationId == destinationId)
            .ToListAsync(cancellationToken);
    }
    
    public async Task<DataPipeline?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .FirstOrDefaultAsync(dp => dp.Name == name, cancellationToken);
    }
    
    public async Task<DataPipeline?> GetWithRelatedDataAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(dp => dp.Source)
            .Include(dp => dp.Destination)
            .FirstOrDefaultAsync(dp => dp.Id == id, cancellationToken);
    }
}
