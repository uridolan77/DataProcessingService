using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.Core.Interfaces.Messaging;
using Microsoft.Extensions.Logging;

namespace DataProcessingService.Infrastructure.Messaging;

public class InMemoryMessageConsumer : IMessageConsumer
{
    private readonly Dictionary<string, object> _subscriptions = new();
    private readonly ILogger<InMemoryMessageConsumer> _logger;
    
    public InMemoryMessageConsumer(ILogger<InMemoryMessageConsumer> logger)
    {
        _logger = logger;
    }
    
    public Task SubscribeAsync<TMessage>(
        string topicName,
        string subscriptionName,
        Func<TMessage, IDictionary<string, string>, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default) 
        where TMessage : class
    {
        var key = $"{topicName}:{subscriptionName}";
        
        if (_subscriptions.ContainsKey(key))
        {
            throw new InvalidOperationException($"Subscription {subscriptionName} to topic {topicName} already exists");
        }
        
        _subscriptions[key] = handler;
        
        _logger.LogInformation("Subscribed to topic {TopicName} with subscription {SubscriptionName}", 
            topicName, subscriptionName);
        
        return Task.CompletedTask;
    }
    
    public Task UnsubscribeAsync(
        string topicName,
        string subscriptionName,
        CancellationToken cancellationToken = default)
    {
        var key = $"{topicName}:{subscriptionName}";
        
        if (_subscriptions.ContainsKey(key))
        {
            _subscriptions.Remove(key);
            
            _logger.LogInformation("Unsubscribed from topic {TopicName} with subscription {SubscriptionName}", 
                topicName, subscriptionName);
        }
        
        return Task.CompletedTask;
    }
}
