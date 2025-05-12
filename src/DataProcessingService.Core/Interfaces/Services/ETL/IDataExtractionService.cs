using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.Core.Domain.Entities;

namespace DataProcessingService.Core.Interfaces.Services.ETL;

public interface IDataExtractionService
{
    Task<IEnumerable<ExpandoObject>> ExtractDataAsync(
        DataSource source,
        string query,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default);
    
    Task<IAsyncEnumerable<ExpandoObject>> ExtractDataStreamAsync(
        DataSource source,
        string query,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default);
    
    Task<IEnumerable<string>> GetAvailableTablesAsync(
        DataSource source,
        CancellationToken cancellationToken = default);
    
    Task<IEnumerable<(string Name, string Type)>> GetTableSchemaAsync(
        DataSource source,
        string tableName,
        CancellationToken cancellationToken = default);
}
