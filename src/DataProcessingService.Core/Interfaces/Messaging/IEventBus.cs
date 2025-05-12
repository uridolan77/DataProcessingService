using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.Core.Domain.Events;

namespace DataProcessingService.Core.Interfaces.Messaging;

public interface IEventBus
{
    Task PublishDomainEventAsync<TEvent>(
        TEvent domainEvent,
        CancellationToken cancellationToken = default) 
        where TEvent : DomainEvent;
    
    Task PublishIntegrationEventAsync<TEvent>(
        TEvent integrationEvent,
        CancellationToken cancellationToken = default) 
        where TEvent : class;
}
