using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.Core.Domain.Entities;

namespace DataProcessingService.Core.Interfaces.Services.ETL;

public interface IDataReplicationService
{
    Task<int> ReplicateTableAsync(
        DataSource source,
        DataSource destination,
        string sourceTable,
        string? destinationTable = null,
        bool createIfNotExists = false,
        IEnumerable<string>? keyColumns = null,
        ReplicationMode mode = ReplicationMode.Full,
        CancellationToken cancellationToken = default);
    
    Task<IDictionary<string, int>> ReplicateAllTablesAsync(
        DataSource source,
        DataSource destination,
        bool createIfNotExists = false,
        IEnumerable<string>? excludedTables = null,
        ReplicationMode mode = ReplicationMode.Full,
        CancellationToken cancellationToken = default);
    
    Task<int> ReplicateIncrementalAsync(
        DataSource source,
        DataSource destination,
        string sourceTable,
        string? destinationTable = null,
        string changeTrackingColumn = "LastModified",
        CancellationToken cancellationToken = default);
    
    Task ConfigureChangeDataCaptureAsync(
        DataSource source,
        string tableName,
        IEnumerable<string>? columns = null,
        CancellationToken cancellationToken = default);
}

public enum ReplicationMode
{
    Full,
    Incremental,
    ChangeDataCapture
}
