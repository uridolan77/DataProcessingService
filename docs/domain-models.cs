# Core Domain Models and Entities

## src/DataProcessingService.Core/Domain/Entities/DataSource.cs
```csharp
using System;
using System.Collections.Generic;
using DataProcessingService.Core.Domain.Entities.Base;
using DataProcessingService.Core.Domain.Enums;

namespace DataProcessingService.Core.Domain.Entities;

public class DataSource : AuditableEntity
{
    public string Name { get; private set; } = null!;
    public string ConnectionString { get; private set; } = null!;
    public DataSourceType Type { get; private set; }
    public string? Schema { get; private set; }
    public Dictionary<string, string> Properties { get; private set; } = new();
    public bool IsActive { get; private set; }
    public DateTimeOffset LastSyncTime { get; private set; }
    
    public ICollection<DataPipeline> DataPipelines { get; private set; } = new List<DataPipeline>();
    
    // For EF Core
    private DataSource() { }
    
    public DataSource(
        string name, 
        string connectionString, 
        DataSourceType type, 
        string? schema = null,
        Dictionary<string, string>? properties = null)
    {
        Name = name;
        ConnectionString = connectionString;
        Type = type;
        Schema = schema;
        Properties = properties ?? new Dictionary<string, string>();
        IsActive = true;
        LastSyncTime = DateTimeOffset.UtcNow;
    }
    
    public void UpdateConnectionDetails(string connectionString, string? schema)
    {
        ConnectionString = connectionString;
        Schema = schema;
    }
    
    public void Activate() => IsActive = true;
    
    public void Deactivate() => IsActive = false;
    
    public void UpdateLastSyncTime(DateTimeOffset syncTime) => LastSyncTime = syncTime;
    
    public void AddProperty(string key, string value) => Properties[key] = value;
    
    public void RemoveProperty(string key) => Properties.Remove(key);
}

## src/DataProcessingService.Core/Domain/Entities/DataPipeline.cs
```csharp
using System;
using System.Collections.Generic;
using DataProcessingService.Core.Domain.Entities.Base;
using DataProcessingService.Core.Domain.Enums;
using DataProcessingService.Core.Domain.Events;
using DataProcessingService.Core.Domain.ValueObjects;

namespace DataProcessingService.Core.Domain.Entities;

