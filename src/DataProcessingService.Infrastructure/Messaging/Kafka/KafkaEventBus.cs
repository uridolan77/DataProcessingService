using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.Core.Domain.Events;
using DataProcessingService.Core.Interfaces.Messaging;
using Microsoft.Extensions.Logging;

namespace DataProcessingService.Infrastructure.Messaging.Kafka;

public class KafkaEventBus : IEventBus
{
    private readonly IMessagePublisher _messagePublisher;
    private readonly ILogger<KafkaEventBus> _logger;
    
    public KafkaEventBus(
        IMessagePublisher messagePublisher,
        ILogger<KafkaEventBus> logger)
    {
        _messagePublisher = messagePublisher;
        _logger = logger;
    }
    
    public async Task PublishDomainEventAsync<TEvent>(
        TEvent domainEvent,
        CancellationToken cancellationToken = default) 
        where TEvent : DomainEvent
    {
        var eventType = domainEvent.GetType().Name;
        var topicName = $"DomainEvents.{eventType}";
        
        _logger.LogInformation("Publishing domain event {EventType} with ID {EventId} to Kafka", 
            eventType, domainEvent.Id);
        
        await _messagePublisher.PublishAsync(topicName, domainEvent, cancellationToken);
    }
    
    public async Task PublishIntegrationEventAsync<TEvent>(
        TEvent integrationEvent,
        CancellationToken cancellationToken = default) 
        where TEvent : class
    {
        var eventType = integrationEvent.GetType().Name;
        var topicName = $"IntegrationEvents.{eventType}";
        
        _logger.LogInformation("Publishing integration event {EventType} to Kafka", eventType);
        
        await _messagePublisher.PublishAsync(topicName, integrationEvent, cancellationToken);
    }
}
