using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using DataProcessingService.API.DTOs;
using DataProcessingService.Core.Domain.Entities;
using DataProcessingService.Core.Domain.ValueObjects;
using DataProcessingService.Core.Interfaces.Services;

namespace DataProcessingService.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class DataPipelinesController : ControllerBase
{
    private readonly IDataPipelineService _dataPipelineService;
    private readonly IDataSourceService _dataSourceService;
    private readonly ILogger<DataPipelinesController> _logger;
    
    public DataPipelinesController(
        IDataPipelineService dataPipelineService,
        IDataSourceService dataSourceService,
        ILogger<DataPipelinesController> logger)
    {
        _dataPipelineService = dataPipelineService;
        _dataSourceService = dataSourceService;
        _logger = logger;
    }
    
    /// <summary>
    /// Gets all data pipelines
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of data pipelines</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<DataPipelineDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllPipelines(CancellationToken cancellationToken)
    {
        var pipelines = await _dataPipelineService.GetAllPipelinesAsync(cancellationToken);
        var dtos = new List<DataPipelineDto>();
        
        foreach (var pipeline in pipelines)
        {
            var dto = await MapToDtoAsync(pipeline, cancellationToken);
            dtos.Add(dto);
        }
        
        return Ok(ApiResponse<List<DataPipelineDto>>.SuccessResponse(dtos));
    }
    
    /// <summary>
    /// Gets a data pipeline by ID
    /// </summary>
    /// <param name="id">Pipeline ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Pipeline details</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<DataPipelineDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<DataPipelineDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPipelineById(Guid id, CancellationToken cancellationToken)
    {
        var pipeline = await _dataPipelineService.GetPipelineByIdAsync(id, cancellationToken);
        
        if (pipeline == null)
        {
            return NotFound(ApiResponse<DataPipelineDto>.ErrorResponse($"Pipeline with ID {id} not found"));
        }
        
        var dto = await MapToDtoAsync(pipeline, cancellationToken);
        
        return Ok(ApiResponse<DataPipelineDto>.SuccessResponse(dto));
    }
    
    /// <summary>
    /// Creates a new data pipeline
    /// </summary>
    /// <param name="dto">Pipeline creation details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created pipeline</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<DataPipelineDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<DataPipelineDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreatePipeline(
        [FromBody] CreateDataPipelineDto dto, 
        CancellationToken cancellationToken)
    {
        try
        {
            var schedule = MapToExecutionSchedule(dto.Schedule);
            var transformationRules = MapToTransformationRules(dto.TransformationRules);
            
            var pipeline = await _dataPipelineService.CreatePipelineAsync(
                dto.Name,
                dto.Description,
                dto.Type,
                dto.SourceId,
                schedule,
                transformationRules,
                dto.DestinationId,
                cancellationToken);
            
            var resultDto = await MapToDtoAsync(pipeline, cancellationToken);
            
            return CreatedAtAction(
                nameof(GetPipelineById), 
                new { id = resultDto.Id }, 
                ApiResponse<DataPipelineDto>.SuccessResponse(resultDto, "Pipeline created successfully"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<DataPipelineDto>.ErrorResponse(ex.Message));
        }
        catch (KeyNotFoundException ex)
        {
            return BadRequest(ApiResponse<DataPipelineDto>.ErrorResponse(ex.Message));
        }
    }
    
    /// <summary>
    /// Updates an existing data pipeline
    /// </summary>
    /// <param name="id">Pipeline ID</param>
    /// <param name="dto">Pipeline update details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content</returns>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<DataPipelineDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<DataPipelineDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdatePipeline(
        Guid id, 
        [FromBody] UpdateDataPipelineDto dto, 
        CancellationToken cancellationToken)
    {
        try
        {
            var pipeline = await _dataPipelineService.GetPipelineByIdAsync(id, cancellationToken);
            
            if (pipeline == null)
            {
                return NotFound(ApiResponse<DataPipelineDto>.ErrorResponse($"Pipeline with ID {id} not found"));
            }
            
            ExecutionSchedule? schedule = null;
            if (dto.Schedule != null)
            {
                schedule = MapToExecutionSchedule(dto.Schedule);
            }
            
            TransformationRules? transformationRules = null;
            if (dto.TransformationRules != null)
            {
                transformationRules = MapToTransformationRules(dto.TransformationRules);
            }
            
            await _dataPipelineService.UpdatePipelineAsync(
                id,
                dto.Description,
                schedule,
                transformationRules,
                cancellationToken);
            
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<DataPipelineDto>.ErrorResponse(ex.Message));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<DataPipelineDto>.ErrorResponse(ex.Message));
        }
    }
    
    /// <summary>
    /// Deletes a data pipeline
    /// </summary>
    /// <param name="id">Pipeline ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content</returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<DataPipelineDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePipeline(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _dataPipelineService.DeletePipelineAsync(id, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<DataPipelineDto>.ErrorResponse(ex.Message));
        }
    }
    
    /// <summary>
    /// Executes a data pipeline
    /// </summary>
    /// <param name="id">Pipeline ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Execution details</returns>
    [HttpPost("{id}/execute")]
    [ProducesResponseType(typeof(ApiResponse<PipelineExecutionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PipelineExecutionDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExecutePipeline(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var execution = await _dataPipelineService.ExecutePipelineAsync(id, cancellationToken);
            var dto = MapToExecutionDto(execution);
            
            return Ok(ApiResponse<PipelineExecutionDto>.SuccessResponse(dto, "Pipeline execution started"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<PipelineExecutionDto>.ErrorResponse(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<PipelineExecutionDto>.ErrorResponse(ex.Message));
        }
    }
    
    /// <summary>
    /// Pauses a running data pipeline
    /// </summary>
    /// <param name="id">Pipeline ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content</returns>
    [HttpPost("{id}/pause")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<DataPipelineDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PausePipeline(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _dataPipelineService.PausePipelineAsync(id, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<DataPipelineDto>.ErrorResponse(ex.Message));
        }
    }
    
    /// <summary>
    /// Resumes a paused data pipeline
    /// </summary>
    /// <param name="id">Pipeline ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content</returns>
    [HttpPost("{id}/resume")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<DataPipelineDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResumePipeline(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _dataPipelineService.ResumePipelineAsync(id, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<DataPipelineDto>.ErrorResponse(ex.Message));
        }
    }
    
    private async Task<DataPipelineDto> MapToDtoAsync(DataPipeline pipeline, CancellationToken cancellationToken)
    {
        var source = await _dataSourceService.GetDataSourceByIdAsync(pipeline.SourceId, cancellationToken);
        DataSourceDto? sourceDto = source != null ? MapToDataSourceDto(source) : null;
        
        DataSourceDto? destinationDto = null;
        if (pipeline.DestinationId.HasValue)
        {
            var destination = await _dataSourceService.GetDataSourceByIdAsync(pipeline.DestinationId.Value, cancellationToken);
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
    
    private static DataSourceDto MapToDataSourceDto(DataSource dataSource)
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
    
    private static PipelineExecutionDto MapToExecutionDto(PipelineExecution execution)
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