public class DataPipeline : AuditableEntity
{
    public string Name { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public PipelineType Type { get; private set; }
    public PipelineStatus Status { get; private set; }
    public Guid SourceId { get; private set; }
    public Guid? DestinationId { get; private set; }
    public ExecutionSchedule Schedule { get; private set; } = null!;
    public TransformationRules TransformationRules { get; private set; } = null!;
    public DateTimeOffset? LastExecutionTime { get; private set; }
    public DateTimeOffset? NextExecutionTime { get; private set; }
    
    public virtual DataSource Source { get; private set; } = null!;
    public virtual DataSource? Destination { get; private set; }
    public ICollection<PipelineExecution> Executions { get; private set; } = new List<PipelineExecution>();
    
    // For EF Core
    private DataPipeline() { }
    
    public DataPipeline(
        string name,
        string description,
        PipelineType type,
        DataSource source,
        ExecutionSchedule schedule,
        TransformationRules transformationRules,
        DataSource? destination = null)
    {
        Name = name;
        Description = description;
        Type = type;
        Status = PipelineStatus.Idle;
        Source = source;
        SourceId = source.Id;
        Schedule = schedule;
        TransformationRules = transformationRules;
        
        if (destination != null)
        {
            Destination = destination;
            DestinationId = destination.Id;
        }
        
        CalculateNextExecutionTime();
    }
    
    public void Start()
    {
        if (Status != PipelineStatus.Failed && Status != PipelineStatus.Idle)
            throw new InvalidOperationException("Cannot start pipeline in current state");
            
        Status = PipelineStatus.Running;
        AddDomainEvent(new PipelineStartedEvent(Id, Name));
    }
    
    public void Complete(DateTimeOffset executionTime)
    {
        Status = PipelineStatus.Idle;
        LastExecutionTime = executionTime;
        CalculateNextExecutionTime();
        AddDomainEvent(new PipelineCompletedEvent(Id, Name, executionTime));
    }
    
    public void Fail(string reason, DateTimeOffset executionTime)
    {
        Status = PipelineStatus.Failed;
        LastExecutionTime = executionTime;
        AddDomainEvent(new PipelineFailedEvent(Id, Name, reason, executionTime));
    }
    
    public void Pause()
    {
        if (Status == PipelineStatus.Running)
        {
            Status = PipelineStatus.Paused;
            AddDomainEvent(new PipelinePausedEvent(Id, Name));
        }
    }
    
    public void UpdateSchedule(ExecutionSchedule newSchedule)
    {
        Schedule = newSchedule;
        CalculateNextExecutionTime();
    }
    
    public void UpdateTransformationRules(TransformationRules newRules)
    {
        TransformationRules = newRules;
    }
    
    private void CalculateNextExecutionTime()
    {
        NextExecutionTime = Schedule.CalculateNextExecution(LastExecutionTime ?? DateTimeOffset.UtcNow);
    }
    
    public bool ShouldExecute(DateTimeOffset currentTime)
    {
        return Status == PipelineStatus.Idle && 
               NextExecutionTime.HasValue && 
               currentTime >= NextExecutionTime.Value;
    }
}

## src/DataProcessingService.Core/Domain/Entities/PipelineExecution.cs
```csharp
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

## src/DataProcessingService.Core/Domain/Entities/ExecutionMetric.cs
```csharp
using System;
using DataProcessingService.Core.Domain.Enums;

namespace DataProcessingService.Core.Domain.Entities;

public class ExecutionMetric
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = null!;
    public string Value { get; set; } = null!;
    public MetricType Type { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}

## src/DataProcessingService.Core/Domain/Entities/Base/Entity.cs
```csharp
using System;
using System.Collections.Generic;
using DataProcessingService.Core.Domain.Events;

namespace DataProcessingService.Core.Domain.Entities.Base;

public abstract class Entity
{
    private readonly List<DomainEvent> _domainEvents = new();
    
    public Guid Id { get; protected set; } = Guid.NewGuid();
    
    public IReadOnlyCollection<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    
    public void AddDomainEvent(DomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }
    
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
    
    public override bool Equals(object? obj)
    {
        if (obj is not Entity other)
            return false;
        
        if (ReferenceEquals(this, other))
            return true;
        
        if (GetType() != other.GetType())
            return false;
        
        return Id == other.Id;
    }
    
    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
    
    public static bool operator ==(Entity? left, Entity? right)
    {
        if (left is null && right is null)
            return true;
        
        if (left is null || right is null)
            return false;
        
        return left.Equals(right);
    }
    
    public static bool operator !=(Entity? left, Entity? right)
    {
        return !(left == right);
    }
}

## src/DataProcessingService.Core/Domain/Entities/Base/AuditableEntity.cs
```csharp
using System;

namespace DataProcessingService.Core.Domain.Entities.Base;

public abstract class AuditableEntity : Entity
{
    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public string? LastModifiedBy { get; set; }
    public DateTimeOffset? LastModifiedAt { get; set; }
}

## src/DataProcessingService.Core/Domain/ValueObjects/ExecutionSchedule.cs
```csharp
using System;
using DataProcessingService.Core.Domain.Enums;

namespace DataProcessingService.Core.Domain.ValueObjects;

public class ExecutionSchedule
{
    public ScheduleType Type { get; private set; }
    public int? Interval { get; private set; }
    public string? CronExpression { get; private set; }
    
    private ExecutionSchedule() { }
    
    private ExecutionSchedule(ScheduleType type, int? interval, string? cronExpression)
    {
        Type = type;
        Interval = interval;
        CronExpression = cronExpression;
    }
    
    public static ExecutionSchedule CreateInterval(int minutes)
    {
        if (minutes <= 0)
            throw new ArgumentException("Interval must be greater than zero", nameof(minutes));
            
        return new ExecutionSchedule(ScheduleType.Interval, minutes, null);
    }
    
    public static ExecutionSchedule CreateDaily(int hour, int minute)
    {
        if (hour < 0 || hour > 23)
            throw new ArgumentOutOfRangeException(nameof(hour), "Hour must be between 0 and 23");
            
        if (minute < 0 || minute > 59)
            throw new ArgumentOutOfRangeException(nameof(minute), "Minute must be between 0 and 59");
            
        string cronExpression = $"0 {minute} {hour} * * *";
        return new ExecutionSchedule(ScheduleType.Cron, null, cronExpression);
    }
    
    public static ExecutionSchedule CreateWeekly(DayOfWeek dayOfWeek, int hour, int minute)
    {
        if (hour < 0 || hour > 23)
            throw new ArgumentOutOfRangeException(nameof(hour), "Hour must be between 0 and 23");
            
        if (minute < 0 || minute > 59)
            throw new ArgumentOutOfRangeException(nameof(minute), "Minute must be between 0 and 59");
            
        int cronDayOfWeek = (int)dayOfWeek; // In cron, Sunday is 0
        string cronExpression = $"0 {minute} {hour} * * {cronDayOfWeek}";
        return new ExecutionSchedule(ScheduleType.Cron, null, cronExpression);
    }
    
    public static ExecutionSchedule CreateMonthly(int dayOfMonth, int hour, int minute)
    {
        if (dayOfMonth < 1 || dayOfMonth > 31)
            throw new ArgumentOutOfRangeException(nameof(dayOfMonth), "Day of month must be between 1 and 31");
            
        if (hour < 0 || hour > 23)
            throw new ArgumentOutOfRangeException(nameof(hour), "Hour must be between 0 and 23");
            
        if (minute < 0 || minute > 59)
            throw new ArgumentOutOfRangeException(nameof(minute), "Minute must be between 0 and 59");
            
        string cronExpression = $"0 {minute} {hour} {dayOfMonth} * *";
        return new ExecutionSchedule(ScheduleType.Cron, null, cronExpression);
    }
    
    public static ExecutionSchedule CreateCustomCron(string cronExpression)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
            throw new ArgumentException("Cron expression cannot be empty", nameof(cronExpression));
            
        // Here you might want to validate the cron expression format
        return new ExecutionSchedule(ScheduleType.Cron, null, cronExpression);
    }
    
    public DateTimeOffset CalculateNextExecution(DateTimeOffset fromTime)
    {
        return Type switch
        {
            ScheduleType.Interval => fromTime.AddMinutes(Interval!.Value),
            ScheduleType.Cron => CalculateNextCronExecution(fromTime),
            _ => throw new NotImplementedException($"Schedule type {Type} is not supported")
        };
    }
    
    private DateTimeOffset CalculateNextCronExecution(DateTimeOffset fromTime)
    {
        // This would normally use a library like Cronos to parse cron expressions
        // For simplicity, we'll just add a day as a placeholder
        // In a real implementation, this would properly parse and calculate based on the cron expression
        return fromTime.AddDays(1);
    }
}

## src/DataProcessingService.Core/Domain/ValueObjects/TransformationRules.cs
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DataProcessingService.Core.Domain.Enums;

namespace DataProcessingService.Core.Domain.ValueObjects;

public class TransformationRules
{
    public List<TransformationRule> Rules { get; private set; } = new();
    
    private TransformationRules() { }
    
    public TransformationRules(IEnumerable<TransformationRule> rules)
    {
        Rules = rules.ToList();
    }
    
    public static TransformationRules Empty => new(Enumerable.Empty<TransformationRule>());
    
    public static TransformationRules FromJson(string json)
    {
        if (string.IsNullOrEmpty(json))
            return Empty;
            
        var rules = JsonSerializer.Deserialize<List<TransformationRule>>(json) ?? new List<TransformationRule>();
        return new TransformationRules(rules);
    }
    
    public string ToJson()
    {
        return JsonSerializer.Serialize(Rules);
    }
    
    public void AddRule(TransformationRule rule)
    {
        Rules.Add(rule);
    }
    
    public void RemoveRule(Guid ruleId)
    {
        Rules.RemoveAll(r => r.Id == ruleId);
    }
    
    public TransformationRule? FindRule(Guid ruleId)
    {
        return Rules.FirstOrDefault(r => r.Id == ruleId);
    }
}

public class TransformationRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string SourceField { get; set; } = null!;
    public string? TargetField { get; set; }
    public TransformationType Type { get; set; }
    public string? FormatString { get; set; }
    public Dictionary<string, string> Parameters { get; set; } = new();
    public int Order { get; set; }
    
    // For JSON deserialization
    public TransformationRule() { }
    
    public static TransformationRule CreateCopy(string sourceField, string targetField, int order = 0)
    {
        return new TransformationRule
        {
            SourceField = sourceField,
            TargetField = targetField,
            Type = TransformationType.Copy,
            Order = order
        };
    }
    
    public static TransformationRule CreateFormat(string sourceField, string targetField, string formatString, int order = 0)
    {
        return new TransformationRule
        {
            SourceField = sourceField,
            TargetField = targetField,
            Type = TransformationType.Format,
            FormatString = formatString,
            Order = order
        };
    }
    
    public static TransformationRule CreateConcatenate(
        IEnumerable<string> sourceFields, 
        string targetField, 
        string separator = " ", 
        int order = 0)
    {
        return new TransformationRule
        {
            SourceField = string.Join(",", sourceFields),
            TargetField = targetField,
            Type = TransformationType.Concatenate,
            Parameters = new Dictionary<string, string> { ["separator"] = separator },
            Order = order
        };
    }
    
    public static TransformationRule CreateCustom(
        string sourceField, 
        string targetField, 
        Dictionary<string, string> parameters, 
        int order = 0)
    {
        return new TransformationRule
        {
            SourceField = sourceField,
            TargetField = targetField,
            Type = TransformationType.Custom,
            Parameters = parameters,
            Order = order
        };
    }
}

## src/DataProcessingService.Core/Domain/Events/DomainEvent.cs
```csharp
using System;

namespace DataProcessingService.Core.Domain.Events;

public abstract class DomainEvent
{
    public Guid Id { get; }
    public DateTimeOffset OccurredOn { get; }
    
