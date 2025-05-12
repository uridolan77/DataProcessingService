using System;
using System.Collections.Generic;
using DataProcessingService.Core.Domain.Enums;
using DataProcessingService.Core.Domain.ValueObjects;

namespace DataProcessingService.API.DTOs;

public class DataPipelineDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public PipelineType Type { get; set; }
    public PipelineStatus Status { get; set; }
    public Guid SourceId { get; set; }
    public Guid? DestinationId { get; set; }
    public ExecutionScheduleDto Schedule { get; set; } = null!;
    public List<TransformationRuleDto> TransformationRules { get; set; } = new();
    public DateTimeOffset? LastExecutionTime { get; set; }
    public DateTimeOffset? NextExecutionTime { get; set; }
    public DataSourceDto Source { get; set; } = null!;
    public DataSourceDto? Destination { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTimeOffset? LastModifiedAt { get; set; }
    public string? LastModifiedBy { get; set; }
}

public class CreateDataPipelineDto
{
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public PipelineType Type { get; set; }
    public Guid SourceId { get; set; }
    public Guid? DestinationId { get; set; }
    public ExecutionScheduleDto Schedule { get; set; } = null!;
    public List<TransformationRuleDto>? TransformationRules { get; set; }
}

public class UpdateDataPipelineDto
{
    public string? Description { get; set; }
    public ExecutionScheduleDto? Schedule { get; set; }
    public List<TransformationRuleDto>? TransformationRules { get; set; }
}

public class ExecutionScheduleDto
{
    public ScheduleType Type { get; set; }
    public int? Interval { get; set; }
    public string? CronExpression { get; set; }
}

public class TransformationRuleDto
{
    public Guid? Id { get; set; }
    public string SourceField { get; set; } = null!;
    public string? TargetField { get; set; }
    public TransformationType Type { get; set; }
    public string? FormatString { get; set; }
    public Dictionary<string, string>? Parameters { get; set; }
    public int Order { get; set; }
}
