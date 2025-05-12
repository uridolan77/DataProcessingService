using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.Core.Domain.Entities;
using DataProcessingService.Core.Domain.ValueObjects;

namespace DataProcessingService.Core.Interfaces.Services.ETL;

public interface IStreamProcessingService
{
    Task<Guid> StartStreamProcessingAsync(
        Guid pipelineId,
        CancellationToken cancellationToken = default);
    
    Task<bool> StopStreamProcessingAsync(
        Guid streamId,
        CancellationToken cancellationToken = default);
    
    Task<bool> PauseStreamProcessingAsync(
        Guid streamId,
        CancellationToken cancellationToken = default);
    
    Task<bool> ResumeStreamProcessingAsync(
        Guid streamId,
        CancellationToken cancellationToken = default);
    
    Task<StreamProcessingStatus> GetStreamStatusAsync(
        Guid streamId,
        CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<StreamProcessingStatus>> GetAllStreamStatusesAsync(
        CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<StreamProcessingStatus>> GetStreamStatusesByPipelineIdAsync(
        Guid pipelineId,
        CancellationToken cancellationToken = default);
    
    Task<StreamProcessingMetrics> GetStreamMetricsAsync(
        Guid streamId,
        CancellationToken cancellationToken = default);
    
    Task ProcessRecordAsync(
        Guid streamId,
        ExpandoObject record,
        CancellationToken cancellationToken = default);
    
    Task ProcessBatchAsync(
        Guid streamId,
        IEnumerable<ExpandoObject> records,
        CancellationToken cancellationToken = default);
    
    Task<IAsyncEnumerable<ExpandoObject>> CreateStreamTransformationPipelineAsync(
        IAsyncEnumerable<ExpandoObject> sourceStream,
        TransformationRules transformationRules,
        CancellationToken cancellationToken = default);
    
    Task<IAsyncEnumerable<ExpandoObject>> CreateStreamWindowedAggregationAsync(
        IAsyncEnumerable<ExpandoObject> sourceStream,
        TimeSpan windowSize,
        string groupByField,
        Dictionary<string, AggregationType> aggregations,
        CancellationToken cancellationToken = default);
    
    Task<IAsyncEnumerable<ExpandoObject>> CreateStreamJoinAsync(
        IAsyncEnumerable<ExpandoObject> leftStream,
        IAsyncEnumerable<ExpandoObject> rightStream,
        string leftJoinField,
        string rightJoinField,
        TimeSpan joinWindow,
        CancellationToken cancellationToken = default);
}

public class StreamProcessingStatus
{
    public Guid StreamId { get; set; }
    public Guid PipelineId { get; set; }
    public string PipelineName { get; set; } = null!;
    public StreamState State { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset? EndTime { get; set; }
    public string? ErrorMessage { get; set; }
}

public class StreamProcessingMetrics
{
    public Guid StreamId { get; set; }
    public long RecordsProcessed { get; set; }
    public long RecordsPerSecond { get; set; }
    public long ErrorCount { get; set; }
    public long BackpressureCount { get; set; }
    public double AverageProcessingTimeMs { get; set; }
    public double CpuUsagePercent { get; set; }
    public double MemoryUsageMb { get; set; }
    public Dictionary<string, double> CustomMetrics { get; set; } = new();
}

public enum StreamState
{
    Starting,
    Running,
    Paused,
    Stopping,
    Stopped,
    Failed
}

public enum AggregationType
{
    Count,
    Sum,
    Min,
    Max,
    Average,
    First,
    Last,
    Custom
}