    protected DomainEvent()
    {
        Id = Guid.NewGuid();
        OccurredOn = DateTimeOffset.UtcNow;
    }
}

## src/DataProcessingService.Core/Domain/Events/PipelineStartedEvent.cs
```csharp
using System;

namespace DataProcessingService.Core.Domain.Events;

public class PipelineStartedEvent : DomainEvent
{
    public Guid PipelineId { get; }
    public string PipelineName { get; }
    
    public PipelineStartedEvent(Guid pipelineId, string pipelineName)
    {
        PipelineId = pipelineId;
        PipelineName = pipelineName;
    }
}

## src/DataProcessingService.Core/Domain/Events/PipelineCompletedEvent.cs
```csharp
using System;

namespace DataProcessingService.Core.Domain.Events;

public class PipelineCompletedEvent : DomainEvent
{
    public Guid PipelineId { get; }
    public string PipelineName { get; }
    public DateTimeOffset ExecutionTime { get; }
    
    public PipelineCompletedEvent(
        Guid pipelineId, 
        string pipelineName, 
        DateTimeOffset executionTime)
    {
        PipelineId = pipelineId;
        PipelineName = pipelineName;
        ExecutionTime = executionTime;
    }
}

## src/DataProcessingService.Core/Domain/Events/PipelineFailedEvent.cs
```csharp
using System;

namespace DataProcessingService.Core.Domain.Events;

public class PipelineFailedEvent : DomainEvent
{
    public Guid PipelineId { get; }
    public string PipelineName { get; }
    public string Reason { get; }
    public DateTimeOffset ExecutionTime { get; }
    
    public PipelineFailedEvent(
        Guid pipelineId, 
        string pipelineName, 
        string reason, 
        DateTimeOffset executionTime)
    {
        PipelineId = pipelineId;
        PipelineName = pipelineName;
        Reason = reason;
        ExecutionTime = executionTime;
    }
}

## src/DataProcessingService.Core/Domain/Events/PipelinePausedEvent.cs
```csharp
using System;

namespace DataProcessingService.Core.Domain.Events;

public class PipelinePausedEvent : DomainEvent
{
    public Guid PipelineId { get; }
    public string PipelineName { get; }
    
