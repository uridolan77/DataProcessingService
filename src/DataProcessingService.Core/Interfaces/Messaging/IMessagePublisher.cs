using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DataProcessingService.Core.Interfaces.Messaging;

public interface IMessagePublisher
{
    Task PublishAsync<TMessage>(
        string topicName,
        TMessage message,
        CancellationToken cancellationToken = default) 
        where TMessage : class;
    
    Task PublishAsync<TMessage>(
        string topicName,
        TMessage message,
        string? partitionKey = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default) 
        where TMessage : class;
}
