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
