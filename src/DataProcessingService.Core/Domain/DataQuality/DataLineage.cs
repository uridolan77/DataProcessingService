using System;
using System.Collections.Generic;
using DataProcessingService.Core.Domain.Entities.Base;

namespace DataProcessingService.Core.Domain.DataQuality;

public class DataLineage : AuditableEntity
{
    public Guid PipelineId { get; private set; }
    public Guid ExecutionId { get; private set; }
    public string SourceTable { get; private set; } = null!;
    public string TargetTable { get; private set; } = null!;
    public DateTimeOffset ProcessedAt { get; private set; }
    public List<ColumnLineage> ColumnMappings { get; private set; } = new();
    public List<LineageOperation> Operations { get; private set; } = new();
    
    // For EF Core
    private DataLineage() { }
    
    public DataLineage(
        Guid pipelineId,
        Guid executionId,
        string sourceTable,
        string targetTable,
        List<ColumnLineage> columnMappings,
        List<LineageOperation> operations)
    {
        PipelineId = pipelineId;
        ExecutionId = executionId;
        SourceTable = sourceTable;
        TargetTable = targetTable;
        ProcessedAt = DateTimeOffset.UtcNow;
        ColumnMappings = columnMappings;
        Operations = operations;
    }
}

public class ColumnLineage
{
    public string SourceColumn { get; set; } = null!;
    public string TargetColumn { get; set; } = null!;
    public string TransformationType { get; set; } = null!;
    public string? TransformationExpression { get; set; }
    public List<string> DependsOn { get; set; } = new();
}

public class LineageOperation
{
    public string OperationType { get; set; } = null!;
    public string Description { get; set; } = null!;
    public DateTimeOffset Timestamp { get; set; }
    public Dictionary<string, string> Parameters { get; set; } = new();
    
    public LineageOperation(
        string operationType,
        string description,
        Dictionary<string, string>? parameters = null)
    {
        OperationType = operationType;
        Description = description;
        Timestamp = DateTimeOffset.UtcNow;
        Parameters = parameters ?? new Dictionary<string, string>();
    }
}
