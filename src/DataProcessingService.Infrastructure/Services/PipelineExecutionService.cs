using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using DataProcessingService.Core.Domain.Entities;
using DataProcessingService.Core.Domain.Enums;
using DataProcessingService.Core.Interfaces.Repositories;
using DataProcessingService.Core.Interfaces.Services;

namespace DataProcessingService.Infrastructure.Services;

public class PipelineExecutionService : IPipelineExecutionService
{
    private readonly IPipelineExecutionRepository _pipelineExecutionRepository;
    private readonly IDataPipelineRepository _dataPipelineRepository;
    private readonly ILogger<PipelineExecutionService> _logger;
    
    public PipelineExecutionService(
        IPipelineExecutionRepository pipelineExecutionRepository,
        IDataPipelineRepository dataPipelineRepository,
        ILogger<PipelineExecutionService> logger)
    {
        _pipelineExecutionRepository = pipelineExecutionRepository;
        _dataPipelineRepository = dataPipelineRepository;
        _logger = logger;
    }
    
    public async Task<PipelineExecution?> GetExecutionByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _pipelineExecutionRepository.GetByIdAsync(id, cancellationToken);
    }
    
    public async Task<IReadOnlyList<PipelineExecution>> GetExecutionsByPipelineIdAsync(
        Guid pipelineId, 
        CancellationToken cancellationToken = default)
    {
        return await _pipelineExecutionRepository.GetExecutionsByPipelineIdAsync(pipelineId, cancellationToken);
    }
    
    public async Task<IReadOnlyList<PipelineExecution>> GetExecutionsByStatusAsync(
        ExecutionStatus status, 
        CancellationToken cancellationToken = default)
    {
        return await _pipelineExecutionRepository.GetExecutionsByStatusAsync(status, cancellationToken);
    }
    
    public async Task<PipelineExecution?> GetLatestExecutionByPipelineIdAsync(
        Guid pipelineId, 
        CancellationToken cancellationToken = default)
    {
        return await _pipelineExecutionRepository.GetLatestExecutionByPipelineIdAsync(pipelineId, cancellationToken);
    }
    
    public async Task<IReadOnlyList<PipelineExecution>> GetExecutionsInDateRangeAsync(
        DateTimeOffset start, 
        DateTimeOffset end, 
        CancellationToken cancellationToken = default)
    {
        return await _pipelineExecutionRepository.GetExecutionsInDateRangeAsync(start, end, cancellationToken);
    }
    
    public async Task<Dictionary<string, int>> GetExecutionStatisticsAsync(
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken = default)
    {
        var executions = await _pipelineExecutionRepository.GetExecutionsInDateRangeAsync(start, end, cancellationToken);
        
        var statistics = new Dictionary<string, int>
        {
            ["TotalExecutions"] = executions.Count,
            ["CompletedExecutions"] = 0,
            ["FailedExecutions"] = 0,
            ["RunningExecutions"] = 0,
            ["CanceledExecutions"] = 0,
            ["TotalProcessedRecords"] = 0,
            ["TotalFailedRecords"] = 0
        };
        
        foreach (var execution in executions)
        {
            switch (execution.Status)
            {
                case ExecutionStatus.Completed:
                    statistics["CompletedExecutions"]++;
                    break;
                case ExecutionStatus.Failed:
                    statistics["FailedExecutions"]++;
                    break;
                case ExecutionStatus.Running:
                    statistics["RunningExecutions"]++;
                    break;
                case ExecutionStatus.Canceled:
                    statistics["CanceledExecutions"]++;
                    break;
            }
            
            statistics["TotalProcessedRecords"] += execution.ProcessedRecords;
            statistics["TotalFailedRecords"] += execution.FailedRecords;
        }
        
        return statistics;
    }
}
