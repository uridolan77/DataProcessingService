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
