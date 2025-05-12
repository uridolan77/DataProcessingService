using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using DataProcessingService.Core.Domain.Entities;
using DataProcessingService.Core.Domain.Enums;
using DataProcessingService.Core.Interfaces.Services.ETL;

namespace DataProcessingService.Infrastructure.Services.ETL;

public class DataExtractionService : IDataExtractionService
{
    private readonly ILogger<DataExtractionService> _logger;
    
    public DataExtractionService(ILogger<DataExtractionService> logger)
    {
        _logger = logger;
    }
    
    public async Task<IEnumerable<ExpandoObject>> ExtractDataAsync(
        DataSource source,
        string query,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Extracting data from {DataSourceName} using query: {Query}", 
            source.Name, query);
        
        // In a real implementation, this would connect to the data source and execute the query
        // For now, we'll just return some dummy data
        var result = new List<ExpandoObject>();
        
        for (int i = 0; i < 10; i++)
        {
            dynamic obj = new ExpandoObject();
            obj.Id = i;
            obj.Name = $"Item {i}";
            obj.Value = i * 10;
            obj.CreatedAt = DateTimeOffset.UtcNow.AddDays(-i);
            
            result.Add(obj);
        }
        
        await Task.Delay(100, cancellationToken); // Simulate some processing time
        
        return result;
    }
    
    public async Task<IAsyncEnumerable<ExpandoObject>> ExtractDataStreamAsync(
        DataSource source,
        string query,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Streaming data from {DataSourceName} using query: {Query}", 
            source.Name, query);
        
        // In a real implementation, this would connect to the data source and stream the results
        // For now, we'll just return the same dummy data as the non-streaming version
        var data = await ExtractDataAsync(source, query, parameters, cancellationToken);
        
        return data.ToAsyncEnumerable();
    }
    
    public async Task<IEnumerable<string>> GetAvailableTablesAsync(
        DataSource source,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting available tables from {DataSourceName}", source.Name);
        
        // In a real implementation, this would connect to the data source and get the available tables
        // For now, we'll just return some dummy tables
        await Task.Delay(100, cancellationToken); // Simulate some processing time
        
        return new[] { "Table1", "Table2", "Table3" };
    }
    
    public async Task<IEnumerable<(string Name, string Type)>> GetTableSchemaAsync(
        DataSource source,
        string tableName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting schema for table {TableName} from {DataSourceName}", 
            tableName, source.Name);
        
        // In a real implementation, this would connect to the data source and get the table schema
        // For now, we'll just return some dummy schema
        await Task.Delay(100, cancellationToken); // Simulate some processing time
        
        return new[]
        {
            ("Id", "int"),
            ("Name", "string"),
            ("Value", "decimal"),
            ("CreatedAt", "datetime")
        };
    }
}

// Extension method to convert IEnumerable to IAsyncEnumerable
public static class EnumerableExtensions
{
    public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> source)
    {
        foreach (var item in source)
        {
            yield return item;
        }
        
        await Task.CompletedTask;
    }
}
