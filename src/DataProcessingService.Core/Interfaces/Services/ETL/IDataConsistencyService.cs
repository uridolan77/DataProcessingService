using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.Core.Domain.Entities;

namespace DataProcessingService.Core.Interfaces.Services.ETL;

public interface IDataConsistencyService
{
    Task<bool> ValidateDataConsistencyAsync(
        DataSource source,
        DataSource destination,
        string sourceTable,
        string destinationTable,
        IEnumerable<string> keyColumns,
        CancellationToken cancellationToken = default);
    
    Task<(int MissingRecords, int DifferentRecords, int TotalRecords)> GetConsistencyMetricsAsync(
        DataSource source,
        DataSource destination,
        string sourceTable,
        string destinationTable,
        IEnumerable<string> keyColumns,
        IEnumerable<string>? compareColumns = null,
        CancellationToken cancellationToken = default);
    
    Task<int> SynchronizeDataAsync(
        DataSource source,
        DataSource destination,
        string sourceTable,
        string destinationTable,
        IEnumerable<string> keyColumns,
        IEnumerable<string>? syncColumns = null,
        CancellationToken cancellationToken = default);
    
    Task<(string Hash, DateTimeOffset Timestamp)> ComputeTableChecksumAsync(
        DataSource source,
        string tableName,
        IEnumerable<string>? columns = null,
        CancellationToken cancellationToken = default);
}
