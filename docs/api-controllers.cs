# API Controllers and DTOs

## src/DataProcessingService.API/Controllers/DataSourcesController.cs
```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.API.DTOs;
using DataProcessingService.Core.CQRS.Commands;
using DataProcessingService.Core.CQRS.Queries;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DataProcessingService.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DataSourcesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<DataSourcesController> _logger;
    
    public DataSourcesController(
        IMediator mediator,
        ILogger<DataSourcesController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }
    
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<DataSourceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetAllDataSourcesQuery(), cancellationToken);
        return Ok(result);
    }
    
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(DataSourceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetDataSourceByIdQuery(id), cancellationToken);
        
        if (result == null)
        {
            return NotFound();
        }
        
        return Ok(result);
    }
    
    [HttpPost]
    [ProducesResponseType(typeof(DataSourceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateDataSourceDto createDto, 
        CancellationToken cancellationToken)
    {
        var command = new CreateDataSourceCommand(
            createDto.Name,
            createDto.ConnectionString,
            createDto.Type,
            createDto.Schema,
            createDto.Properties);
        
        var result = await _mediator.Send(command, cancellationToken);
        
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }
    
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(
        Guid id, 
        [FromBody] UpdateDataSourceDto updateDto, 
        CancellationToken cancellationToken)
    {
        var command = new UpdateDataSourceCommand(
            id,
            updateDto.ConnectionString,
            updateDto.Schema,
            updateDto.IsActive,
            updateDto.PropertiesToAdd,
            updateDto.PropertiesToRemove);
        
        try
        {
            await _mediator.Send(command, cancellationToken);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        
        return NoContent();
    }
    
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _mediator.Send(new DeleteDataSourceCommand(id), cancellationToken);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        
        return NoContent();
    }
    
    [HttpPost("{id:guid}/test-connection")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TestConnection(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _mediator.Send(new TestDataSourceConnectionCommand(id), cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}

## src/DataProcessingService.API/Controllers/DataPipelinesController.cs
```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.API.DTOs;
using DataProcessingService.Core.CQRS.Commands;
using DataProcessingService.Core.CQRS.Queries;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DataProcessingService.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DataPipelinesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<DataPipelinesController> _logger;
    
    public DataPipelinesController(
        IMediator mediator,
        ILogger<DataPipelinesController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }
    
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<DataPipelineDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetAllDataPipelinesQuery(), cancellationToken);
        return Ok(result);
    }
    
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(DataPipelineDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetDataPipelineByIdQuery(id), cancellationToken);
        
        if (result == null)
        {
            return NotFound();
        }
        
        return Ok(result);
    }
    
    [HttpPost]
    [ProducesResponseType(typeof(DataPipelineDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateDataPipelineDto createDto, 
        CancellationToken cancellationToken)
    {
        var command = new CreateDataPipelineCommand(
            createDto.Name,
            createDto.Description,
            createDto.Type,
            createDto.SourceId,
            createDto.Schedule,
            createDto.TransformationRules,
            createDto.DestinationId);
        
        var result = await _mediator.Send(command, cancellationToken);
        
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }
    
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(
        Guid id, 
        [FromBody] UpdateDataPipelineDto updateDto, 
        CancellationToken cancellationToken)
    {
        var command = new UpdateDataPipelineCommand(
            id,
            updateDto.Description,
            updateDto.Schedule,
            updateDto.TransformationRules);
        
        try
        {
            await _mediator.Send(command, cancellationToken);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        
        return NoContent();
    }
    
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _mediator.Send(new DeleteDataPipelineCommand(id), cancellationToken);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        
        return NoContent();
    }
    
    [HttpPost("{id:guid}/execute")]
    [ProducesResponseType(typeof(PipelineExecutionDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Execute(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _mediator.Send(new ExecuteDataPipelineCommand(id), cancellationToken);
            return Accepted(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
    
    [HttpPost("{id:guid}/pause")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Pause(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _mediator.Send(new PauseDataPipelineCommand(id), cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
    
    [HttpPost("{id:guid}/resume")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Resume(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _mediator.Send(new ResumeDataPipelineCommand(id), cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}

## src/DataProcessingService.API/Controllers/PipelineExecutionsController.cs
```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.API.DTOs;
using DataProcessingService.Core.CQRS.Queries;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DataProcessingService.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PipelineExecutionsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<PipelineExecutionsController> _logger;
    
    public PipelineExecutionsController(
        IMediator mediator,
        ILogger<PipelineExecutionsController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }
    
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PipelineExecutionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetPipelineExecutionByIdQuery(id), cancellationToken);
        
        if (result == null)
        {
            return NotFound();
        }
        
        return Ok(result);
    }
    
    [HttpGet("pipeline/{pipelineId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<PipelineExecutionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByPipelineId(
        Guid pipelineId, 
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new GetPipelineExecutionsByPipelineIdQuery(pipelineId), 
            cancellationToken);
        
        return Ok(result);
    }
    
    [HttpGet("status/{status}")]
    [ProducesResponseType(typeof(IEnumerable<PipelineExecutionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByStatus(
        string status, 
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<Core.Domain.Enums.ExecutionStatus>(status, true, out var executionStatus))
        {
            return BadRequest($"Invalid status: {status}");
        }
        
        var result = await _mediator.Send(
            new GetPipelineExecutionsByStatusQuery(executionStatus), 
            cancellationToken);
        
        return Ok(result);
    }
    
    [HttpGet("date-range")]
    [ProducesResponseType(typeof(IEnumerable<PipelineExecutionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByDateRange(
        [FromQuery] DateTimeOffset start,
        [FromQuery] DateTimeOffset end,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new GetPipelineExecutionsInDateRangeQuery(start, end), 
            cancellationToken);
        
        return Ok(result);
    }
    
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(Dictionary<string, int>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatistics(
        [FromQuery] DateTimeOffset? start = null,
        [FromQuery] DateTimeOffset? end = null,
        CancellationToken cancellationToken = default)
    {
        var startDate = start ?? DateTimeOffset.UtcNow.AddDays(-30);
        var endDate = end ?? DateTimeOffset.UtcNow;
        
        var result = await _mediator.Send(
            new GetPipelineExecutionStatisticsQuery(startDate, endDate), 
            cancellationToken);
        
        return Ok(result);
    }
}

## src/DataProcessingService.API/DTOs/DataSourceDtos.cs
```csharp
using System;
using System.Collections.Generic;
using DataProcessingService.Core.Domain.Enums;

namespace DataProcessingService.API.DTOs;

public class DataSourceDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string ConnectionString { get; set; } = null!;
    public DataSourceType Type { get; set; }
    public string? Schema { get; set; }
    public Dictionary<string, string> Properties { get; set; } = new();
    public bool IsActive { get; set; }
    public DateTimeOffset LastSyncTime { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTimeOffset? LastModifiedAt { get; set; }
    public string? LastModifiedBy { get; set; }
}

public class CreateDataSourceDto
{
    public string Name { get; set; } = null!;
    public string ConnectionString { get; set; } = null!;
    public DataSourceType Type { get; set; }
    public string? Schema { get; set; }
    public Dictionary<string, string>? Properties { get; set; }
}

public class UpdateDataSourceDto
{
    public string? ConnectionString { get; set; }
    public string? Schema { get; set; }
    public bool? IsActive { get; set; }
    public Dictionary<string, string>? PropertiesToAdd { get; set; }
    public List<string>? PropertiesToRemove { get; set; }
}

## src/DataProcessingService.API/DTOs/DataPipelineDtos.cs
```csharp
using System;
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
    public string SourceName { get; set; } = null!;
    public Guid? DestinationId { get; set; }
    public string? DestinationName { get; set; }
    public ExecutionSchedule Schedule { get; set; } = null!;
    public TransformationRules TransformationRules { get; set; } = null!;
    public DateTimeOffset? LastExecutionTime { get; set; }
    public DateTimeOffset? NextExecutionTime { get; set; }
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
    public ExecutionSchedule Schedule { get; set; } = null!;
    public TransformationRules TransformationRules { get; set; } = null!;
}

public class UpdateDataPipelineDto
{
    public string? Description { get; set; }
    public ExecutionSchedule? Schedule { get; set; }
    public TransformationRules? TransformationRules { get; set; }
}

## src/DataProcessingService.API/DTOs/PipelineExecutionDtos.cs
```csharp
using System;
using System.Collections.Generic;
using DataProcessingService.Core.Domain.Enums;

namespace DataProcessingService.API.DTOs;

public class PipelineExecutionDto
{
    public Guid Id { get; set; }
    public Guid PipelineId { get; set; }
    public string PipelineName { get; set; } = null!;
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset? EndTime { get; set; }
    public ExecutionStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public int ProcessedRecords { get; set; }
    public int FailedRecords { get; set; }
    public List<ExecutionMetricDto> Metrics { get; set; } = new();
}

public class ExecutionMetricDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Value { get; set; } = null!;
    public MetricType Type { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}

## src/DataProcessingService.Core/CQRS/Queries/DataSource/GetAllDataSourcesQuery.cs
```csharp
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.API.DTOs;
using DataProcessingService.Core.Interfaces.Repositories;
using MediatR;

namespace DataProcessingService.Core.CQRS.Queries;

public record GetAllDataSourcesQuery() : IQuery<IEnumerable<DataSourceDto>>;

public class GetAllDataSourcesQueryHandler : IQueryHandler<GetAllDataSourcesQuery, IEnumerable<DataSourceDto>>
{
    private readonly IDataSourceRepository _dataSourceRepository;
    
    public GetAllDataSourcesQueryHandler(IDataSourceRepository dataSourceRepository)
    {
        _dataSourceRepository = dataSourceRepository;
    }
    
    public async Task<IEnumerable<DataSourceDto>> Handle(
        GetAllDataSourcesQuery request, 
        CancellationToken cancellationToken)
    {
        var dataSources = await _dataSourceRepository.GetAllAsync(cancellationToken);
        
        return dataSources.Select(ds => new DataSourceDto
        {
            Id = ds.Id,
            Name = ds.Name,
            ConnectionString = ds.ConnectionString,
            Type = ds.Type,
            Schema = ds.Schema,
            Properties = ds.Properties,
            IsActive = ds.IsActive,
            LastSyncTime = ds.LastSyncTime,
            CreatedAt = ds.CreatedAt,
            CreatedBy = ds.CreatedBy,
            LastModifiedAt = ds.LastModifiedAt,
            LastModifiedBy = ds.LastModifiedBy
        });
    }
}

## src/DataProcessingService.Core/CQRS/Queries/DataSource/GetDataSourceByIdQuery.cs
```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.API.DTOs;
using DataProcessingService.Core.Interfaces.Repositories;
using MediatR;

namespace DataProcessingService.Core.CQRS.Queries;

public record GetDataSourceByIdQuery(Guid Id) : IQuery<DataSourceDto?>;

public class GetDataSourceByIdQueryHandler : IQueryHandler<GetDataSourceByIdQuery, DataSourceDto?>
{
    private readonly IDataSourceRepository _dataSourceRepository;
    
    public GetDataSourceByIdQueryHandler(IDataSourceRepository dataSourceRepository)
    {
        _dataSourceRepository = dataSourceRepository;
    }
    
    public async Task<DataSourceDto?> Handle(
        GetDataSourceByIdQuery request, 
        CancellationToken cancellationToken)
    {
        var dataSource = await _dataSourceRepository.GetByIdAsync(request.Id, cancellationToken);
        
        if (dataSource == null)
        {
            return null;
        }
        
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
}

## src/DataProcessingService.Core/CQRS/Commands/DataSource/CreateDataSourceCommand.cs
```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.API.DTOs;
using DataProcessingService.Core.Domain.Entities;
using DataProcessingService.Core.Domain.Enums;
using DataProcessingService.Core.Interfaces.Services;
using MediatR;

namespace DataProcessingService.Core.CQRS.Commands;

public record CreateDataSourceCommand(
    string Name,
    string ConnectionString,
    DataSourceType Type,
    string? Schema,
    Dictionary<string, string>? Properties) : ICommand<DataSourceDto>;

public class CreateDataSourceCommandHandler : ICommandHandler<CreateDataSourceCommand, DataSourceDto>
{
    private readonly IDataSourceService _dataSourceService;
    
    public CreateDataSourceCommandHandler(IDataSourceService dataSourceService)
    {
        _dataSourceService = dataSourceService;
    }
    
    public async Task<DataSourceDto> Handle(
        CreateDataSourceCommand request, 
        CancellationToken cancellationToken)
    {
        var dataSource = await _dataSourceService.CreateDataSourceAsync(
            request.Name,
            request.ConnectionString,
            request.Type,
            request.Schema,
            request.Properties,
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
}

## src/DataProcessingService.Core/CQRS/Commands/DataSource/UpdateDataSourceCommand.cs
```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.Core.Interfaces.Services;
using MediatR;

namespace DataProcessingService.Core.CQRS.Commands;

public record UpdateDataSourceCommand(
    Guid Id,
    string? ConnectionString,
    string? Schema,
    bool? IsActive,
    Dictionary<string, string>? PropertiesToAdd,
    IEnumerable<string>? PropertiesToRemove) : ICommand<Unit>;

public class UpdateDataSourceCommandHandler : ICommandHandler<UpdateDataSourceCommand, Unit>
{
    private readonly IDataSourceService _dataSourceService;
    
    public UpdateDataSourceCommandHandler(IDataSourceService dataSourceService)
    {
        _dataSourceService = dataSourceService;
    }
    
    public async Task<Unit> Handle(
        UpdateDataSourceCommand request, 
        CancellationToken cancellationToken)
    {
        await _dataSourceService.UpdateDataSourceAsync(
            request.Id,
            request.ConnectionString,
            request.Schema,
            request.IsActive,
            request.PropertiesToAdd,
            request.PropertiesToRemove,
            cancellationToken);
        
        return Unit.Value;
    }
}

## src/DataProcessingService.Core/CQRS/Commands/DataSource/DeleteDataSourceCommand.cs
```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.Core.Interfaces.Services;
using MediatR;

namespace DataProcessingService.Core.CQRS.Commands;

public record DeleteDataSourceCommand(Guid Id) : ICommand<Unit>;

public class DeleteDataSourceCommandHandler : ICommandHandler<DeleteDataSourceCommand, Unit>
{
    private readonly IDataSourceService _dataSourceService;
    
    public DeleteDataSourceCommandHandler(IDataSourceService dataSourceService)
    {
        _dataSourceService = dataSourceService;
    }
    
    public async Task<Unit> Handle(
        DeleteDataSourceCommand request, 
        CancellationToken cancellationToken)
    {
        await _dataSourceService.DeleteDataSourceAsync(request.Id, cancellationToken);
        return Unit.Value;
    }
}

## src/DataProcessingService.Core/CQRS/Commands/DataSource/TestDataSourceConnectionCommand.cs
```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.Core.Interfaces.Services;
using MediatR;

namespace DataProcessingService.Core.CQRS.Commands;

public record TestDataSourceConnectionCommand(Guid Id) : ICommand<bool>;

public class TestDataSourceConnectionCommandHandler : ICommandHandler<TestDataSourceConnectionCommand, bool>
{
    private readonly IDataSourceService _dataSourceService;
    
    public TestDataSourceConnectionCommandHandler(IDataSourceService dataSourceService)
    {
        _dataSourceService = dataSourceService;
    }
    
    public async Task<bool> Handle(
        TestDataSourceConnectionCommand request, 
        CancellationToken cancellationToken)
    {
        return await _dataSourceService.TestConnectionAsync(request.Id, cancellationToken);
    }
}

## src/DataProcessingService.Core/CQRS/Queries/DataPipeline/GetAllDataPipelinesQuery.cs
```csharp
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.API.DTOs;
using DataProcessingService.Core.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;
using MediatR;

namespace DataProcessingService.Core.CQRS.Queries;

public record GetAllDataPipelinesQuery() : IQuery<IEnumerable<DataPipelineDto>>;

public class GetAllDataPipelinesQueryHandler : IQueryHandler<GetAllDataPipelinesQuery, IEnumerable<DataPipelineDto>>
{
    private readonly IDataPipelineRepository _dataPipelineRepository;
    
    public GetAllDataPipelinesQueryHandler(IDataPipelineRepository dataPipelineRepository)
    {
        _dataPipelineRepository = dataPipelineRepository;
    }
    
    public async Task<IEnumerable<DataPipelineDto>> Handle(
        GetAllDataPipelinesQuery request, 
        CancellationToken cancellationToken)
    {
        var dataPipelines = await _dataPipelineRepository.GetAllAsync(cancellationToken);
        
        // Load related data (in a real app, this would use more efficient queries)
        var result = new List<DataPipelineDto>();
        
        foreach (var pipeline in dataPipelines)
        {
            var pipelineWithRelated = await _dataPipelineRepository.GetWithRelatedDataAsync(
                pipeline.Id, cancellationToken);
            
            if (pipelineWithRelated != null)
            {
                result.Add(new DataPipelineDto
                {
                    Id = pipelineWithRelated.Id,
                    Name = pipelineWithRelated.Name,
                    Description = pipelineWithRelated.Description,
                    Type = pipelineWithRelated.Type,
                    Status = pipelineWithRelated.Status,
                    SourceId = pipelineWithRelated.SourceId,
                    SourceName = pipelineWithRelated.Source.Name,
                    DestinationId = pipelineWithRelated.DestinationId,
                    DestinationName = pipelineWithRelated.Destination?.Name,
                    Schedule = pipelineWithRelated.Schedule,
                    TransformationRules = pipelineWithRelated.TransformationRules,
                    LastExecutionTime = pipelineWithRelated.LastExecutionTime,
                    NextExecutionTime = pipelineWithRelated.NextExecutionTime,
                    CreatedAt = pipelineWithRelated.CreatedAt,
                    CreatedBy = pipelineWithRelated.CreatedBy,
                    LastModifiedAt = pipelineWithRelated.LastModifiedAt,
                    LastModifiedBy = pipelineWithRelated.LastModifiedBy
                });
            }
        }
        
        return result;
    }
}

## src/DataProcessingService.Core/CQRS/Queries/DataPipeline/GetDataPipelineByIdQuery.cs
```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.API.DTOs;
using DataProcessingService.Core.Interfaces.Repositories;
using MediatR;

namespace DataProcessingService.Core.CQRS.Queries;

public record GetDataPipelineByIdQuery(Guid Id) : IQuery<DataPipelineDto?>;

public class GetDataPipelineByIdQueryHandler : IQueryHandler<GetDataPipelineByIdQuery, DataPipelineDto?>
{
    private readonly IDataPipelineRepository _dataPipelineRepository;
    
    public GetDataPipelineByIdQueryHandler(IDataPipelineRepository dataPipelineRepository)
    {
        _dataPipelineRepository = dataPipelineRepository;
    }
    
    public async Task<DataPipelineDto?> Handle(
        GetDataPipelineByIdQuery request, 
        CancellationToken cancellationToken)
    {
        var pipeline = await _dataPipelineRepository.GetWithRelatedDataAsync(
            request.Id, cancellationToken);
        
        if (pipeline == null)
        {
            return null;
        }
        
        return new DataPipelineDto
        {
            Id = pipeline.Id,
            Name = pipeline.Name,
            Description = pipeline.Description,
            Type = pipeline.Type,
            Status = pipeline.Status,
            SourceId = pipeline.SourceId,
            SourceName = pipeline.Source.Name,
            DestinationId = pipeline.DestinationId,
            DestinationName = pipeline.Destination?.Name,
            Schedule = pipeline.Schedule,
            TransformationRules = pipeline.TransformationRules,
            LastExecutionTime = pipeline.LastExecutionTime,
            NextExecutionTime = pipeline.NextExecutionTime,
            CreatedAt = pipeline.CreatedAt,
            CreatedBy = pipeline.CreatedBy,
            LastModifiedAt = pipeline.LastModifiedAt,
            LastModifiedBy = pipeline.LastModifiedBy
        };
    }
}

## src/DataProcessingService.Core/CQRS/Commands/DataPipeline/CreateDataPipelineCommand.cs
```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.API.DTOs;
using DataProcessingService.Core.Domain.Enums;
using DataProcessingService.Core.Domain.ValueObjects;
using DataProcessingService.Core.Interfaces.Repositories;
using DataProcessingService.Core.Interfaces.Services;
using MediatR;

namespace DataProcessingService.Core.CQRS.Commands;

public record CreateDataPipelineCommand(
    string Name,
    string Description,
    PipelineType Type,
    Guid SourceId,
    ExecutionSchedule Schedule,
    TransformationRules TransformationRules,
    Guid? DestinationId) : ICommand<DataPipelineDto>;

public class CreateDataPipelineCommandHandler : ICommandHandler<CreateDataPipelineCommand, DataPipelineDto>
{
    private readonly IDataPipelineService _dataPipelineService;
    private readonly IDataPipelineRepository _dataPipelineRepository;
    
    public CreateDataPipelineCommandHandler(
        IDataPipelineService dataPipelineService,
        IDataPipelineRepository dataPipelineRepository)
    {
        _dataPipelineService = dataPipelineService;
        _dataPipelineRepository = dataPipelineRepository;
    }
    
    public async Task<DataPipelineDto> Handle(
        CreateDataPipelineCommand request, 
        CancellationToken cancellationToken)
    {
        var pipeline = await _dataPipelineService.CreatePipelineAsync(
            request.Name,
            request.Description,
            request.Type,
            request.SourceId,
            request.Schedule,
            request.TransformationRules,
            request.DestinationId,
            cancellationToken);
        
        // Get full data with relations
        var pipelineWithRelated = await _dataPipelineRepository.GetWithRelatedDataAsync(
            pipeline.Id, cancellationToken);
        
        if (pipelineWithRelated == null)
        {
            throw new InvalidOperationException($"Created pipeline with ID {pipeline.Id} could not be retrieved");
        }
        
        return new DataPipelineDto
        {
            Id = pipelineWithRelated.Id,
            Name = pipelineWithRelated.Name,
            Description = pipelineWithRelated.Description,
            Type = pipelineWithRelated.Type,
            Status = pipelineWithRelated.Status,
            SourceId = pipelineWithRelated.SourceId,
            SourceName = pipelineWithRelated.Source.Name,
            DestinationId = pipelineWithRelated.DestinationId,
            DestinationName = pipelineWithRelated.Destination?.Name,
            Schedule = pipelineWithRelated.Schedule,
            TransformationRules = pipelineWithRelated.TransformationRules,
            LastExecutionTime = pipelineWithRelated.LastExecutionTime,
            NextExecutionTime = pipelineWithRelated.NextExecutionTime,
            CreatedAt = pipelineWithRelated.CreatedAt,
            CreatedBy = pipelineWithRelated.CreatedBy,
            LastModifiedAt = pipelineWithRelated.LastModifiedAt,
            LastModifiedBy = pipelineWithRelated.LastModifiedBy
        };
    }
}

## src/DataProcessingService.Core/CQRS/Commands/DataPipeline/UpdateDataPipelineCommand.cs
```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.Core.Domain.ValueObjects;
using DataProcessingService.Core.Interfaces.Services;
using MediatR;

namespace DataProcessingService.Core.CQRS.Commands;

public record UpdateDataPipelineCommand(
    Guid Id,
    string? Description,
    ExecutionSchedule? Schedule,
    TransformationRules? TransformationRules) : ICommand<Unit>;

public class UpdateDataPipelineCommandHandler : ICommandHandler<UpdateDataPipelineCommand, Unit>
{
    private readonly IDataPipelineService _dataPipelineService;
    
    public UpdateDataPipelineCommandHandler(IDataPipelineService dataPipelineService)
    {
        _dataPipelineService = dataPipelineService;
    }
    
    public async Task<Unit> Handle(
        UpdateDataPipelineCommand request, 
        CancellationToken cancellationToken)
    {
        await _dataPipelineService.UpdatePipelineAsync(
            request.Id,
            request.Description,
            request.Schedule,
            request.TransformationRules,
            cancellationToken);
        
        return Unit.Value;
    }
}

## src/DataProcessingService.Core/CQRS/Commands/DataPipeline/DeleteDataPipelineCommand.cs
```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.Core.Interfaces.Services;
using MediatR;

namespace DataProcessingService.Core.CQRS.Commands;

public record DeleteDataPipelineCommand(Guid Id) : ICommand<Unit>;

public class DeleteDataPipelineCommandHandler : ICommandHandler<DeleteDataPipelineCommand, Unit>
{
    private readonly IDataPipelineService _dataPipelineService;
    
    public DeleteDataPipelineCommandHandler(IDataPipelineService dataPipelineService)
    {
        _dataPipelineService = dataPipelineService;
    }
    
    public async Task<Unit> Handle(
        DeleteDataPipelineCommand request, 
        CancellationToken cancellationToken)
    {
        await _dataPipelineService.DeletePipelineAsync(request.Id, cancellationToken);
        return Unit.Value;
    }
}

## src/DataProcessingService.Core/CQRS/Commands/DataPipeline/ExecuteDataPipelineCommand.cs
```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.API.DTOs;
using DataProcessingService.Core.Interfaces.Services;
using MediatR;

namespace DataProcessingService.Core.CQRS.Commands;

public record ExecuteDataPipelineCommand(Guid Id) : ICommand<PipelineExecutionDto>;

public class ExecuteDataPipelineCommandHandler : ICommandHandler<ExecuteDataPipelineCommand, PipelineExecutionDto>
{
    private readonly IDataPipelineService _dataPipelineService;
    private readonly IDataPipelineRepository _dataPipelineRepository;
    
    public ExecuteDataPipelineCommandHandler(
        IDataPipelineService dataPipelineService,
        IDataPipelineRepository dataPipelineRepository)
    {
        _dataPipelineService = dataPipelineService;
        _dataPipelineRepository = dataPipelineRepository;
    }
    
    public async Task<PipelineExecutionDto> Handle(
        ExecuteDataPipelineCommand request, 
        CancellationToken cancellationToken)
    {
        var execution = await _dataPipelineService.ExecutePipelineAsync(request.Id, cancellationToken);
        var pipeline = await _dataPipelineRepository.GetByIdAsync(request.Id, cancellationToken);
        
        if (pipeline == null)
        {
            throw new KeyNotFoundException($"Pipeline with ID {request.Id} not found");
        }
        
        return new PipelineExecutionDto
        {
            Id = execution.Id,
            PipelineId = execution.PipelineId,
            PipelineName = pipeline.Name,
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

## src/DataProcessingService.Core/CQRS/Commands/DataPipeline/PauseDataPipelineCommand.cs
```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.Core.Interfaces.Services;
using MediatR;

namespace DataProcessingService.Core.CQRS.Commands;

public record PauseDataPipelineCommand(Guid Id) : ICommand<Unit>;

public class PauseDataPipelineCommandHandler : ICommandHandler<PauseDataPipelineCommand, Unit>
{
    private readonly IDataPipelineService _dataPipelineService;
    
    public PauseDataPipelineCommandHandler(IDataPipelineService dataPipelineService)
    {
        _dataPipelineService = dataPipelineService;
    }
    
    public async Task<Unit> Handle(
        PauseDataPipelineCommand request, 
        CancellationToken cancellationToken)
    {
        await _dataPipelineService.PausePipelineAsync(request.Id, cancellationToken);
        return Unit.Value;
    }
}

## src/DataProcessingService.Core/CQRS/Commands/DataPipeline/ResumeDataPipelineCommand.cs
```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.Core.Interfaces.Services;
using MediatR;

namespace DataProcessingService.Core.CQRS.Commands;

public record ResumeDataPipelineCommand(Guid Id) : ICommand<Unit>;

public class ResumeDataPipelineCommandHandler : ICommandHandler<ResumeDataPipelineCommand, Unit>
{
    private readonly IDataPipelineService _dataPipelineService;
    
    public ResumeDataPipelineCommandHandler(IDataPipelineService dataPipelineService)
    {
        _dataPipelineService = dataPipelineService;
    }
    
    public async Task<Unit> Handle(
        ResumeDataPipelineCommand request, 
        CancellationToken cancellationToken)
    {
        await _dataPipelineService.ResumePipelineAsync(request.Id, cancellationToken);
        return Unit.Value;
    }
}

## src/DataProcessingService.Core/CQRS/Queries/PipelineExecution/GetPipelineExecutionByIdQuery.cs
```csharp
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.API.DTOs;
using DataProcessingService.Core.Interfaces.Repositories;
using MediatR;

namespace DataProcessingService.Core.CQRS.Queries;

public record GetPipelineExecutionByIdQuery(Guid Id) : IQuery<PipelineExecutionDto?>;

public class GetPipelineExecutionByIdQueryHandler : IQueryHandler<GetPipelineExecutionByIdQuery, PipelineExecutionDto?>
{
    private readonly IPipelineExecutionRepository _pipelineExecutionRepository;
    private readonly IDataPipelineRepository _dataPipelineRepository;
    
    public GetPipelineExecutionByIdQueryHandler(
        IPipelineExecutionRepository pipelineExecutionRepository,
        IDataPipelineRepository dataPipelineRepository)
    {
        _pipelineExecutionRepository = pipelineExecutionRepository;
        _dataPipelineRepository = dataPipelineRepository;
    }
    
    public async Task<PipelineExecutionDto?> Handle(
        GetPipelineExecutionByIdQuery request, 
        CancellationToken cancellationToken)
    {
        var execution = await _pipelineExecutionRepository.GetByIdAsync(request.Id, cancellationToken);
        
        if (execution == null)
        {
            return null;
        }
        
        var pipeline = await _dataPipelineRepository.GetByIdAsync(execution.PipelineId, cancellationToken);
        
        if (pipeline == null)
        {
            throw new InvalidOperationException($"Pipeline with ID {execution.PipelineId} not found");
        }
        
        return new PipelineExecutionDto
        {
            Id = execution.Id,
            PipelineId = execution.PipelineId,
            PipelineName = pipeline.Name,
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

## src/DataProcessingService.Core/CQRS/Queries/PipelineExecution/GetPipelineExecutionsByPipelineIdQuery.cs
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.API.DTOs;
using DataProcessingService.Core.Interfaces.Repositories;
using MediatR;

namespace DataProcessingService.Core.CQRS.Queries;

public record GetPipelineExecutionsByPipelineIdQuery(Guid PipelineId) : IQuery<IEnumerable<PipelineExecutionDto>>;

public class GetPipelineExecutionsByPipelineIdQueryHandler : 
    IQueryHandler<GetPipelineExecutionsByPipelineIdQuery, IEnumerable<PipelineExecutionDto>>
{
    private readonly IPipelineExecutionRepository _pipelineExecutionRepository;
    private readonly IDataPipelineRepository _dataPipelineRepository;
    
    public GetPipelineExecutionsByPipelineIdQueryHandler(
        IPipelineExecutionRepository pipelineExecutionRepository,
        IDataPipelineRepository dataPipelineRepository)
    {
        _pipelineExecutionRepository = pipelineExecutionRepository;
        _dataPipelineRepository = dataPipelineRepository;
    }
    
    public async Task<IEnumerable<PipelineExecutionDto>> Handle(
        GetPipelineExecutionsByPipelineIdQuery request, 
        CancellationToken cancellationToken)
    {
        var executions = await _pipelineExecutionRepository.GetExecutionsByPipelineIdAsync(
            request.PipelineId, cancellationToken);
        
        if (!executions.Any())
        {
            return Enumerable.Empty<PipelineExecutionDto>();
        }
        
        var pipeline = await _dataPipelineRepository.GetByIdAsync(request.PipelineId, cancellationToken);
        
        if (pipeline == null)
        {
            throw new InvalidOperationException($"Pipeline with ID {request.PipelineId} not found");
        }
        
        return executions.Select(e => new PipelineExecutionDto
        {
            Id = e.Id,
            PipelineId = e.PipelineId,
            PipelineName = pipeline.Name,
            StartTime = e.StartTime,
            EndTime = e.EndTime,
            Status = e.Status,
            ErrorMessage = e.ErrorMessage,
            ProcessedRecords = e.ProcessedRecords,
            FailedRecords = e.FailedRecords,
            Metrics = e.Metrics.Select(m => new ExecutionMetricDto
            {
                Id = m.Id,
                Name = m.Name,
                Value = m.Value,
                Type = m.Type,
                Timestamp = m.Timestamp
            }).ToList()
        });
    }
}
