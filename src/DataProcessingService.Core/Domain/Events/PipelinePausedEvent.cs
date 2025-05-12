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
