using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DataProcessingService.Core.Domain.Entities;
using DataProcessingService.Core.Domain.Enums;
using DataProcessingService.Core.Interfaces.Repositories;

namespace DataProcessingService.Infrastructure.Data.Repositories;

public class DataSourceRepository : RepositoryBase<DataSource>, IDataSourceRepository
{
    public DataSourceRepository(AppDbContext dbContext) : base(dbContext)
    {
    }
    
    public async Task<IReadOnlyList<DataSource>> GetActiveDataSourcesAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(ds => ds.IsActive)
            .ToListAsync(cancellationToken);
    }
    
    public async Task<IReadOnlyList<DataSource>> GetDataSourcesByTypeAsync(
        DataSourceType type, 
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(ds => ds.Type == type)
            .ToListAsync(cancellationToken);
    }
    
    public async Task<DataSource?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .FirstOrDefaultAsync(ds => ds.Name == name, cancellationToken);
    }
}
