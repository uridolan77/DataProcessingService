using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.Core.Domain.DataQuality;
using DataProcessingService.Core.Domain.Entities;

namespace DataProcessingService.Core.Interfaces.Services.DataQuality;

public interface IDataLineageService
{
    Task<DataLineage?> GetLineageByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<DataLineage>> GetLineageByPipelineIdAsync(
        Guid pipelineId, 
        CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<DataLineage>> GetLineageByExecutionIdAsync(
        Guid executionId, 
        CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<DataLineage>> GetLineageBySourceTableAsync(
        string sourceTable, 
        CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<DataLineage>> GetLineageByTargetTableAsync(
        string targetTable, 
        CancellationToken cancellationToken = default);
    
    Task<DataLineage> CreateLineageAsync(
        Guid pipelineId,
        Guid executionId,
        string sourceTable,
        string targetTable,
        List<ColumnLineage> columnMappings,
        List<LineageOperation> operations,
        CancellationToken cancellationToken = default);
    
    Task<DataLineage> CreateLineageFromPipelineAsync(
        DataPipeline pipeline,
        PipelineExecution execution,
        CancellationToken cancellationToken = default);
    
    Task DeleteLineageAsync(Guid id, CancellationToken cancellationToken = default);
    
    Task<List<DataLineage>> GetUpstreamLineageAsync(
        string tableName,
        int depth = 1,
        CancellationToken cancellationToken = default);
    
    Task<List<DataLineage>> GetDownstreamLineageAsync(
        string tableName,
        int depth = 1,
        CancellationToken cancellationToken = default);
    
    Task<Dictionary<string, object>> GenerateLineageGraphAsync(
        string tableName,
        int upstreamDepth = 1,
        int downstreamDepth = 1,
        CancellationToken cancellationToken = default);
    
    Task<Dictionary<string, object>> GenerateImpactAnalysisAsync(
        string tableName,
        string columnName,
        CancellationToken cancellationToken = default);
}
