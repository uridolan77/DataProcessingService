using System;
using DataProcessingService.Core.Domain.Enums;

namespace DataProcessingService.Core.Domain.Entities;

public class ExecutionMetric
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = null!;
    public string Value { get; set; } = null!;
    public MetricType Type { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}
