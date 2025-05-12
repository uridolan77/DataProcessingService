using System;
using System.Collections.Generic;
using DataProcessingService.Core.Domain.Entities.Base;
using DataProcessingService.Core.Domain.Enums;

namespace DataProcessingService.Core.Domain.Entities;

public class PipelineExecution : Entity
{
    public Guid PipelineId { get; private set; }
    public DateTimeOffset StartTime { get; private set; }
    public DateTimeOffset? EndTime { get; private set; }
    public ExecutionStatus Status { get; private set; }
    public string? ErrorMessage { get; private set; }
    public int ProcessedRecords { get; private set; }
    public int FailedRecords { get; private set; }
    
    public virtual DataPipeline Pipeline { get; private set; } = null!;
    public List<ExecutionMetric> Metrics { get; private set; } = new();
    
    // For EF Core
    private PipelineExecution() { }
    
    public PipelineExecution(DataPipeline pipeline)
    {
        PipelineId = pipeline.Id;
        Pipeline = pipeline;
        StartTime = DateTimeOffset.UtcNow;
        Status = ExecutionStatus.Running;
        ProcessedRecords = 0;
        FailedRecords = 0;
    }
    
    public void Complete(int processedRecords)
    {
        Status = ExecutionStatus.Completed;
        EndTime = DateTimeOffset.UtcNow;
        ProcessedRecords = processedRecords;
    }
    
    public void Fail(string errorMessage, int processedRecords, int failedRecords)
    {
        Status = ExecutionStatus.Failed;
        EndTime = DateTimeOffset.UtcNow;
        ErrorMessage = errorMessage;
        ProcessedRecords = processedRecords;
        FailedRecords = failedRecords;
    }
    
    public void AddMetric(string name, string value, MetricType type)
    {
        Metrics.Add(new ExecutionMetric
        {
            Name = name,
            Value = value,
            Type = type,
            Timestamp = DateTimeOffset.UtcNow
        });
    }
    
    public void IncrementProcessedRecords(int count = 1)
    {
        ProcessedRecords += count;
    }
    
    public void IncrementFailedRecords(int count = 1)
    {
        FailedRecords += count;
    }
}
