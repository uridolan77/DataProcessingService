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
