using System;
using System.Collections.Generic;
using DataProcessingService.Core.Domain.Enums;

namespace DataProcessingService.API.DTOs;

public class PipelineExecutionDto
{
    public Guid Id { get; set; }
    public Guid PipelineId { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset? EndTime { get; set; }
    public ExecutionStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public int ProcessedRecords { get; set; }
    public int FailedRecords { get; set; }
    public List<ExecutionMetricDto> Metrics { get; set; } = new();
}

public class ExecutionMetricDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Value { get; set; } = null!;
    public MetricType Type { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}

public class ExecutionStatisticsDto
{
    public int TotalExecutions { get; set; }
    public int CompletedExecutions { get; set; }
    public int FailedExecutions { get; set; }
    public int RunningExecutions { get; set; }
    public int CanceledExecutions { get; set; }
    public int TotalProcessedRecords { get; set; }
    public int TotalFailedRecords { get; set; }
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset EndDate { get; set; }
}
