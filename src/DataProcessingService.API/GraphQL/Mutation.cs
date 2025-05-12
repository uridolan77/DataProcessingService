using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.API.DTOs;
using DataProcessingService.Core.Domain.ValueObjects;
using DataProcessingService.Core.Interfaces.Services;
using HotChocolate;

namespace DataProcessingService.API.GraphQL;

public class Mutation
{
    // Data Sources
    public async Task<DataSourceDto> CreateDataSourceAsync(
        [Service] IDataSourceService dataSourceService,
        CreateDataSourceDto input,
        CancellationToken cancellationToken)
    {
        var dataSource = await dataSourceService.CreateDataSourceAsync(
            input.Name,
            input.ConnectionString,
            input.Type,
            input.Schema,
            input.Properties,
            cancellationToken);
        
        return new DataSourceDto
        {
            Id = dataSource.Id,
            Name = dataSource.Name,
            ConnectionString = dataSource.ConnectionString,
            Type = dataSource.Type,
            Schema = dataSource.Schema,
            Properties = dataSource.Properties,
            IsActive = dataSource.IsActive,
            LastSyncTime = dataSource.LastSyncTime,
            CreatedAt = dataSource.CreatedAt,
            CreatedBy = dataSource.CreatedBy,
            LastModifiedAt = dataSource.LastModifiedAt,
            LastModifiedBy = dataSource.LastModifiedBy
        };
    }
    
    public async Task<bool> UpdateDataSourceAsync(
        [Service] IDataSourceService dataSourceService,
        Guid id,
        UpdateDataSourceDto input,
        CancellationToken cancellationToken)
    {
        await dataSourceService.UpdateDataSourceAsync(
            id,
            input.ConnectionString,
            input.Schema,
            input.IsActive,
            input.PropertiesToAdd,
            input.PropertiesToRemove,
            cancellationToken);
        
        return true;
    }
    
    public async Task<bool> DeleteDataSourceAsync(
        [Service] IDataSourceService dataSourceService,
        Guid id,
        CancellationToken cancellationToken)
    {
        await dataSourceService.DeleteDataSourceAsync(id, cancellationToken);
        return true;
    }
    
