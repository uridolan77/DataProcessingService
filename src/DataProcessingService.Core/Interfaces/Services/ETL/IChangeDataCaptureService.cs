using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.Core.Domain.Entities;

namespace DataProcessingService.Core.Interfaces.Services.ETL;

public interface IChangeDataCaptureService
{
    Task<bool> EnableCdcForTableAsync(
        DataSource dataSource,
        string tableName,
        string[]? columns = null,
        CancellationToken cancellationToken = default);
    
    Task<bool> DisableCdcForTableAsync(
        DataSource dataSource,
        string tableName,
        CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<string>> GetCdcEnabledTablesAsync(
        DataSource dataSource,
        CancellationToken cancellationToken = default);
    
    Task<CdcTableInfo> GetCdcTableInfoAsync(
        DataSource dataSource,
        string tableName,
        CancellationToken cancellationToken = default);
    
    Task<IEnumerable<CdcChange>> GetChangesAsync(
        DataSource dataSource,
        string tableName,
        DateTimeOffset fromTime,
        DateTimeOffset toTime,
        CancellationToken cancellationToken = default);
    
    Task<IEnumerable<CdcChange>> GetChangesSinceLastCaptureAsync(
        DataSource dataSource,
        string tableName,
        CancellationToken cancellationToken = default);
    
    Task<IEnumerable<ExpandoObject>> ApplyChangesToTargetAsync(
        IEnumerable<CdcChange> changes,
        DataSource targetDataSource,
        string targetTable,
        bool createIfNotExists = false,
        CancellationToken cancellationToken = default);
    
    Task<CdcSyncResult> SynchronizeTablesAsync(
        DataSource sourceDataSource,
        string sourceTable,
        DataSource targetDataSource,
        string targetTable,
        bool createIfNotExists = false,
        CancellationToken cancellationToken = default);
    
    Task<DateTimeOffset> MarkSyncPointAsync(
        DataSource dataSource,
        string tableName,
        CancellationToken cancellationToken = default);
    
    Task<IAsyncEnumerable<CdcChange>> SubscribeToChangesAsync(
        DataSource dataSource,
        string tableName,
        CancellationToken cancellationToken = default);
}

public class CdcTableInfo
{
    public string TableName { get; set; } = null!;
    public bool CdcEnabled { get; set; }
    public DateTimeOffset? EnabledSince { get; set; }
    public string[] TrackedColumns { get; set; } = Array.Empty<string>();
    public DateTimeOffset? LastSyncPoint { get; set; }
    public long TotalChangesCaptured { get; set; }
}

public class CdcChange
{
    public string TableName { get; set; } = null!;
    public CdcOperation Operation { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string TransactionId { get; set; } = null!;
    public ExpandoObject BeforeImage { get; set; } = new ExpandoObject();
    public ExpandoObject AfterImage { get; set; } = new ExpandoObject();
    public string[] PrimaryKeyColumns { get; set; } = Array.Empty<string>();
}

public enum CdcOperation
{
    Insert,
    Update,
    Delete
}

public class CdcSyncResult
{
    public string SourceTable { get; set; } = null!;
    public string TargetTable { get; set; } = null!;
    public DateTimeOffset SyncStartTime { get; set; }
    public DateTimeOffset SyncEndTime { get; set; }
    public int InsertCount { get; set; }
    public int UpdateCount { get; set; }
    public int DeleteCount { get; set; }
    public int ErrorCount { get; set; }
    public List<string> Errors { get; set; } = new();
}
