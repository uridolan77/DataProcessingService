using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using DataProcessingService.Core.Interfaces.Messaging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DataProcessingService.Infrastructure.Messaging.Kafka;

public class KafkaMessageConsumer : IMessageConsumer
{
    private readonly KafkaConfiguration _configuration;
    private readonly ILogger<KafkaMessageConsumer> _logger;
    private readonly Dictionary<string, IHostedService> _consumers = new();
    private readonly IServiceProvider _serviceProvider;
    
    public KafkaMessageConsumer(
        IOptions<KafkaConfiguration> configuration,
        ILogger<KafkaMessageConsumer> logger,
        IServiceProvider serviceProvider)
    {
        _configuration = configuration.Value;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }
    
    public Task SubscribeAsync<TMessage>(
        string topicName,
        string subscriptionName,
        Func<TMessage, IDictionary<string, string>, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default) 
        where TMessage : class
    {
        var key = $"{topicName}:{subscriptionName}";
        
        if (_consumers.ContainsKey(key))
        {
            throw new InvalidOperationException($"Subscription {subscriptionName} to topic {topicName} already exists");
        }
        
        var consumer = new KafkaConsumerService<TMessage>(
            _configuration,
            topicName,
            subscriptionName,
            handler,
            _logger);
        
        _consumers[key] = consumer;
        
        // In a real application, we would register this with the host
        // For now, we'll just start it manually
        _ = consumer.StartAsync(cancellationToken);
        
        _logger.LogInformation("Subscribed to Kafka topic {TopicName} with subscription {SubscriptionName}", 
            topicName, subscriptionName);
        
        return Task.CompletedTask;
    }
    
    public async Task UnsubscribeAsync(
        string topicName,
        string subscriptionName,
        CancellationToken cancellationToken = default)
    {
        var key = $"{topicName}:{subscriptionName}";
        
        if (_consumers.TryGetValue(key, out var consumer))
        {
            await consumer.StopAsync(cancellationToken);
            _consumers.Remove(key);
            
            _logger.LogInformation("Unsubscribed from Kafka topic {TopicName} with subscription {SubscriptionName}", 
                topicName, subscriptionName);
        }
    }
    
    private class KafkaConsumerService<TMessage> : BackgroundService where TMessage : class
    {
        private readonly KafkaConfiguration _configuration;
        private readonly string _topicName;
        private readonly string _subscriptionName;
        private readonly Func<TMessage, IDictionary<string, string>, CancellationToken, Task> _handler;
        private readonly ILogger _logger;
        private IConsumer<string, string>? _consumer;
        
        public KafkaConsumerService(
            KafkaConfiguration configuration,
            string topicName,
            string subscriptionName,
            Func<TMessage, IDictionary<string, string>, CancellationToken, Task> handler,
            ILogger logger)
        {
            _configuration = configuration;
            _topicName = topicName;
            _subscriptionName = subscriptionName;
            _handler = handler;
            _logger = logger;
        }
        
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.Run(async () =>
            {
                try
                {
                    var config = new ConsumerConfig
                    {
                        BootstrapServers = string.Join(",", _configuration.BootstrapServers),
                        GroupId = $"{_configuration.GroupId}-{_subscriptionName}",
                        EnableAutoCommit = _configuration.EnableAutoCommit,
                        AutoCommitIntervalMs = _configuration.AutoCommitIntervalMs,
                        AutoOffsetReset = Enum.Parse<AutoOffsetReset>(_configuration.AutoOffsetReset, true),
                        SessionTimeoutMs = _configuration.SessionTimeoutMs,
                        MaxPollIntervalMs = _configuration.MaxPollIntervalMs,
                        EnablePartitionEof = _configuration.EnablePartitionEof
                    };
                    
                    _consumer = new ConsumerBuilder<string, string>(config).Build();
                    _consumer.Subscribe(_topicName);
                    
                    _logger.LogInformation("Started Kafka consumer for topic {TopicName} with subscription {SubscriptionName}", 
                        _topicName, _subscriptionName);
                    
                    while (!stoppingToken.IsCancellationRequested)
                    {
                        try
                        {
                            var consumeResult = _consumer.Consume(stoppingToken);
                            
                            if (consumeResult.IsPartitionEOF)
                            {
                                continue;
                            }
                            
                            var message = JsonSerializer.Deserialize<TMessage>(consumeResult.Message.Value);
                            
                            if (message == null)
                            {
                                _logger.LogWarning("Received null message from Kafka topic {TopicName}", _topicName);
                                continue;
                            }
                            
                            var headers = new Dictionary<string, string>();
                            foreach (var header in consumeResult.Message.Headers)
                            {
                                headers[header.Key] = Encoding.UTF8.GetString(header.GetValueBytes());
                            }
                            
                            await _handler(message, headers, stoppingToken);
                            
                            if (!_configuration.EnableAutoCommit)
                            {
                                _consumer.Commit(consumeResult);
                            }
                        }
                        catch (ConsumeException ex)
                        {
                            _logger.LogError(ex, "Error consuming message from Kafka topic {TopicName}", _topicName);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Normal shutdown
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in Kafka consumer for topic {TopicName}", _topicName);
                }
                finally
                {
                    _consumer?.Close();
                    _consumer?.Dispose();
                }
            }, stoppingToken);
        }
        
        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _consumer?.Close();
            return base.StopAsync(cancellationToken);
        }
    }
}
