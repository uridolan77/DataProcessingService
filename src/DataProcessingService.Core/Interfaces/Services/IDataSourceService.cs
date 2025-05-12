using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.Core.Domain.Entities;
using DataProcessingService.Core.Domain.Enums;

namespace DataProcessingService.Core.Interfaces.Services;

public interface IDataSourceService
{
    Task<DataSource?> GetDataSourceByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    Task<DataSource?> GetDataSourceByNameAsync(string name, CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<DataSource>> GetAllDataSourcesAsync(CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<DataSource>> GetActiveDataSourcesAsync(CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<DataSource>> GetDataSourcesByTypeAsync(
        DataSourceType type, 
        CancellationToken cancellationToken = default);
    
    Task<DataSource> CreateDataSourceAsync(
        string name,
        string connectionString,
        DataSourceType type,
        string? schema = null,
        Dictionary<string, string>? properties = null,
        CancellationToken cancellationToken = default);
    
    Task UpdateDataSourceAsync(
        Guid id,
        string? connectionString = null,
        string? schema = null,
        bool? isActive = null,
        Dictionary<string, string>? propertiesToAdd = null,
        IEnumerable<string>? propertiesToRemove = null,
        CancellationToken cancellationToken = default);
    
    Task DeleteDataSourceAsync(Guid id, CancellationToken cancellationToken = default);
    
    Task<bool> TestConnectionAsync(Guid id, CancellationToken cancellationToken = default);
}
