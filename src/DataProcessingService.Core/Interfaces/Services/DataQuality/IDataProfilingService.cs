using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.Core.Domain.DataQuality;
using DataProcessingService.Core.Domain.Entities;

namespace DataProcessingService.Core.Interfaces.Services.DataQuality;

public interface IDataProfilingService
{
    Task<DataProfile?> GetProfileByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    Task<DataProfile?> GetProfileByDataSourceAndTableAsync(
        Guid dataSourceId, 
        string tableName, 
        CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<DataProfile>> GetProfilesByDataSourceAsync(
        Guid dataSourceId, 
        CancellationToken cancellationToken = default);
    
    Task<DataProfile> CreateProfileAsync(
        Guid dataSourceId,
        string tableName,
        CancellationToken cancellationToken = default);
    
    Task<DataProfile> CreateProfileAsync(
        DataSource dataSource,
        string tableName,
        CancellationToken cancellationToken = default);
    
    Task<DataProfile> CreateProfileFromDataAsync(
        Guid dataSourceId,
        string tableName,
        IEnumerable<IDictionary<string, object?>> data,
        CancellationToken cancellationToken = default);
    
    Task<DataProfile> CreateProfileFromDataAsync(
        Guid dataSourceId,
        string tableName,
        IEnumerable<ExpandoObject> data,
        CancellationToken cancellationToken = default);
    
    Task UpdateProfileAsync(Guid id, CancellationToken cancellationToken = default);
    
    Task DeleteProfileAsync(Guid id, CancellationToken cancellationToken = default);
    
    Task<List<DataQualityIssue>> DetectAnomaliesAsync(
        Guid profileId,
        IDictionary<string, object?> data,
        CancellationToken cancellationToken = default);
    
    Task<List<DataQualityIssue>> DetectAnomaliesAsync(
        DataProfile profile,
        IDictionary<string, object?> data,
        CancellationToken cancellationToken = default);
    
    Task<Dictionary<string, double>> CalculateDataQualityScoreAsync(
        Guid profileId,
        CancellationToken cancellationToken = default);
    
    Task<Dictionary<string, double>> CalculateDataQualityScoreAsync(
        DataProfile profile,
        CancellationToken cancellationToken = default);
    
    Task<Dictionary<string, object>> GenerateDataQualityReportAsync(
        Guid profileId,
        CancellationToken cancellationToken = default);
}
