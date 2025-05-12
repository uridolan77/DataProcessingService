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