    public PipelinePausedEvent(Guid pipelineId, string pipelineName)
    {
        PipelineId = pipelineId;
        PipelineName = pipelineName;
    }
}

## src/DataProcessingService.Core/Domain/Enums/DataSourceType.cs
```csharp
namespace DataProcessingService.Core.Domain.Enums;

public enum DataSourceType
{
    SqlServer,
    PostgreSql,
    MySql,
    Oracle,
    MongoDb,
    CosmosDb,
    Csv,
    Json,
    Xml,
    RestApi,
    GraphQl,
    Kafka,
    RabbitMq,
    AzureServiceBus,
    AwsSqs,
    Custom
}

## src/DataProcessingService.Core/Domain/Enums/PipelineType.cs
```csharp
namespace DataProcessingService.Core.Domain.Enums;

public enum PipelineType
{
    Extract,
    Transform,
    Load,
    Etl,
    Replication,
    Integration,
    Validation,
    Aggregation,
    Enrichment,
    Custom
}

## src/DataProcessingService.Core/Domain/Enums/PipelineStatus.cs
```csharp
namespace DataProcessingService.Core.Domain.Enums;

public enum PipelineStatus
{
    Idle,
    Running,
    Paused,
    Failed
}

## src/DataProcessingService.Core/Domain/Enums/ScheduleType.cs
```csharp
namespace DataProcessingService.Core.Domain.Enums;

public enum ScheduleType
{
    Interval,
    Cron
}

## src/DataProcessingService.Core/Domain/Enums/TransformationType.cs
```csharp
namespace DataProcessingService.Core.Domain.Enums;

public enum TransformationType
{
    Copy,
    Format,
    Concatenate,
    Split,
    Replace,
    Trim,
    Uppercase,
    Lowercase,
    Substring,
    DateFormat,
    NumberFormat,
    Round,
    Floor,
    Ceiling,
    Lookup,
    Conditional,
    Calculated,
    Custom
}

## src/DataProcessingService.Core/Domain/Enums/ExecutionStatus.cs
```csharp
namespace DataProcessingService.Core.Domain.Enums;

public enum ExecutionStatus
{
    Running,
    Completed,
    Failed,
    Canceled
}

## src/DataProcessingService.Core/Domain/Enums/MetricType.cs
```csharp
namespace DataProcessingService.Core.Domain.Enums;

public enum MetricType
{
    Counter,
    Gauge,
    Timer,
    Text
}
