using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.Core.Domain.Entities;
using DataProcessingService.Core.Domain.Enums;

namespace DataProcessingService.Core.Interfaces.Repositories;

public interface IDataSourceRepository : IRepository<DataSource>
{
    Task<IReadOnlyList<DataSource>> GetActiveDataSourcesAsync(CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<DataSource>> GetDataSourcesByTypeAsync(
        DataSourceType type, 
        CancellationToken cancellationToken = default);
    
    Task<DataSource?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
}
