using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.API.DTOs;
using DataProcessingService.Core.Domain.Enums;
using DataProcessingService.Core.Interfaces.Services;
using HotChocolate;
using HotChocolate.Types;

namespace DataProcessingService.API.GraphQL;

public class Query
{
    // Data Sources
    public async Task<DataSourceDto?> GetDataSourceAsync(
        [Service] IDataSourceService dataSourceService,
        Guid id,
        CancellationToken cancellationToken)
    {
        var dataSource = await dataSourceService.GetDataSourceByIdAsync(id, cancellationToken);
        return dataSource != null ? MapToDataSourceDto(dataSource) : null;
    }
    
    public async Task<IEnumerable<DataSourceDto>> GetDataSourcesAsync(
        [Service] IDataSourceService dataSourceService,
        bool? activeOnly,
        DataSourceType? type,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Core.Domain.Entities.DataSource> dataSources;
        
        if (activeOnly == true)
        {
            dataSources = await dataSourceService.GetActiveDataSourcesAsync(cancellationToken);
        }
        else if (type.HasValue)
        {
            dataSources = await dataSourceService.GetDataSourcesByTypeAsync(type.Value, cancellationToken);
        }
        else
        {
            dataSources = await dataSourceService.GetAllDataSourcesAsync(cancellationToken);
        }
        
        return dataSources.Select(MapToDataSourceDto);
    }
    
    // Data Pipelines
    public async Task<DataPipelineDto?> GetDataPipelineAsync(
        [Service] IDataPipelineService dataPipelineService,
        [Service] IDataSourceService dataSourceService,
        Guid id,
        CancellationToken cancellationToken)
    {
        var pipeline = await dataPipelineService.GetPipelineByIdAsync(id, cancellationToken);
        return pipeline != null ? await MapToDataPipelineDtoAsync(pipeline, dataSourceService, cancellationToken) : null;
    }
    
    public async Task<IEnumerable<DataPipelineDto>> GetDataPipelinesAsync(
        [Service] IDataPipelineService dataPipelineService,
        [Service] IDataSourceService dataSourceService,
        PipelineStatus? status,
        PipelineType? type,
        Guid? sourceId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Core.Domain.Entities.DataPipeline> pipelines;
        
        if (status.HasValue)
        {
            pipelines = await dataPipelineService.GetPipelinesByStatusAsync(status.Value, cancellationToken);
        }
        else if (type.HasValue)
        {
            pipelines = await dataPipelineService.GetPipelinesByTypeAsync(type.Value, cancellationToken);
        }
        else if (sourceId.HasValue)
        {
            pipelines = await dataPipelineService.GetPipelinesBySourceIdAsync(sourceId.Value, cancellationToken);
        }
        else
        {
            pipelines = await dataPipelineService.GetAllPipelinesAsync(cancellationToken);
        }
        
        var dtos = new List<DataPipelineDto>();
        foreach (var pipeline in pipelines)
        {
            dtos.Add(await MapToDataPipelineDtoAsync(pipeline, dataSourceService, cancellationToken));
        }
        
        return dtos;
    }
    
    // Pipeline Executions
    public async Task<PipelineExecutionDto?> GetPipelineExecutionAsync(
        [Service] IPipelineExecutionService pipelineExecutionService,
        Guid id,
        CancellationToken cancellationToken)
    {
        var execution = await pipelineExecutionService.GetExecutionByIdAsync(id, cancellationToken);
        return execution != null ? MapToPipelineExecutionDto(execution) : null;
    }
    
    public async Task<IEnumerable<PipelineExecutionDto>> GetPipelineExecutionsAsync(
        [Service] IPipelineExecutionService pipelineExecutionService,
        Guid? pipelineId,
        ExecutionStatus? status,
        DateTimeOffset? startDate,
        DateTimeOffset? endDate,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Core.Domain.Entities.PipelineExecution> executions;
        
        if (pipelineId.HasValue)
        {
            executions = await pipelineExecutionService.GetExecutionsByPipelineIdAsync(pipelineId.Value, cancellationToken);
        }
        else if (status.HasValue)
        {
            executions = await pipelineExecutionService.GetExecutionsByStatusAsync(status.Value, cancellationToken);
        }
        else if (startDate.HasValue && endDate.HasValue)
        {
            executions = await pipelineExecutionService.GetExecutionsInDateRangeAsync(startDate.Value, endDate.Value, cancellationToken);
        }
        else
        {
            // Default to last 24 hours if no filters provided
            var end = DateTimeOffset.UtcNow;
            var start = end.AddDays(-1);
            executions = await pipelineExecutionService.GetExecutionsInDateRangeAsync(start, end, cancellationToken);
        }
        
        return executions.Select(MapToPipelineExecutionDto);
    }
    
    // Execution Statistics
    public async Task<ExecutionStatisticsDto> GetExecutionStatisticsAsync(
        [Service] IPipelineExecutionService pipelineExecutionService,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        CancellationToken cancellationToken)
    {
        var statistics = await pipelineExecutionService.GetExecutionStatisticsAsync(startDate, endDate, cancellationToken);
        
        return new ExecutionStatisticsDto
        {
            TotalExecutions = statistics.GetValueOrDefault("TotalExecutions"),
            CompletedExecutions = statistics.GetValueOrDefault("CompletedExecutions"),
            FailedExecutions = statistics.GetValueOrDefault("FailedExecutions"),
            RunningExecutions = statistics.GetValueOrDefault("RunningExecutions"),
            CanceledExecutions = statistics.GetValueOrDefault("CanceledExecutions"),
            TotalProcessedRecords = statistics.GetValueOrDefault("TotalProcessedRecords"),
            TotalFailedRecords = statistics.GetValueOrDefault("TotalFailedRecords"),
            StartDate = startDate,
            EndDate = endDate
        };
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
    
    private static async Task<DataPipelineDto> MapToDataPipelineDtoAsync(
        Core.Domain.Entities.DataPipeline pipeline,
        IDataSourceService dataSourceService,
        CancellationToken cancellationToken)
    {
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
            Schedule = new ExecutionScheduleDto
            {
                Type = pipeline.Schedule.Type,
                Interval = pipeline.Schedule.Interval,
                CronExpression = pipeline.Schedule.CronExpression
            },
            TransformationRules = pipeline.TransformationRules.Rules.Select(r => new TransformationRuleDto
            {
                Id = r.Id,
                SourceField = r.SourceField,
                TargetField = r.TargetField,
                Type = r.Type,
                FormatString = r.FormatString,
                Parameters = r.Parameters,
                Order = r.Order
            }).ToList(),
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
    
    private static PipelineExecutionDto MapToPipelineExecutionDto(Core.Domain.Entities.PipelineExecution execution)
    {
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
}
