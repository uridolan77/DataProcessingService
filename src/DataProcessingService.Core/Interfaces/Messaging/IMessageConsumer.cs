using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DataProcessingService.Core.Interfaces.Messaging;

public interface IMessageConsumer
{
    Task SubscribeAsync<TMessage>(
        string topicName,
        string subscriptionName,
        Func<TMessage, IDictionary<string, string>, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default) 
        where TMessage : class;
    
    Task UnsubscribeAsync(
        string topicName,
        string subscriptionName,
        CancellationToken cancellationToken = default);
}