    public async Task<TestConnectionDto> TestConnectionAsync(
        [Service] IDataSourceService dataSourceService,
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            bool success = await dataSourceService.TestConnectionAsync(id, cancellationToken);
            
            return new TestConnectionDto
            {
                Success = success,
                Message = success ? "Connection successful" : "Connection failed"
            };
        }
        catch (Exception ex)
        {
            return new TestConnectionDto
            {
                Success = false,
                Message = $"Connection failed: {ex.Message}"
            };
        }
    }
    
    // Data Pipelines
    public async Task<DataPipelineDto> CreateDataPipelineAsync(
        [Service] IDataPipelineService dataPipelineService,
        [Service] IDataSourceService dataSourceService,
        CreateDataPipelineDto input,
        CancellationToken cancellationToken)
    {
        var schedule = MapToExecutionSchedule(input.Schedule);
        var transformationRules = MapToTransformationRules(input.TransformationRules);
        
        var pipeline = await dataPipelineService.CreatePipelineAsync(
            input.Name,
            input.Description,
            input.Type,
            input.SourceId,
            schedule,
            transformationRules,
            input.DestinationId,
            cancellationToken);
        
        var source = await dataSourceService.GetDataSourceByIdAsync(pipeline.SourceId, cancellationToken);
        var sourceDto = source != null ? MapToDataSourceDto(source) : null;
        
        DataSourceDto? destinationDto = null;
        if (pipeline.DestinationId.HasValue)
        {
            var destination = await dataSourceService.GetDataSourceByIdAsync(pipeline.DestinationId.Value, cancellationToken);
            if (destination != null)
            {
                destinationDto = MapToDataSourceDto(destination);
            }
        }
        
        return new DataPipelineDto
        {
            Id = pipeline.Id,
            Name = pipeline.Name,
            Description = pipeline.Description,
            Type = pipeline.Type,
            Status = pipeline.Status,
            SourceId = pipeline.SourceId,
            DestinationId = pipeline.DestinationId,
            Schedule = MapToScheduleDto(pipeline.Schedule),
            TransformationRules = MapToTransformationRuleDtos(pipeline.TransformationRules),
            LastExecutionTime = pipeline.LastExecutionTime,
            NextExecutionTime = pipeline.NextExecutionTime,
            Source = sourceDto!,
            Destination = destinationDto,
            CreatedAt = pipeline.CreatedAt,
            CreatedBy = pipeline.CreatedBy,
            LastModifiedAt = pipeline.LastModifiedAt,
            LastModifiedBy = pipeline.LastModifiedBy
        };
    }
    
    public async Task<bool> UpdateDataPipelineAsync(
        [Service] IDataPipelineService dataPipelineService,
        Guid id,
        UpdateDataPipelineDto input,
        CancellationToken cancellationToken)
    {
        ExecutionSchedule? schedule = null;
        if (input.Schedule != null)
        {
            schedule = MapToExecutionSchedule(input.Schedule);
        }
        
        TransformationRules? transformationRules = null;
        if (input.TransformationRules != null)
        {
            transformationRules = MapToTransformationRules(input.TransformationRules);
        }
        
        await dataPipelineService.UpdatePipelineAsync(
            id,
            input.Description,
            schedule,
            transformationRules,
            cancellationToken);
        
        return true;
    }
    
    public async Task<bool> DeleteDataPipelineAsync(
        [Service] IDataPipelineService dataPipelineService,
        Guid id,
        CancellationToken cancellationToken)
    {
        await dataPipelineService.DeletePipelineAsync(id, cancellationToken);
        return true;
    }
    
    public async Task<PipelineExecutionDto> ExecutePipelineAsync(
        [Service] IDataPipelineService dataPipelineService,
        Guid id,
        CancellationToken cancellationToken)
    {
        var execution = await dataPipelineService.ExecutePipelineAsync(id, cancellationToken);
        
        return new PipelineExecutionDto
        {
            Id = execution.Id,
            PipelineId = execution.PipelineId,
            StartTime = execution.StartTime,
            EndTime = execution.EndTime,
            Status = execution.Status,
            ErrorMessage = execution.ErrorMessage,
            ProcessedRecords = execution.ProcessedRecords,
            FailedRecords = execution.FailedRecords,
            Metrics = execution.Metrics.Select(m => new ExecutionMetricDto
            {
                Id = m.Id,
                Name = m.Name,
                Value = m.Value,
                Type = m.Type,
                Timestamp = m.Timestamp
            }).ToList()
        };
    }
    
    public async Task<bool> PausePipelineAsync(
        [Service] IDataPipelineService dataPipelineService,
        Guid id,
        CancellationToken cancellationToken)
    {
        await dataPipelineService.PausePipelineAsync(id, cancellationToken);
        return true;
    }
    
    public async Task<bool> ResumePipelineAsync(
        [Service] IDataPipelineService dataPipelineService,
        Guid id,
        CancellationToken cancellationToken)
    {
        await dataPipelineService.ResumePipelineAsync(id, cancellationToken);
        return true;
    }
    
    // Helper methods
    private static DataSourceDto MapToDataSourceDto(Core.Domain.Entities.DataSource dataSource)
    {
        return new DataSourceDto
        {
            Id = dataSource.Id,
            Name = dataSource.Name,
            ConnectionString = dataSource.ConnectionString,
            Type = dataSource.Type,
            Schema = dataSource.Schema,
            Properties = dataSource.Properties,
            IsActive = dataSource.IsActive,
            LastSyncTime = dataSource.LastSyncTime,
            CreatedAt = dataSource.CreatedAt,
            CreatedBy = dataSource.CreatedBy,
            LastModifiedAt = dataSource.LastModifiedAt,
            LastModifiedBy = dataSource.LastModifiedBy
        };
    }
    
    private static ExecutionScheduleDto MapToScheduleDto(ExecutionSchedule schedule)
    {
        return new ExecutionScheduleDto
        {
            Type = schedule.Type,
            Interval = schedule.Interval,
            CronExpression = schedule.CronExpression
        };
    }
    
    private static List<TransformationRuleDto> MapToTransformationRuleDtos(TransformationRules rules)
    {
        return rules.Rules.Select(r => new TransformationRuleDto
        {
            Id = r.Id,
            SourceField = r.SourceField,
            TargetField = r.TargetField,
            Type = r.Type,
            FormatString = r.FormatString,
            Parameters = r.Parameters,
            Order = r.Order
        }).ToList();
    }
    
    private static ExecutionSchedule MapToExecutionSchedule(ExecutionScheduleDto dto)
    {
        return dto.Type switch
        {
            Core.Domain.Enums.ScheduleType.Interval => ExecutionSchedule.CreateInterval(dto.Interval!.Value),
            Core.Domain.Enums.ScheduleType.Cron => ExecutionSchedule.CreateCustomCron(dto.CronExpression!),
            _ => throw new ArgumentException($"Unsupported schedule type: {dto.Type}")
        };
    }
    
    private static TransformationRules MapToTransformationRules(List<TransformationRuleDto>? dtos)
    {
        if (dtos == null || !dtos.Any())
        {
            return TransformationRules.Empty;
        }
        
        var rules = dtos.Select(dto => new TransformationRule
        {
            Id = dto.Id ?? Guid.NewGuid(),
            SourceField = dto.SourceField,
            TargetField = dto.TargetField,
            Type = dto.Type,
            FormatString = dto.FormatString,
            Parameters = dto.Parameters ?? new Dictionary<string, string>(),
            Order = dto.Order
        }).ToList();
        
        return new TransformationRules(rules);
    }
}
