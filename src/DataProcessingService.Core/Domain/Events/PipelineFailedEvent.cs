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
