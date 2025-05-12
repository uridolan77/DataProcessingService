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
using DataProcessingService.Core.Domain.Enums;
using DataProcessingService.Core.Interfaces.Services;

namespace DataProcessingService.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class PipelineExecutionsController : ControllerBase
{
    private readonly IPipelineExecutionService _pipelineExecutionService;
    private readonly ILogger<PipelineExecutionsController> _logger;
    
    public PipelineExecutionsController(
        IPipelineExecutionService pipelineExecutionService,
        ILogger<PipelineExecutionsController> logger)
    {
        _pipelineExecutionService = pipelineExecutionService;
        _logger = logger;
    }
    
    /// <summary>
    /// Gets a pipeline execution by ID
    /// </summary>
    /// <param name="id">Execution ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Execution details</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<PipelineExecutionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PipelineExecutionDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetExecutionById(Guid id, CancellationToken cancellationToken)
    {
        var execution = await _pipelineExecutionService.GetExecutionByIdAsync(id, cancellationToken);
        
        if (execution == null)
        {
            return NotFound(ApiResponse<PipelineExecutionDto>.ErrorResponse($"Execution with ID {id} not found"));
        }
        
        return Ok(ApiResponse<PipelineExecutionDto>.SuccessResponse(MapToDto(execution)));
    }
    
    /// <summary>
    /// Gets executions for a specific pipeline
    /// </summary>
    /// <param name="pipelineId">Pipeline ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of executions</returns>
    [HttpGet("by-pipeline/{pipelineId}")]
    [ProducesResponseType(typeof(ApiResponse<List<PipelineExecutionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExecutionsByPipelineId(Guid pipelineId, CancellationToken cancellationToken)
    {
        var executions = await _pipelineExecutionService.GetExecutionsByPipelineIdAsync(pipelineId, cancellationToken);
        var dtos = executions.Select(MapToDto).ToList();
        
        return Ok(ApiResponse<List<PipelineExecutionDto>>.SuccessResponse(dtos));
    }
    
    /// <summary>
    /// Gets the latest execution for a specific pipeline
    /// </summary>
    /// <param name="pipelineId">Pipeline ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Latest execution details</returns>
    [HttpGet("latest-by-pipeline/{pipelineId}")]
    [ProducesResponseType(typeof(ApiResponse<PipelineExecutionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PipelineExecutionDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLatestExecutionByPipelineId(Guid pipelineId, CancellationToken cancellationToken)
    {
        var execution = await _pipelineExecutionService.GetLatestExecutionByPipelineIdAsync(pipelineId, cancellationToken);
        
        if (execution == null)
        {
            return NotFound(ApiResponse<PipelineExecutionDto>.ErrorResponse($"No executions found for pipeline with ID {pipelineId}"));
        }
        
        return Ok(ApiResponse<PipelineExecutionDto>.SuccessResponse(MapToDto(execution)));
    }
    
    /// <summary>
    /// Gets executions by status
    /// </summary>
    /// <param name="status">Execution status</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of executions</returns>
    [HttpGet("by-status/{status}")]
    [ProducesResponseType(typeof(ApiResponse<List<PipelineExecutionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExecutionsByStatus(ExecutionStatus status, CancellationToken cancellationToken)
    {
        var executions = await _pipelineExecutionService.GetExecutionsByStatusAsync(status, cancellationToken);
        var dtos = executions.Select(MapToDto).ToList();
        
        return Ok(ApiResponse<List<PipelineExecutionDto>>.SuccessResponse(dtos));
    }
    
    /// <summary>
    /// Gets executions in a date range
    /// </summary>
    /// <param name="startDate">Start date (ISO 8601 format)</param>
    /// <param name="endDate">End date (ISO 8601 format)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of executions</returns>
    [HttpGet("by-date-range")]
    [ProducesResponseType(typeof(ApiResponse<List<PipelineExecutionDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<PipelineExecutionDto>>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetExecutionsInDateRange(
        [FromQuery] DateTimeOffset startDate, 
        [FromQuery] DateTimeOffset endDate, 
        CancellationToken cancellationToken)
    {
        if (startDate > endDate)
        {
            return BadRequest(ApiResponse<List<PipelineExecutionDto>>.ErrorResponse("Start date must be before end date"));
        }
        
        var executions = await _pipelineExecutionService.GetExecutionsInDateRangeAsync(startDate, endDate, cancellationToken);
        var dtos = executions.Select(MapToDto).ToList();
        
        return Ok(ApiResponse<List<PipelineExecutionDto>>.SuccessResponse(dtos));
    }
    
    /// <summary>
    /// Gets execution statistics for a date range
    /// </summary>
    /// <param name="startDate">Start date (ISO 8601 format)</param>
    /// <param name="endDate">End date (ISO 8601 format)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Execution statistics</returns>
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(ApiResponse<ExecutionStatisticsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ExecutionStatisticsDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetExecutionStatistics(
        [FromQuery] DateTimeOffset startDate, 
        [FromQuery] DateTimeOffset endDate, 
        CancellationToken cancellationToken)
    {
        if (startDate > endDate)
        {
            return BadRequest(ApiResponse<ExecutionStatisticsDto>.ErrorResponse("Start date must be before end date"));
        }
        
        var statistics = await _pipelineExecutionService.GetExecutionStatisticsAsync(startDate, endDate, cancellationToken);
        
        var dto = new ExecutionStatisticsDto
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
        
        return Ok(ApiResponse<ExecutionStatisticsDto>.SuccessResponse(dto));
    }
    
    private static PipelineExecutionDto MapToDto(PipelineExecution execution)
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
