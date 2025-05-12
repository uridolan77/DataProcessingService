using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using DataProcessingService.Core.Domain.Entities;
using DataProcessingService.Core.Domain.Enums;
using DataProcessingService.Core.Interfaces.Repositories;
using DataProcessingService.Core.Interfaces.Services;

namespace DataProcessingService.Infrastructure.Services;

public class DataSourceService : IDataSourceService
{
    private readonly IDataSourceRepository _dataSourceRepository;
    private readonly ILogger<DataSourceService> _logger;
    
    public DataSourceService(
        IDataSourceRepository dataSourceRepository,
        ILogger<DataSourceService> logger)
    {
        _dataSourceRepository = dataSourceRepository;
        _logger = logger;
    }
    
    public async Task<DataSource?> GetDataSourceByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dataSourceRepository.GetByIdAsync(id, cancellationToken);
    }
    
    public async Task<DataSource?> GetDataSourceByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _dataSourceRepository.GetByNameAsync(name, cancellationToken);
    }
    
    public async Task<IReadOnlyList<DataSource>> GetAllDataSourcesAsync(CancellationToken cancellationToken = default)
    {
        return await _dataSourceRepository.GetAllAsync(cancellationToken);
    }
    
    public async Task<IReadOnlyList<DataSource>> GetActiveDataSourcesAsync(CancellationToken cancellationToken = default)
    {
        return await _dataSourceRepository.GetActiveDataSourcesAsync(cancellationToken);
    }
    
    public async Task<IReadOnlyList<DataSource>> GetDataSourcesByTypeAsync(
        DataSourceType type, 
        CancellationToken cancellationToken = default)
    {
        return await _dataSourceRepository.GetDataSourcesByTypeAsync(type, cancellationToken);
    }
    
    public async Task<DataSource> CreateDataSourceAsync(
        string name,
        string connectionString,
        DataSourceType type,
        string? schema = null,
        Dictionary<string, string>? properties = null,
        CancellationToken cancellationToken = default)
    {
        var existingDataSource = await _dataSourceRepository.GetByNameAsync(name, cancellationToken);
        if (existingDataSource != null)
        {
            throw new InvalidOperationException($"Data source with name '{name}' already exists");
        }
        
        var dataSource = new DataSource(name, connectionString, type, schema, properties);
        
        await _dataSourceRepository.AddAsync(dataSource, cancellationToken);
        
        _logger.LogInformation("Created data source {DataSourceId} with name {DataSourceName}", 
            dataSource.Id, dataSource.Name);
        
        return dataSource;
    }
    
    public async Task UpdateDataSourceAsync(
        Guid id,
        string? connectionString = null,
        string? schema = null,
        bool? isActive = null,
        Dictionary<string, string>? propertiesToAdd = null,
        IEnumerable<string>? propertiesToRemove = null,
        CancellationToken cancellationToken = default)
    {
        var dataSource = await _dataSourceRepository.GetByIdAsync(id, cancellationToken);
        if (dataSource == null)
        {
            throw new KeyNotFoundException($"Data source with ID {id} not found");
        }
        
        if (connectionString != null || schema != null)
        {
            dataSource.UpdateConnectionDetails(
                connectionString ?? dataSource.ConnectionString,
                schema ?? dataSource.Schema);
        }
        
        if (isActive.HasValue)
        {
            if (isActive.Value)
            {
                dataSource.Activate();
            }
            else
            {
                dataSource.Deactivate();
            }
        }
        
        if (propertiesToAdd != null)
        {
            foreach (var (key, value) in propertiesToAdd)
            {
                dataSource.AddProperty(key, value);
            }
        }
        
        if (propertiesToRemove != null)
        {
            foreach (var key in propertiesToRemove)
            {
                dataSource.RemoveProperty(key);
            }
        }
        
        await _dataSourceRepository.UpdateAsync(dataSource, cancellationToken);
        
        _logger.LogInformation("Updated data source {DataSourceId}", dataSource.Id);
    }
    
    public async Task DeleteDataSourceAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dataSource = await _dataSourceRepository.GetByIdAsync(id, cancellationToken);
        if (dataSource == null)
        {
            throw new KeyNotFoundException($"Data source with ID {id} not found");
        }
        
        await _dataSourceRepository.DeleteAsync(dataSource, cancellationToken);
        
        _logger.LogInformation("Deleted data source {DataSourceId}", id);
    }
    
    public async Task<bool> TestConnectionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dataSource = await _dataSourceRepository.GetByIdAsync(id, cancellationToken);
        if (dataSource == null)
        {
            throw new KeyNotFoundException($"Data source with ID {id} not found");
        }
        
        // In a real implementation, this would test the connection based on the data source type
        // For now, we'll just return true
        _logger.LogInformation("Testing connection for data source {DataSourceId}", id);
        
        return true;
    }
}
