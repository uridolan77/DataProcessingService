using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using DataProcessingService.Core.Interfaces.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DataProcessingService.Infrastructure.Messaging.Kafka;

public class KafkaMessagePublisher : IMessagePublisher
{
    private readonly KafkaConfiguration _configuration;
    private readonly ILogger<KafkaMessagePublisher> _logger;
    
    public KafkaMessagePublisher(
        IOptions<KafkaConfiguration> configuration,
        ILogger<KafkaMessagePublisher> logger)
    {
        _configuration = configuration.Value;
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
    
    public async Task PublishAsync<TMessage>(
        string topicName,
        TMessage message,
        string? partitionKey = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default) 
        where TMessage : class
    {
        try
        {
            var config = new ProducerConfig
            {
                BootstrapServers = string.Join(",", _configuration.BootstrapServers)
            };
            
            using var producer = new ProducerBuilder<string, string>(config).Build();
            
            var messageValue = JsonSerializer.Serialize(message);
            var key = partitionKey ?? Guid.NewGuid().ToString();
            
            var kafkaMessage = new Message<string, string>
            {
                Key = key,
                Value = messageValue
            };
            
            if (headers != null)
            {
                kafkaMessage.Headers = new Headers();
                foreach (var header in headers)
                {
                    kafkaMessage.Headers.Add(header.Key, System.Text.Encoding.UTF8.GetBytes(header.Value));
                }
            }
            
            var result = await producer.ProduceAsync(topicName, kafkaMessage, cancellationToken);
            
            _logger.LogInformation("Published message to Kafka topic {TopicName} with offset {Offset}", 
                topicName, result.Offset.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing message to Kafka topic {TopicName}", topicName);
            throw;
        }
    }
}
