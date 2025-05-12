using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.Core.Interfaces.Messaging;
using Microsoft.Extensions.Logging;

namespace DataProcessingService.Infrastructure.Messaging;

public class InMemoryMessagePublisher : IMessagePublisher
{
    private static readonly Dictionary<string, List<object>> Messages = new();
    
    private readonly ILogger<InMemoryMessagePublisher> _logger;
    
    public InMemoryMessagePublisher(ILogger<InMemoryMessagePublisher> logger)
    {
        _logger = logger;
    }
    
    public Task PublishAsync<TMessage>(
        string topicName,
        TMessage message,
        CancellationToken cancellationToken = default) 
        where TMessage : class
    {
        return PublishAsync(topicName, message, null, null, cancellationToken);
    }
    
    public Task PublishAsync<TMessage>(
        string topicName,
        TMessage message,
        string? partitionKey = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default) 
        where TMessage : class
    {
        if (!Messages.ContainsKey(topicName))
        {
            Messages[topicName] = new List<object>();
        }
        
        Messages[topicName].Add(message);
        
        _logger.LogInformation("Published message to topic {TopicName}", topicName);
        
        return Task.CompletedTask;
    }
}
