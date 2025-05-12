using System.Collections.Generic;

namespace DataProcessingService.Infrastructure.Messaging.Kafka;

public class KafkaConfiguration
{
    public List<string> BootstrapServers { get; set; } = new();
    public string GroupId { get; set; } = "data-processing-service";
    public bool EnableAutoCommit { get; set; } = false;
    public int AutoCommitIntervalMs { get; set; } = 5000;
    public string AutoOffsetReset { get; set; } = "earliest";
    public int SessionTimeoutMs { get; set; } = 30000;
    public int MaxPollIntervalMs { get; set; } = 300000;
    public bool EnablePartitionEof { get; set; } = false;
}
