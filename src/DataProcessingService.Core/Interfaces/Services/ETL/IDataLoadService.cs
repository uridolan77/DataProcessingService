using System.Collections.Generic;
using System.Dynamic;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.Core.Domain.Entities;

namespace DataProcessingService.Core.Interfaces.Services.ETL;

public interface IDataLoadService
{
    Task<int> LoadDataAsync(
        DataSource destination,
        string tableName,
        IEnumerable<ExpandoObject> data,
        bool createIfNotExists = false,
        bool truncateBeforeLoad = false,
        CancellationToken cancellationToken = default);
    
    Task<int> LoadDataStreamAsync(
        DataSource destination,
        string tableName,
        IAsyncEnumerable<ExpandoObject> dataStream,
        bool createIfNotExists = false,
        bool truncateBeforeLoad = false,
        CancellationToken cancellationToken = default);
    
    Task<bool> TableExistsAsync(
        DataSource destination,
        string tableName,
        CancellationToken cancellationToken = default);
    
    Task<bool> CreateTableAsync(
        DataSource destination,
        string tableName,
        IEnumerable<(string Name, string Type)> columns,
        CancellationToken cancellationToken = default);
}
