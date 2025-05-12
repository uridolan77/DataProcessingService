/*
 * DataLoader.Microservice.sln
 * 
 * Solution structure:
 * - DataLoader.API (Main API project)
 * - DataLoader.Core (Core business logic)
 * - DataLoader.Infrastructure (Data access, external services)
 * - DataLoader.Shared (Shared models and utilities)
 * - DataLoader.Tests (Unit and integration tests)
 */

// ======================================================================
// DataLoader.API/Program.cs
// ======================================================================

using DataLoader.API.Extensions;
using DataLoader.API.Middleware;
using DataLoader.Core;
using DataLoader.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add core and infrastructure services
builder.Services.AddCoreServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// Add cross-cutting concerns
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database")
    .AddCheck<CacheServiceHealthCheck>("cache-service");

// Add OpenTelemetry
builder.Services.AddOpenTelemetry()
    .ConfigureTracing(builder.Configuration)
    .ConfigureMetrics(builder.Configuration);

// Add API versioning
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler();
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Add custom middleware
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();

// Map endpoints
app.MapControllers();
app.MapDataLoaderEndpoints();

// Map health check endpoints
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = JsonSerializer.Serialize(
            new
            {
                status = report.Status.ToString(),
                checks = report.Entries.Select(e => new { key = e.Key, status = e.Value.Status.ToString() })
            });
        await context.Response.WriteAsync(result);
    }
});

app.Run();

// ======================================================================
// DataLoader.API/Extensions/EndpointExtensions.cs
// ======================================================================

using DataLoader.API.Endpoints;
using DataLoader.Core.Commands;
using DataLoader.Core.Models;
using DataLoader.Core.Queries;
using MediatR;

namespace DataLoader.API.Extensions;

public static class EndpointExtensions
{
    public static void MapDataLoaderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v{version:apiVersion}/data-loader")
            .WithTags("DataLoader")
            .WithOpenApi();

        // Get data sources endpoint
        group.MapGet("/sources", async (
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(new GetDataSourcesQuery(), cancellationToken);
            return Results.Ok(result);
        })
        .WithName("GetDataSources")
        .WithDescription("Gets all configured data sources")
        .Produces<List<DataSourceDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status500InternalServerError);

        // Start data loading job endpoint
        group.MapPost("/jobs", async (
            IMediator mediator,
            LoadDataCommand command,
            CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(command, cancellationToken);
            return Results.Accepted($"/api/v1/data-loader/jobs/{result.JobId}", result);
        })
        .WithName("StartDataLoadingJob")
        .WithDescription("Starts a new data loading job")
        .Produces<JobDto>(StatusCodes.Status202Accepted)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status500InternalServerError);

        // Get job status endpoint
        group.MapGet("/jobs/{jobId:guid}", async (
            Guid jobId,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(new GetJobStatusQuery { JobId = jobId }, cancellationToken);
            return result != null
                ? Results.Ok(result)
                : Results.NotFound();
        })
        .WithName("GetJobStatus")
        .WithDescription("Gets the status of a data loading job")
        .Produces<JobDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status500InternalServerError);

        // Cancel job endpoint
        group.MapDelete("/jobs/{jobId:guid}", async (
            Guid jobId,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(new CancelJobCommand { JobId = jobId }, cancellationToken);
            return result ? Results.NoContent() : Results.NotFound();
        })
        .WithName("CancelJob")
        .WithDescription("Cancels a running job")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status500InternalServerError);
    }
}

// ======================================================================
// DataLoader.API/Middleware/RequestLoggingMiddleware.cs
// ======================================================================

using System.Diagnostics;

namespace DataLoader.API.Middleware;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await _next(context);
            stopwatch.Stop();

            var statusCode = context.Response.StatusCode;
            var level = statusCode >= 500 ? LogLevel.Error :
                       statusCode >= 400 ? LogLevel.Warning : LogLevel.Information;

            _logger.Log(level, "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms",
                context.Request.Method,
                context.Request.Path,
                statusCode,
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "HTTP {Method} {Path} failed in {ElapsedMs}ms",
                context.Request.Method,
                context.Request.Path,
                stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}

// ======================================================================
// DataLoader.API/Middleware/CorrelationIdMiddleware.cs
// ======================================================================

namespace DataLoader.API.Middleware;

public class CorrelationIdMiddleware
{
    private const string CorrelationIdHeaderName = "X-Correlation-ID";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(CorrelationIdHeaderName, out var correlationId))
        {
            correlationId = Guid.NewGuid().ToString();
            context.Request.Headers.Append(CorrelationIdHeaderName, correlationId);
        }

        // Add correlation ID to the response
        context.Response.OnStarting(() => 
        {
            context.Response.Headers.Append(CorrelationIdHeaderName, correlationId);
            return Task.CompletedTask;
        });

        // Add correlation ID to the Activity
        var activity = Activity.Current;
        if (activity != null)
        {
            activity.SetTag("correlation_id", correlationId);
        }

        await _next(context);
    }
}

// ======================================================================
// DataLoader.API/Middleware/GlobalExceptionHandler.cs
// ======================================================================

using DataLoader.Core.Exceptions;
using Microsoft.AspNetCore.Diagnostics;

namespace DataLoader.API.Middleware;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "An unhandled exception occurred");

        var statusCode = exception switch
        {
            ValidationException => StatusCodes.Status400BadRequest,
            NotFoundException => StatusCodes.Status404NotFound,
            DataSourceException => StatusCodes.Status502BadGateway,
            _ => StatusCodes.Status500InternalServerError
        };

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = GetTitle(exception),
            Detail = exception.Message,
            Type = GetType(exception)
        };

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }

    private static string GetTitle(Exception exception) => exception switch
    {
        ValidationException => "Validation Error",
        NotFoundException => "Resource Not Found",
        DataSourceException => "Data Source Error",
        _ => "Server Error"
    };

    private static string GetType(Exception exception) => exception switch
    {
        ValidationException => "https://dataloader.error/validation",
        NotFoundException => "https://dataloader.error/not-found",
        DataSourceException => "https://dataloader.error/data-source",
        _ => "https://dataloader.error/server"
    };
}

// ======================================================================
// DataLoader.Core/Commands/LoadDataCommand.cs
// ======================================================================

using DataLoader.Core.Models;
using FluentValidation;
using MediatR;

namespace DataLoader.Core.Commands;

public class LoadDataCommand : IRequest<JobDto>
{
    public string SourceId { get; set; } = string.Empty;
    public string? Query { get; set; }
    public List<DataProcessingStep>? ProcessingSteps { get; set; }
    public DataDestination? Destination { get; set; }
    public Dictionary<string, string>? Parameters { get; set; }
}

public class LoadDataCommandValidator : AbstractValidator<LoadDataCommand>
{
    public LoadDataCommandValidator()
    {
        RuleFor(x => x.SourceId).NotEmpty().WithMessage("Source ID is required");
        
        When(x => x.Destination != null, () => {
            RuleFor(x => x.Destination.Type).IsInEnum().WithMessage("Invalid destination type");
            RuleFor(x => x.Destination.ConnectionInfo).NotEmpty().WithMessage("Destination connection info is required");
        });

        When(x => x.ProcessingSteps != null && x.ProcessingSteps.Any(), () => {
            RuleForEach(x => x.ProcessingSteps).ChildRules(step => {
                step.RuleFor(s => s.Type).NotEmpty().WithMessage("Processing step type is required");
                step.RuleFor(s => s.Configuration).NotNull().WithMessage("Processing step configuration is required");
            });
        });
    }
}

// ======================================================================
// DataLoader.Core/Commands/LoadDataCommandHandler.cs
// ======================================================================

using DataLoader.Core.Exceptions;
using DataLoader.Core.Interfaces;
using DataLoader.Core.Models;
using DataLoader.Core.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace DataLoader.Core.Commands;

public class LoadDataCommandHandler : IRequestHandler<LoadDataCommand, JobDto>
{
    private readonly IDataSourceRegistry _dataSourceRegistry;
    private readonly IJobManager _jobManager;
    private readonly ILogger<LoadDataCommandHandler> _logger;

    public LoadDataCommandHandler(
        IDataSourceRegistry dataSourceRegistry,
        IJobManager jobManager,
        ILogger<LoadDataCommandHandler> logger)
    {
        _dataSourceRegistry = dataSourceRegistry;
        _jobManager = jobManager;
        _logger = logger;
    }

    public async Task<JobDto> Handle(LoadDataCommand request, CancellationToken cancellationToken)
    {
        // Validate the data source exists
        var dataSource = await _dataSourceRegistry.GetDataSourceAsync(request.SourceId, cancellationToken);
        if (dataSource == null)
        {
            throw new NotFoundException($"Data source '{request.SourceId}' not found");
        }

        // Create and start the job
        var job = await _jobManager.CreateJobAsync(new JobDefinition
        {
            SourceId = request.SourceId,
            Query = request.Query,
            ProcessingSteps = request.ProcessingSteps ?? new List<DataProcessingStep>(),
            Destination = request.Destination,
            Parameters = request.Parameters ?? new Dictionary<string, string>()
        }, cancellationToken);

        _logger.LogInformation("Created new data loading job {JobId} for source {SourceId}", 
            job.Id, request.SourceId);

        // Start the job background process
        await _jobManager.StartJobAsync(job.Id, cancellationToken);

        return new JobDto
        {
            JobId = job.Id,
            Status = job.Status.ToString(),
            StartTime = job.StartTime,
            EstimatedCompletion = job.EstimatedCompletion
        };
    }
}

// ======================================================================
// DataLoader.Core/Commands/CancelJobCommand.cs
// ======================================================================

using MediatR;

namespace DataLoader.Core.Commands;

public class CancelJobCommand : IRequest<bool>
{
    public Guid JobId { get; set; }
}

public class CancelJobCommandHandler : IRequestHandler<CancelJobCommand, bool>
{
    private readonly IJobManager _jobManager;
    private readonly ILogger<CancelJobCommandHandler> _logger;

    public CancelJobCommandHandler(IJobManager jobManager, ILogger<CancelJobCommandHandler> logger)
    {
        _jobManager = jobManager;
        _logger = logger;
    }

    public async Task<bool> Handle(CancelJobCommand request, CancellationToken cancellationToken)
    {
        var result = await _jobManager.CancelJobAsync(request.JobId, cancellationToken);
        
        if (result)
        {
            _logger.LogInformation("Job {JobId} cancelled successfully", request.JobId);
            return true;
        }
        
        _logger.LogWarning("Job {JobId} not found or already completed", request.JobId);
        return false;
    }
}

// ======================================================================
// DataLoader.Core/DependencyInjection.cs
// ======================================================================

using DataLoader.Core.Commands;
using DataLoader.Core.Interfaces;
using DataLoader.Core.Services;
using DataLoader.Core.Validators;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace DataLoader.Core;

public static class DependencyInjection
{
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        // Register MediatR
        services.AddMediatR(cfg => {
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        });

        // Register validators
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        // Register core services
        services.AddSingleton<IJobManager, JobManager>();
        services.AddSingleton<IDataProcessorFactory, DataProcessorFactory>();
        
        // Register background services
        services.AddHostedService<JobProcessingService>();

        return services;
    }
}

// ======================================================================
// DataLoader.Core/Interfaces/IDataSource.cs
// ======================================================================

using DataLoader.Core.Models;

namespace DataLoader.Core.Interfaces;

public interface IDataSource
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    Task<DataSourceMetadata> GetMetadataAsync(CancellationToken cancellationToken = default);
    Task<DataStream> ReadDataAsync(string? query, Dictionary<string, string>? parameters, CancellationToken cancellationToken = default);
}

// ======================================================================
// DataLoader.Core/Interfaces/IDataSourceRegistry.cs
// ======================================================================

using DataLoader.Core.Models;

namespace DataLoader.Core.Interfaces;

public interface IDataSourceRegistry
{
    Task<IDataSource?> GetDataSourceAsync(string sourceId, CancellationToken cancellationToken = default);
    Task<List<DataSourceDto>> GetAllDataSourcesAsync(CancellationToken cancellationToken = default);
    Task<bool> RegisterDataSourceAsync(IDataSource dataSource, CancellationToken cancellationToken = default);
    Task<bool> RemoveDataSourceAsync(string sourceId, CancellationToken cancellationToken = default);
}

// ======================================================================
// DataLoader.Core/Interfaces/IDataProcessor.cs
// ======================================================================

using DataLoader.Core.Models;

namespace DataLoader.Core.Interfaces;

public interface IDataProcessor
{
    string ProcessorType { get; }
    Task<DataStream> ProcessAsync(DataStream inputStream, object configuration, CancellationToken cancellationToken = default);
}

// ======================================================================
// DataLoader.Core/Interfaces/IDataProcessorFactory.cs
// ======================================================================

namespace DataLoader.Core.Interfaces;

public interface IDataProcessorFactory
{
    IDataProcessor GetProcessor(string processorType);
    IEnumerable<string> GetAvailableProcessorTypes();
}

// ======================================================================
// DataLoader.Core/Interfaces/IDataDestination.cs
// ======================================================================

using DataLoader.Core.Models;

namespace DataLoader.Core.Interfaces;

public interface IDataDestination
{
    string DestinationType { get; }
    Task WriteDataAsync(DataStream dataStream, string connectionInfo, CancellationToken cancellationToken = default);
}

// ======================================================================
// DataLoader.Core/Interfaces/IJobManager.cs
// ======================================================================

using DataLoader.Core.Models;

namespace DataLoader.Core.Interfaces;

public interface IJobManager
{
    Task<Job> CreateJobAsync(JobDefinition definition, CancellationToken cancellationToken = default);
    Task<bool> StartJobAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<bool> CancelJobAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<Job?> GetJobAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task UpdateJobStatusAsync(Guid jobId, JobStatus status, string? message = null, CancellationToken cancellationToken = default);
    Task<List<Job>> GetActiveJobsAsync(CancellationToken cancellationToken = default);
}

// ======================================================================
// DataLoader.Core/Models/DataSourceDto.cs
// ======================================================================

namespace DataLoader.Core.Models;

public record DataSourceDto
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Type { get; init; }
    public Dictionary<string, string>? Capabilities { get; init; }
}

// ======================================================================
// DataLoader.Core/Models/DataDestination.cs
// ======================================================================

namespace DataLoader.Core.Models;

public class DataDestination
{
    public DestinationType Type { get; set; }
    public required string ConnectionInfo { get; set; }
}

public enum DestinationType
{
    DatabaseTable,
    CacheService,
    MessageQueue,
    FileSystem,
    RestApi,
    Custom
}

// ======================================================================
// DataLoader.Core/Models/DataProcessingStep.cs
// ======================================================================

namespace DataLoader.Core.Models;

public class DataProcessingStep
{
    public required string Type { get; set; }
    public required object Configuration { get; set; }
}

// ======================================================================
// DataLoader.Core/Models/JobDto.cs
// ======================================================================

namespace DataLoader.Core.Models;

public class JobDto
{
    public Guid JobId { get; set; }
    public required string Status { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public DateTime? EstimatedCompletion { get; set; }
    public int? Progress { get; set; }
    public string? Message { get; set; }
    public Dictionary<string, string>? Metrics { get; set; }
}

// ======================================================================
// DataLoader.Core/Models/Job.cs
// ======================================================================

namespace DataLoader.Core.Models;

public class Job
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public JobStatus Status { get; set; } = JobStatus.Created;
    public required JobDefinition Definition { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public DateTime? EstimatedCompletion { get; set; }
    public int? Progress { get; set; }
    public string? StatusMessage { get; set; }
    public Dictionary<string, string> Metrics { get; set; } = new();
    public CancellationTokenSource? CancellationTokenSource { get; set; }
}

public enum JobStatus
{
    Created,
    Queued,
    Running,
    Completed,
    Failed,
    Cancelled
}

// ======================================================================
// DataLoader.Core/Models/JobDefinition.cs
// ======================================================================

namespace DataLoader.Core.Models;

public class JobDefinition
{
    public required string SourceId { get; set; }
    public string? Query { get; set; }
    public List<DataProcessingStep> ProcessingSteps { get; set; } = new();
    public DataDestination? Destination { get; set; }
    public Dictionary<string, string> Parameters { get; set; } = new();
}

// ======================================================================
// DataLoader.Core/Models/DataStream.cs
// ======================================================================

namespace DataLoader.Core.Models;

public class DataStream : IDisposable, IAsyncDisposable
{
    private readonly Stream _stream;
    private bool _disposed = false;

    public DataStream(Stream stream, string contentType, Dictionary<string, string>? metadata = null)
    {
        _stream = stream;
        ContentType = contentType;
        Metadata = metadata ?? new Dictionary<string, string>();
    }

    public Stream Stream => _stream;
    public string ContentType { get; }
    public Dictionary<string, string> Metadata { get; }
    
    public long? Length => _stream.CanSeek ? _stream.Length : null;

    public void Dispose()
    {
        if (!_disposed)
        {
            _stream.Dispose();
            _disposed = true;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            await _stream.DisposeAsync();
            _disposed = true;
        }
    }
}

// ======================================================================
// DataLoader.Core/Models/DataSourceMetadata.cs
// ======================================================================

namespace DataLoader.Core.Models;

public class DataSourceMetadata
{
    public required string Type { get; set; }
    public Dictionary<string, string> Capabilities { get; set; } = new();
    public Dictionary<string, string> Schema { get; set; } = new();
}

// ======================================================================
// DataLoader.Core/Queries/GetDataSourcesQuery.cs
// ======================================================================

using DataLoader.Core.Interfaces;
using DataLoader.Core.Models;
using MediatR;

namespace DataLoader.Core.Queries;

public class GetDataSourcesQuery : IRequest<List<DataSourceDto>>
{
}

public class GetDataSourcesQueryHandler : IRequestHandler<GetDataSourcesQuery, List<DataSourceDto>>
{
    private readonly IDataSourceRegistry _dataSourceRegistry;

    public GetDataSourcesQueryHandler(IDataSourceRegistry dataSourceRegistry)
    {
        _dataSourceRegistry = dataSourceRegistry;
    }

    public Task<List<DataSourceDto>> Handle(GetDataSourcesQuery request, CancellationToken cancellationToken)
    {
        return _dataSourceRegistry.GetAllDataSourcesAsync(cancellationToken);
    }
}

// ======================================================================
// DataLoader.Core/Queries/GetJobStatusQuery.cs
// ======================================================================

using DataLoader.Core.Interfaces;
using DataLoader.Core.Models;
using MediatR;

namespace DataLoader.Core.Queries;

public class GetJobStatusQuery : IRequest<JobDto?>
{
    public Guid JobId { get; set; }
}

public class GetJobStatusQueryHandler : IRequestHandler<GetJobStatusQuery, JobDto?>
{
    private readonly IJobManager _jobManager;

    public GetJobStatusQueryHandler(IJobManager jobManager)
    {
        _jobManager = jobManager;
    }

    public async Task<JobDto?> Handle(GetJobStatusQuery request, CancellationToken cancellationToken)
    {
        var job = await _jobManager.GetJobAsync(request.JobId, cancellationToken);
        
        if (job == null)
        {
            return null;
        }

        return new JobDto
        {
            JobId = job.Id,
            Status = job.Status.ToString(),
            StartTime = job.StartTime,
            EndTime = job.EndTime,
            EstimatedCompletion = job.EstimatedCompletion,
            Progress = job.Progress,
            Message = job.StatusMessage,
            Metrics = job.Metrics
        };
    }
}

// ======================================================================
// DataLoader.Core/Services/JobManager.cs
// ======================================================================

using DataLoader.Core.Interfaces;
using DataLoader.Core.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace DataLoader.Core.Services;

public class JobManager : IJobManager
{
    private readonly ConcurrentDictionary<Guid, Job> _jobs = new();
    private readonly ILogger<JobManager> _logger;

    public JobManager(ILogger<JobManager> logger)
    {
        _logger = logger;
    }

    public Task<Job> CreateJobAsync(JobDefinition definition, CancellationToken cancellationToken = default)
    {
        var job = new Job
        {
            Definition = definition,
            Status = JobStatus.Created,
            CancellationTokenSource = new CancellationTokenSource()
        };

        _jobs.TryAdd(job.Id, job);
        _logger.LogInformation("Created job {JobId} for source {SourceId}", job.Id, definition.SourceId);

        return Task.FromResult(job);
    }

    public Task<bool> StartJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        if (!_jobs.TryGetValue(jobId, out var job))
        {
            _logger.LogWarning("Attempted to start non-existent job {JobId}", jobId);
            return Task.FromResult(false);
        }

        if (job.Status != JobStatus.Created)
        {
            _logger.LogWarning("Cannot start job {JobId} because it is in state {Status}", jobId, job.Status);
            return Task.FromResult(false);
        }

        job.Status = JobStatus.Queued;
        job.StartTime = DateTime.UtcNow;
        
        _logger.LogInformation("Job {JobId} queued for processing", jobId);
        return Task.FromResult(true);
    }

    public Task<bool> CancelJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        if (!_jobs.TryGetValue(jobId, out var job))
        {
            return Task.FromResult(false);
        }

        if (job.Status != JobStatus.Queued && job.Status != JobStatus.Running)
        {
            return Task.FromResult(false);
        }

        job.CancellationTokenSource?.Cancel();
        job.Status = JobStatus.Cancelled;
        job.EndTime = DateTime.UtcNow;
        job.StatusMessage = "Cancelled by user";

        _logger.LogInformation("Job {JobId} cancelled", jobId);
        return Task.FromResult(true);
    }

    public Task<Job?> GetJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        _jobs.TryGetValue(jobId, out var job);
        return Task.FromResult(job);
    }

    public Task UpdateJobStatusAsync(Guid jobId, JobStatus status, string? message = null, CancellationToken cancellationToken = default)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            job.Status = status;
            job.StatusMessage = message;

            if (status == JobStatus.Completed || status == JobStatus.Failed || status == JobStatus.Cancelled)
            {
                job.EndTime = DateTime.UtcNow;
                job.CancellationTokenSource?.Dispose();
                job.CancellationTokenSource = null;
            }

            _logger.LogInformation("Updated job {JobId} status to {Status}", jobId, status);
        }
        else
        {
            _logger.LogWarning("Attempted to update non-existent job {JobId}", jobId);
        }

        return Task.CompletedTask;
    }

    public Task<List<Job>> GetActiveJobsAsync(CancellationToken cancellationToken = default)
    {
        var activeJobs = _jobs.Values
            .Where(j => j.Status == JobStatus.Queued || j.Status == JobStatus.Running)
            .ToList();

        return Task.FromResult(activeJobs);
    }
}

// ======================================================================
// DataLoader.Core/Services/JobProcessingService.cs
// ======================================================================

using DataLoader.Core.Interfaces;
using DataLoader.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DataLoader.Core.Services;

public class JobProcessingService : BackgroundService
{
    private readonly IJobManager _jobManager;
    private readonly IDataSourceRegistry _dataSourceRegistry;
    private readonly IDataProcessorFactory _dataProcessorFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<JobProcessingService> _logger;
    
    private const int JobCheckIntervalMs = 1000;

    public JobProcessingService(
        IJobManager jobManager,
        IDataSourceRegistry dataSourceRegistry,
        IDataProcessorFactory dataProcessorFactory,
        IServiceProvider serviceProvider,
        ILogger<JobProcessingService> logger)
    {
        _jobManager = jobManager;
        _dataSourceRegistry = dataSourceRegistry;
        _dataProcessorFactory = dataProcessorFactory;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Job processing service starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var activeJobs = await _jobManager.GetActiveJobsAsync(stoppingToken);
                
                foreach (var job in activeJobs.Where(j => j.Status == JobStatus.Queued))
                {
                    _ = ProcessJobAsync(job);
                }

                await Task.Delay(JobCheckIntervalMs, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in job processing service");
                await Task.Delay(5000, stoppingToken); // Back off on error
            }
        }

        _logger.LogInformation("Job processing service stopping");
    }

    private async Task ProcessJobAsync(Job job)
    {
        var jobCts = job.CancellationTokenSource ?? new CancellationTokenSource();
        
        try
        {
            await _jobManager.UpdateJobStatusAsync(job.Id, JobStatus.Running);
            
            _logger.LogInformation("Starting to process job {JobId}", job.Id);

            // Get the data source
            var dataSource = await _dataSourceRegistry.GetDataSourceAsync(
                job.Definition.SourceId, jobCts.Token);

            if (dataSource == null)
            {
                await _jobManager.UpdateJobStatusAsync(
                    job.Id, JobStatus.Failed, $"Data source '{job.Definition.SourceId}' not found");
                return;
            }

            // Read data from source
            using var dataStream = await dataSource.ReadDataAsync(
                job.Definition.Query, job.Definition.Parameters, jobCts.Token);

            // Apply processing steps
            var processedStream = dataStream;
            foreach (var step in job.Definition.ProcessingSteps)
            {
                var processor = _dataProcessorFactory.GetProcessor(step.Type);
                processedStream = await processor.ProcessAsync(
                    processedStream, step.Configuration, jobCts.Token);

                // Update progress
                var stepIndex = job.Definition.ProcessingSteps.IndexOf(step) + 1;
                var progress = (int)(stepIndex * 100.0 / (job.Definition.ProcessingSteps.Count + 2));
                job.Progress = progress;
            }

            // Write to destination if specified
            if (job.Definition.Destination != null)
            {
                using var scope = _serviceProvider.CreateScope();
                var destinationServices = scope.ServiceProvider.GetServices<IDataDestination>();
                
                var destinationHandler = destinationServices
                    .FirstOrDefault(d => d.DestinationType.Equals(
                        job.Definition.Destination.Type.ToString(), 
                        StringComparison.OrdinalIgnoreCase));

                if (destinationHandler == null)
                {
                    await _jobManager.UpdateJobStatusAsync(
                        job.Id, JobStatus.Failed, $"Destination type '{job.Definition.Destination.Type}' not supported");
                    return;
                }

                await destinationHandler.WriteDataAsync(
                    processedStream, job.Definition.Destination.ConnectionInfo, jobCts.Token);
            }

            // Mark job as completed
            job.Progress = 100;
            await _jobManager.UpdateJobStatusAsync(job.Id, JobStatus.Completed, "Job completed successfully");
            _logger.LogInformation("Job {JobId} completed successfully", job.Id);
        }
        catch (OperationCanceledException) when (jobCts.Token.IsCancellationRequested)
        {
            _logger.LogInformation("Job {JobId} was cancelled", job.Id);
            await _jobManager.UpdateJobStatusAsync(job.Id, JobStatus.Cancelled, "Job cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing job {JobId}", job.Id);
            await _jobManager.UpdateJobStatusAsync(job.Id, JobStatus.Failed, ex.Message);
        }
    }
}

// ======================================================================
// DataLoader.Core/Services/DataProcessorFactory.cs
// ======================================================================

using DataLoader.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DataLoader.Core.Services;

public class DataProcessorFactory : IDataProcessorFactory
{
    private readonly IServiceProvider _serviceProvider;

    public DataProcessorFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IDataProcessor GetProcessor(string processorType)
    {
        using var scope = _serviceProvider.CreateScope();
        var processors = scope.ServiceProvider.GetServices<IDataProcessor>();
        
        var processor = processors.FirstOrDefault(p => 
            p.ProcessorType.Equals(processorType, StringComparison.OrdinalIgnoreCase));
        
        if (processor == null)
        {
            throw new InvalidOperationException($"Processor type '{processorType}' not found");
        }
        
        return processor;
    }

    public IEnumerable<string> GetAvailableProcessorTypes()
    {
        using var scope = _serviceProvider.CreateScope();
        var processors = scope.ServiceProvider.GetServices<IDataProcessor>();
        return processors.Select(p => p.ProcessorType);
    }
}

// ======================================================================
// DataLoader.Core/Validators/ValidationBehavior.cs
// ======================================================================

using FluentValidation;
using MediatR;
using ValidationException = DataLoader.Core.Exceptions.ValidationException;

namespace DataLoader.Core.Validators;

public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request, 
        RequestHandlerDelegate<TResponse> next, 
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);
        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToList();

        if (failures.Count != 0)
        {
            var errorMessages = failures
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray()
                );

            throw new ValidationException("Validation failed", errorMessages);
        }

        return await next();
    }
}

// ======================================================================
// DataLoader.Core/Validators/LoggingBehavior.cs
// ======================================================================

using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace DataLoader.Core.Validators;

public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request, 
        RequestHandlerDelegate<TResponse> next, 
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        
        _logger.LogInformation("Handling {RequestName}", requestName);
        
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var response = await next();
            stopwatch.Stop();
            
            _logger.LogInformation("Handled {RequestName} in {ElapsedMilliseconds}ms", 
                requestName, stopwatch.ElapsedMilliseconds);
            
            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error handling {RequestName} after {ElapsedMilliseconds}ms", 
                requestName, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}

// ======================================================================
// DataLoader.Core/Exceptions/DataSourceException.cs
// ======================================================================

namespace DataLoader.Core.Exceptions;

public class DataSourceException : Exception
{
    public DataSourceException(string message) : base(message)
    {
    }

    public DataSourceException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

// ======================================================================
// DataLoader.Core/Exceptions/NotFoundException.cs
// ======================================================================

namespace DataLoader.Core.Exceptions;

public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message)
    {
    }
}

// ======================================================================
// DataLoader.Core/Exceptions/ValidationException.cs
// ======================================================================

namespace DataLoader.Core.Exceptions;

public class ValidationException : Exception
{
    public Dictionary<string, string[]> Errors { get; }

    public ValidationException(string message, Dictionary<string, string[]> errors) 
        : base(message)
    {
        Errors = errors;
    }
}

// ======================================================================
// DataLoader.Infrastructure/Data/DataSourceRegistry.cs
// ======================================================================

using DataLoader.Core.Interfaces;
using DataLoader.Core.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace DataLoader.Infrastructure.Data;

public class DataSourceRegistry : IDataSourceRegistry
{
    private readonly ConcurrentDictionary<string, IDataSource> _dataSources = new();
    private readonly ILogger<DataSourceRegistry> _logger;

    public DataSourceRegistry(
        IEnumerable<IDataSource> initialDataSources,
        ILogger<DataSourceRegistry> logger)
    {
        _logger = logger;

        foreach (var dataSource in initialDataSources)
        {
            _dataSources.TryAdd(dataSource.Id, dataSource);
        }
    }

    public Task<IDataSource?> GetDataSourceAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        _dataSources.TryGetValue(sourceId, out var dataSource);
        return Task.FromResult(dataSource);
    }

    public async Task<List<DataSourceDto>> GetAllDataSourcesAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<DataSourceDto>();

        foreach (var dataSource in _dataSources.Values)
        {
            try
            {
                var metadata = await dataSource.GetMetadataAsync(cancellationToken);
                
                result.Add(new DataSourceDto
                {
                    Id = dataSource.Id,
                    Name = dataSource.Name,
                    Description = dataSource.Description,
                    Type = metadata.Type,
                    Capabilities = metadata.Capabilities
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting metadata for data source {SourceId}", dataSource.Id);
            }
        }

        return result;
    }

    public Task<bool> RegisterDataSourceAsync(IDataSource dataSource, CancellationToken cancellationToken = default)
    {
        var result = _dataSources.TryAdd(dataSource.Id, dataSource);
        
        if (result)
        {
            _logger.LogInformation("Registered data source {SourceId}", dataSource.Id);
        }
        else
        {
            _logger.LogWarning("Data source {SourceId} already registered", dataSource.Id);
        }
        
        return Task.FromResult(result);
    }

    public Task<bool> RemoveDataSourceAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        var result = _dataSources.TryRemove(sourceId, out _);
        
        if (result)
        {
            _logger.LogInformation("Removed data source {SourceId}", sourceId);
        }
        else
        {
            _logger.LogWarning("Data source {SourceId} not found for removal", sourceId);
        }
        
        return Task.FromResult(result);
    }
}

// ======================================================================
// DataLoader.Infrastructure/Data/DatabaseHealthCheck.cs
// ======================================================================

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DataLoader.Infrastructure.Data;

public class DatabaseHealthCheck : IHealthCheck
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DatabaseHealthCheck(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            await connection.OpenAsync(cancellationToken);
            
            // Basic query to test the connection
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            await command.ExecuteScalarAsync(cancellationToken);
            
            return HealthCheckResult.Healthy("Database connection is healthy");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database connection failed", ex);
        }
    }
}

// ======================================================================
// DataLoader.Infrastructure/Data/DatabaseConnectionFactory.cs
// ======================================================================

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace DataLoader.Infrastructure.Data;

public interface IDbConnectionFactory
{
    Task<IDbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default);
}

public class SqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new InvalidOperationException("DefaultConnection string is not defined");
    }

    public async Task<IDbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}

// ======================================================================
// DataLoader.Infrastructure/DataSources/SqlDataSource.cs
// ======================================================================

using Dapper;
using DataLoader.Core.Exceptions;
using DataLoader.Core.Interfaces;
using DataLoader.Core.Models;
using DataLoader.Infrastructure.Data;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DataLoader.Infrastructure.DataSources;

public class SqlDataSource : IDataSource
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<SqlDataSource> _logger;
    private readonly string _connectionString;

    public SqlDataSource(
        string id,
        string name, 
        string description,
        string connectionString,
        IDbConnectionFactory connectionFactory,
        ILogger<SqlDataSource> logger)
    {
        Id = id;
        Name = name;
        Description = description;
        _connectionString = connectionString;
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public string Id { get; }
    public string Name { get; }
    public string Description { get; }

    public async Task<DataSourceMetadata> GetMetadataAsync(CancellationToken cancellationToken = default)
    {
        return await Task.FromResult(new DataSourceMetadata
        {
            Type = "SQL",
            Capabilities = new Dictionary<string, string>
            {
                ["SupportsQueries"] = "true",
                ["SupportsParameters"] = "true",
                ["MaxBatchSize"] = "10000"
            },
            Schema = new Dictionary<string, string>
            {
                ["Format"] = "JSON"
            }
        });
    }

    public async Task<DataStream> ReadDataAsync(string? query, Dictionary<string, string>? parameters, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(query))
        {
            throw new ValidationException("Query is required", new Dictionary<string, string[]>
            {
                ["Query"] = new[] { "SQL query is required" }
            });
        }

        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            
            // Parse parameters
            var queryParams = new DynamicParameters();
            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    queryParams.Add(param.Key, param.Value);
                }
            }

            // Execute query
            var result = await connection.QueryAsync(query, queryParams);
            
            // Convert to JSON stream
            var resultStream = new MemoryStream();
            await JsonSerializer.SerializeAsync(resultStream, result, cancellationToken: cancellationToken);
            resultStream.Position = 0;

            _logger.LogInformation("Executed SQL query with {ParameterCount} parameters", parameters?.Count ?? 0);

            return new DataStream(resultStream, "application/json", new Dictionary<string, string>
            {
                ["RowCount"] = result.Count().ToString(),
                ["Source"] = "SQL"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing SQL query");
            throw new DataSourceException("Error executing SQL query", ex);
        }
    }
}

// ======================================================================
// DataLoader.Infrastructure/DataSources/RestApiDataSource.cs
// ======================================================================

using DataLoader.Core.Exceptions;
using DataLoader.Core.Interfaces;
using DataLoader.Core.Models;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;
using System.Text;
using System.Text.Json;

namespace DataLoader.Infrastructure.DataSources;

public class RestApiDataSource : IDataSource
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RestApiDataSource> _logger;
    private readonly string _baseUrl;

    public RestApiDataSource(
        string id,
        string name,
        string description,
        string baseUrl,
        HttpClient httpClient,
        ILogger<RestApiDataSource> logger)
    {
        Id = id;
        Name = name;
        Description = description;
        _baseUrl = baseUrl;
        _httpClient = httpClient;
        _logger = logger;
    }

    public string Id { get; }
    public string Name { get; }
    public string Description { get; }

    public async Task<DataSourceMetadata> GetMetadataAsync(CancellationToken cancellationToken = default)
    {
        return await Task.FromResult(new DataSourceMetadata
        {
            Type = "REST",
            Capabilities = new Dictionary<string, string>
            {
                ["SupportsQueries"] = "true",
                ["SupportsAuthentication"] = "true",
                ["SupportedMethods"] = "GET,POST"
            }
        });
    }

    public async Task<DataStream> ReadDataAsync(string? query, Dictionary<string, string>? parameters, CancellationToken cancellationToken = default)
    {
        try
        {
            var endpoint = string.IsNullOrEmpty(query) ? "" : query;
            var url = $"{_baseUrl.TrimEnd('/')}/{endpoint.TrimStart('/')}";

            // Get HTTP method from parameters or default to GET
            var method = parameters != null && parameters.TryGetValue("Method", out var methodValue)
                ? methodValue.ToUpper()
                : "GET";

            // Extract and remove special parameters
            parameters?.Remove("Method");
            
            var headers = new Dictionary<string, string>();
            if (parameters != null)
            {
                foreach (var param in parameters.Where(p => p.Key.StartsWith("Header_")).ToList())
                {
                    headers[param.Key.Substring(7)] = param.Value;
                    parameters.Remove(param.Key);
                }
            }

            // Create request
            HttpRequestMessage request;
            if (method == "GET")
            {
                // Add query parameters
                var uriBuilder = new UriBuilder(url);
                var query = new StringBuilder(uriBuilder.Query.TrimStart('?'));
                
                if (parameters != null)
                {
                    foreach (var param in parameters)
                    {
                        if (query.Length > 0)
                            query.Append('&');
                        
                        query.Append($"{Uri.EscapeDataString(param.Key)}={Uri.EscapeDataString(param.Value)}");
                    }
                }
                
                uriBuilder.Query = query.ToString();
                request = new HttpRequestMessage(HttpMethod.Get, uriBuilder.Uri);
            }
            else if (method == "POST")
            {
                request = new HttpRequestMessage(HttpMethod.Post, url);
                
                if (parameters != null)
                {
                    var contentType = headers.ContainsKey("Content-Type") ? headers["Content-Type"] : "application/json";
                    
                    if (contentType.Contains("json"))
                    {
                        var json = JsonSerializer.Serialize(parameters);
                        request.Content = new StringContent(json, Encoding.UTF8, contentType);
                    }
                    else if (contentType.Contains("x-www-form-urlencoded"))
                    {
                        var formData = new FormUrlEncodedContent(parameters);
                        request.Content = formData;
                    }
                }
            }
            else
            {
                throw new ValidationException("Unsupported HTTP method", 
                    new Dictionary<string, string[]> { ["Method"] = new[] { "Only GET and POST methods are supported" } });
            }

            // Add headers
            foreach (var header in headers)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            // Set up retry policy
            var retryPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(response => response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (outcome, timespan, retryAttempt, context) =>
                    {
                        _logger.LogWarning("Request failed with {StatusCode}. Waiting {TimeSpan} before retry attempt {RetryAttempt}",
                            outcome.Result?.StatusCode, timespan, retryAttempt);
                    });

            // Execute request with retry
            var response = await retryPolicy.ExecuteAsync(() => _httpClient.SendAsync(request, cancellationToken));
            
            // Check for successful response
            if (!response.IsSuccessStatusCode)
            {
                throw new DataSourceException($"API request failed with status code {response.StatusCode}");
            }

            // Get content stream
            var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";

            _logger.LogInformation("Successfully retrieved data from REST API endpoint {Endpoint}", endpoint);

            return new DataStream(contentStream, contentType, new Dictionary<string, string>
            {
                ["StatusCode"] = ((int)response.StatusCode).ToString(),
                ["Source"] = "REST"
            });
        }
        catch (Exception ex) when (ex is not DataSourceException)
        {
            _logger.LogError(ex, "Error retrieving data from REST API");
            throw new DataSourceException("Error retrieving data from REST API", ex);
        }
    }
}

// ======================================================================
// DataLoader.Infrastructure/DataProcessors/FilterProcessor.cs
// ======================================================================

using DataLoader.Core.Interfaces;
using DataLoader.Core.Models;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DataLoader.Infrastructure.DataProcessors;

public class FilterProcessor : IDataProcessor
{
    public string ProcessorType => "Filter";

    public async Task<DataStream> ProcessAsync(
        DataStream inputStream, 
        object configuration, 
        CancellationToken cancellationToken = default)
    {
        // Convert configuration
        var options = configuration is FilterOptions config 
            ? config 
            : JsonSerializer.Deserialize<FilterOptions>(
                JsonSerializer.Serialize(configuration), 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (options == null)
        {
            throw new ArgumentException("Invalid filter configuration");
        }

        // Only handle JSON data for now
        if (!inputStream.ContentType.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            // Pass through non-JSON data
            return inputStream;
        }

        // Parse JSON
        var jsonArray = await JsonSerializer.DeserializeAsync<JsonArray>(
            inputStream.Stream, cancellationToken: cancellationToken);

        if (jsonArray == null)
        {
            // Return empty result for null input
            var emptyStream = new MemoryStream();
            await JsonSerializer.SerializeAsync(emptyStream, new JsonArray(), cancellationToken: cancellationToken);
            emptyStream.Position = 0;
            return new DataStream(emptyStream, inputStream.ContentType, inputStream.Metadata);
        }

        // Apply filters
        var filteredArray = new JsonArray();
        foreach (var item in jsonArray)
        {
            if (item is not JsonObject itemObj)
            {
                continue;
            }

            bool includeItem = true;
            foreach (var filter in options.Conditions)
            {
                if (!itemObj.TryGetPropertyValue(filter.Field, out var value))
                {
                    includeItem = false;
                    break;
                }

                var stringValue = value?.ToString();
                if (stringValue == null)
                {
                    includeItem = false;
                    break;
                }

                includeItem = filter.Operator switch
                {
                    "equals" => stringValue == filter.Value,
                    "notEquals" => stringValue != filter.Value,
                    "contains" => stringValue.Contains(filter.Value, StringComparison.OrdinalIgnoreCase),
                    "startsWith" => stringValue.StartsWith(filter.Value, StringComparison.OrdinalIgnoreCase),
                    "endsWith" => stringValue.EndsWith(filter.Value, StringComparison.OrdinalIgnoreCase),
                    "greaterThan" => decimal.TryParse(stringValue, out var num1) && 
                                    decimal.TryParse(filter.Value, out var num2) && 
                                    num1 > num2,
                    "lessThan" => decimal.TryParse(stringValue, out var num1) && 
                                    decimal.TryParse(filter.Value, out var num2) && 
                                    num1 < num2,
                    _ => false
                };

                if (!includeItem)
                {
                    break;
                }
            }

            if (includeItem)
            {
                filteredArray.Add(item.DeepClone());
            }
        }

        // Create result stream
        var resultStream = new MemoryStream();
        await JsonSerializer.SerializeAsync(resultStream, filteredArray, cancellationToken: cancellationToken);
        resultStream.Position = 0;

        // Update metadata
        var metadata = new Dictionary<string, string>(inputStream.Metadata)
        {
            ["FilteredCount"] = filteredArray.Count.ToString(),
            ["OriginalCount"] = jsonArray.Count.ToString()
        };

        return new DataStream(resultStream, inputStream.ContentType, metadata);
    }

    public class FilterOptions
    {
        public List<FilterCondition> Conditions { get; set; } = new();
    }

    public class FilterCondition
    {
        public string Field { get; set; } = string.Empty;
        public string Operator { get; set; } = "equals";
        public string Value { get; set; } = string.Empty;
    }
}

// ======================================================================
// DataLoader.Infrastructure/DataProcessors/TransformProcessor.cs
// ======================================================================

using DataLoader.Core.Interfaces;
using DataLoader.Core.Models;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DataLoader.Infrastructure.DataProcessors;

public class TransformProcessor : IDataProcessor
{
    public string ProcessorType => "Transform";

    public async Task<DataStream> ProcessAsync(
        DataStream inputStream, 
        object configuration, 
        CancellationToken cancellationToken = default)
    {
        // Convert configuration
        var options = configuration is TransformOptions config 
            ? config 
            : JsonSerializer.Deserialize<TransformOptions>(
                JsonSerializer.Serialize(configuration), 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (options == null)
        {
            throw new ArgumentException("Invalid transform configuration");
        }

        // Only handle JSON data for now
        if (!inputStream.ContentType.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            // Pass through non-JSON data
            return inputStream;
        }

        // Parse JSON
        var jsonArray = await JsonSerializer.DeserializeAsync<JsonArray>(
            inputStream.Stream, cancellationToken: cancellationToken);

        if (jsonArray == null)
        {
            // Return empty result for null input
            var emptyStream = new MemoryStream();
            await JsonSerializer.SerializeAsync(emptyStream, new JsonArray(), cancellationToken: cancellationToken);
            emptyStream.Position = 0;
            return new DataStream(emptyStream, inputStream.ContentType, inputStream.Metadata);
        }

        // Apply transformations
        var transformedArray = new JsonArray();
        
        foreach (var item in jsonArray)
        {
            if (item is not JsonObject itemObj)
            {
                continue;
            }

            var resultObj = new JsonObject();
            
            // Apply field mappings
            foreach (var mapping in options.FieldMappings)
            {
                if (itemObj.TryGetPropertyValue(mapping.SourceField, out var value))
                {
                    if (string.IsNullOrEmpty(mapping.Transformation))
                    {
                        // Simple field mapping
                        resultObj[mapping.TargetField] = value?.DeepClone();
                    }
                    else
                    {
                        // Apply transformation
                        var stringValue = value?.ToString() ?? string.Empty;
                        resultObj[mapping.TargetField] = mapping.Transformation switch
                        {
                            "uppercase" => stringValue.ToUpperInvariant(),
                            "lowercase" => stringValue.ToLowerInvariant(),
                            "trim" => stringValue.Trim(),
                            "parseInt" => int.TryParse(stringValue, out var intValue) ? intValue : null,
                            "parseDouble" => double.TryParse(stringValue, out var doubleValue) ? doubleValue : null,
                            _ => stringValue // Default is pass-through
                        };
                    }
                }
                else if (mapping.Required)
                {
                    // Skip this item if a required field is missing
                    resultObj = null;
                    break;
                }
            }

            if (resultObj != null)
            {
                transformedArray.Add(resultObj);
            }
        }

        // Create result stream
        var resultStream = new MemoryStream();
        await JsonSerializer.SerializeAsync(resultStream, transformedArray, cancellationToken: cancellationToken);
        resultStream.Position = 0;

        // Update metadata
        var metadata = new Dictionary<string, string>(inputStream.Metadata)
        {
            ["TransformedCount"] = transformedArray.Count.ToString(),
            ["OriginalCount"] = jsonArray.Count.ToString()
        };

        return new DataStream(resultStream, inputStream.ContentType, metadata);
    }

    public class TransformOptions
    {
        public List<FieldMapping> FieldMappings { get; set; } = new();
    }

    public class FieldMapping
    {
        public string SourceField { get; set; } = string.Empty;
        public string TargetField { get; set; } = string.Empty;
        public bool Required { get; set; } = false;
        public string? Transformation { get; set; }
    }
}

// ======================================================================
// DataLoader.Infrastructure/DataProcessors/AggregateProcessor.cs
// ======================================================================

using DataLoader.Core.Interfaces;
using DataLoader.Core.Models;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DataLoader.Infrastructure.DataProcessors;

public class AggregateProcessor : IDataProcessor
{
    public string ProcessorType => "Aggregate";

    public async Task<DataStream> ProcessAsync(
        DataStream inputStream, 
        object configuration, 
        CancellationToken cancellationToken = default)
    {
        // Convert configuration
        var options = configuration is AggregateOptions config 
            ? config 
            : JsonSerializer.Deserialize<AggregateOptions>(
                JsonSerializer.Serialize(configuration), 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (options == null)
        {
            throw new ArgumentException("Invalid aggregate configuration");
        }

        // Only handle JSON data for now
        if (!inputStream.ContentType.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            // Pass through non-JSON data
            return inputStream;
        }

        // Parse JSON
        var jsonArray = await JsonSerializer.DeserializeAsync<JsonArray>(
            inputStream.Stream, cancellationToken: cancellationToken);

        if (jsonArray == null || jsonArray.Count == 0)
        {
            // Return empty result for null or empty input
            var emptyStream = new MemoryStream();
            await JsonSerializer.SerializeAsync(emptyStream, new JsonArray(), cancellationToken: cancellationToken);
            emptyStream.Position = 0;
            return new DataStream(emptyStream, inputStream.ContentType, inputStream.Metadata);
        }

        // Group data
        var groups = new Dictionary<string, List<JsonObject>>();
        
        foreach (var item in jsonArray)
        {
            if (item is not JsonObject itemObj)
            {
                continue;
            }

            // Generate group key
            var keyParts = new List<string>();
            foreach (var field in options.GroupByFields)
            {
                if (itemObj.TryGetPropertyValue(field, out var value))
                {
                    keyParts.Add(value?.ToString() ?? "null");
                }
                else
                {
                    keyParts.Add("null");
                }
            }
            
            var groupKey = string.Join("_", keyParts);
            
            if (!groups.TryGetValue(groupKey, out var group))
            {
                group = new List<JsonObject>();
                groups[groupKey] = group;
            }
            
            group.Add(itemObj);
        }

        // Apply aggregations
        var resultArray = new JsonArray();
        
        foreach (var group in groups)
        {
            var resultObj = new JsonObject();
            
            // Add group by fields
            var firstItem = group.Value.First();
            foreach (var field in options.GroupByFields)
            {
                if (firstItem.TryGetPropertyValue(field, out var value))
                {
                    resultObj[field] = value?.DeepClone();
                }
            }
            
            // Apply aggregations
            foreach (var agg in options.Aggregations)
            {
                if (string.IsNullOrEmpty(agg.Field) || string.IsNullOrEmpty(agg.Function))
                {
                    continue;
                }

                var values = new List<decimal>();
                foreach (var item in group.Value)
                {
                    if (item.TryGetPropertyValue(agg.Field, out var value) && 
                        decimal.TryParse(value?.ToString(), out var decimalValue))
                    {
                        values.Add(decimalValue);
                    }
                }

                if (values.Count > 0)
                {
                    var result = agg.Function.ToLowerInvariant() switch
                    {
                        "sum" => values.Sum(),
                        "avg" => values.Average(),
                        "min" => values.Min(),
                        "max" => values.Max(),
                        "count" => values.Count,
                        _ => 0m
                    };

                    resultObj[agg.OutputField ?? $"{agg.Function}_{agg.Field}"] = result;
                }
                else if (agg.Function.Equals("count", StringComparison.OrdinalIgnoreCase))
                {
                    resultObj[agg.OutputField ?? $"{agg.Function}_{agg.Field}"] = 0;
                }
            }
            
            resultArray.Add(resultObj);
        }

        // Create result stream
        var resultStream = new MemoryStream();
        await JsonSerializer.SerializeAsync(resultStream, resultArray, cancellationToken: cancellationToken);
        resultStream.Position = 0;

        // Update metadata
        var metadata = new Dictionary<string, string>(inputStream.Metadata)
        {
            ["GroupCount"] = groups.Count.ToString(),
            ["OriginalCount"] = jsonArray.Count.ToString()
        };

        return new DataStream(resultStream, inputStream.ContentType, metadata);
    }

    public class AggregateOptions
    {
        public List<string> GroupByFields { get; set; } = new();
        public List<AggregateFunction> Aggregations { get; set; } = new();
    }

    public class AggregateFunction
    {
        public string Field { get; set; } = string.Empty;
        public string Function { get; set; } = string.Empty;
        public string? OutputField { get; set; }
    }
}

// ======================================================================
// DataLoader.Infrastructure/Destinations/CacheDestination.cs
// ======================================================================

using DataLoader.Core.Interfaces;
using DataLoader.Core.Models;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text;

namespace DataLoader.Infrastructure.Destinations;

public class CacheDestination : IDataDestination
{
    private readonly ILogger<CacheDestination> _logger;
    private readonly IConnectionMultiplexer _redis;

    public CacheDestination(
        IConnectionMultiplexer redis,
        ILogger<CacheDestination> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public string DestinationType => DestinationType.CacheService.ToString();

    public async Task WriteDataAsync(
        DataStream dataStream, 
        string connectionInfo, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Parse connection info (JSON string with CacheKey, Expiry, etc.)
            var options = JsonSerializer.Deserialize<CacheOptions>(connectionInfo, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new ArgumentException("Invalid cache connection info");

            // Read data from stream
            using var reader = new StreamReader(dataStream.Stream, Encoding.UTF8, leaveOpen: true);
            var data = await reader.ReadToEndAsync(cancellationToken);
            
            // Reset stream position for potential further processing
            dataStream.Stream.Position = 0;

            // Get Redis database
            var db = _redis.GetDatabase();

            // Set cache entry
            var expiry = options.ExpirySeconds > 0 
                ? TimeSpan.FromSeconds(options.ExpirySeconds) 
                : (TimeSpan?)null;

            await db.StringSetAsync(
                options.CacheKey,
                data,
                expiry,
                When.Always,
                CommandFlags.FireAndForget);

            // Record metadata
            if (options.StoreMetadata && dataStream.Metadata.Count > 0)
            {
                var metadataKey = $"{options.CacheKey}:metadata";
                var metadataJson = JsonSerializer.Serialize(dataStream.Metadata);
                
                await db.StringSetAsync(
                    metadataKey,
                    metadataJson,
                    expiry,
                    When.Always,
                    CommandFlags.FireAndForget);
            }

            _logger.LogInformation(
                "Data cached with key {CacheKey}, expiry {ExpirySeconds} seconds, metadata stored: {StoreMetadata}",
                options.CacheKey, options.ExpirySeconds, options.StoreMetadata);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing data to cache");
            throw;
        }
    }

    private class CacheOptions
    {
        public string CacheKey { get; set; } = string.Empty;
        public int ExpirySeconds { get; set; } = 3600; // Default 1 hour
        public bool StoreMetadata { get; set; } = false;
    }
}

// ======================================================================
// DataLoader.Infrastructure/Destinations/FileSystemDestination.cs
// ======================================================================

using DataLoader.Core.Interfaces;
using DataLoader.Core.Models;
using Microsoft.Extensions.Logging;

namespace DataLoader.Infrastructure.Destinations;

public class FileSystemDestination : IDataDestination
{
    private readonly ILogger<FileSystemDestination> _logger;

    public FileSystemDestination(ILogger<FileSystemDestination> logger)
    {
        _logger = logger;
    }

    public string DestinationType => DestinationType.FileSystem.ToString();

    public async Task WriteDataAsync(
        DataStream dataStream, 
        string connectionInfo, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Parse connection info
            var options = JsonSerializer.Deserialize<FileSystemOptions>(connectionInfo, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new ArgumentException("Invalid file system connection info");

            // Create directory if it doesn't exist
            var directory = Path.GetDirectoryName(options.FilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Write data to file
            using var fileStream = new FileStream(
                options.FilePath, 
                options.Append ? FileMode.Append : FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                useAsync: true);

            await dataStream.Stream.CopyToAsync(fileStream, cancellationToken);

            // Write metadata if requested
            if (options.WriteMetadata && dataStream.Metadata.Count > 0)
            {
                var metadataPath = options.MetadataPath ?? $"{options.FilePath}.metadata";
                var metadataJson = JsonSerializer.Serialize(dataStream.Metadata);
                await File.WriteAllTextAsync(metadataPath, metadataJson, cancellationToken);
            }

            _logger.LogInformation(
                "Data written to file {FilePath}, append mode: {Append}, metadata written: {WriteMetadata}",
                options.FilePath, options.Append, options.WriteMetadata);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing data to file system");
            throw;
        }
    }

    private class FileSystemOptions
    {
        public string FilePath { get; set; } = string.Empty;
        public bool Append { get; set; } = false;
        public bool WriteMetadata { get; set; } = false;
        public string? MetadataPath { get; set; }
    }
}

// ======================================================================
// DataLoader.Infrastructure/Health/CacheServiceHealthCheck.cs
// ======================================================================

using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace DataLoader.Infrastructure.Health;

public class CacheServiceHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _redis;

    public CacheServiceHealthCheck(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            
            // Simple ping test
            var pong = await db.PingAsync();
            var latency = pong.TotalMilliseconds;
            
            if (latency < 100)
            {
                return HealthCheckResult.Healthy($"Cache service responded in {latency}ms");
            }
            else if (latency < 500)
            {
                return HealthCheckResult.Degraded($"Cache service response time is high: {latency}ms");
            }
            else
            {
                return HealthCheckResult.Unhealthy($"Cache service response time is too high: {latency}ms");
            }
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Cache service connection failed", ex);
        }
    }
}

// ======================================================================
// DataLoader.Infrastructure/DependencyInjection.cs
// ======================================================================

using DataLoader.Core.Interfaces;
using DataLoader.Infrastructure.Data;
using DataLoader.Infrastructure.DataProcessors;
using DataLoader.Infrastructure.DataSources;
using DataLoader.Infrastructure.Destinations;
using DataLoader.Infrastructure.Health;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using StackExchange.Redis;

namespace DataLoader.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database
        services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();

        // Redis
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var connectionString = configuration.GetConnectionString("Redis") 
                ?? "localhost:6379";
            return ConnectionMultiplexer.Connect(connectionString);
        });

        // HTTP clients with resilience policies
        services.AddHttpClient("DefaultClient")
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetCircuitBreakerPolicy());

        // Register data sources
        RegisterDataSources(services, configuration);

        // Register data processors
        services.AddTransient<IDataProcessor, FilterProcessor>();
        services.AddTransient<IDataProcessor, TransformProcessor>();
        services.AddTransient<IDataProcessor, AggregateProcessor>();

        // Register data destinations
        services.AddTransient<IDataDestination, CacheDestination>();
        services.AddTransient<IDataDestination, FileSystemDestination>();

        // Register the data source registry
        services.AddSingleton<IDataSourceRegistry, DataSourceRegistry>();

        // Health checks
        services.AddSingleton<CacheServiceHealthCheck>();

        return services;
    }

    private static void RegisterDataSources(IServiceCollection services, IConfiguration configuration)
    {
        // Register SQL data sources from configuration
        var sqlDataSources = configuration.GetSection("DataSources:Sql").Get<List<SqlDataSourceConfig>>() ?? new List<SqlDataSourceConfig>();
        
        foreach (var config in sqlDataSources)
        {
            services.AddSingleton<IDataSource>(sp => new SqlDataSource(
                config.Id,
                config.Name,
                config.Description,
                config.ConnectionString,
                sp.GetRequiredService<IDbConnectionFactory>(),
                sp.GetRequiredService<ILogger<SqlDataSource>>()));
        }

        // Register REST API data sources from configuration
        var restApiDataSources = configuration.GetSection("DataSources:RestApi").Get<List<RestApiDataSourceConfig>>() ?? new List<RestApiDataSourceConfig>();
        
        foreach (var config in restApiDataSources)
        {
            services.AddSingleton<IDataSource>(sp =>
            {
                var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                var httpClient = httpClientFactory.CreateClient("DefaultClient");
                
                return new RestApiDataSource(
                    config.Id,
                    config.Name,
                    config.Description,
                    config.BaseUrl,
                    httpClient,
                    sp.GetRequiredService<ILogger<RestApiDataSource>>());
            });
        }
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(response => response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
    }

    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));
    }

    private class SqlDataSourceConfig
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ConnectionString { get; set; } = string.Empty;
    }

    private class RestApiDataSourceConfig
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = string.Empty;
    }
}

// ======================================================================
// DataLoader.API/appsettings.json
// ======================================================================

{
    "Logging": {
        "LogLevel": {
            "Default": "Information",
            "Microsoft.AspNetCore": "Warning"
        }
    },
    "AllowedHosts": "*",
    "ConnectionStrings": {
        "DefaultConnection": "Server=localhost;Database=DataLoader;User Id=sa;Password=YourStrongPassword!;TrustServerCertificate=True",
        "Redis": "localhost:6379"
    },
    "DataSources": {
        "Sql": [
            {
                "Id": "customer-db",
                "Name": "Customer Database",
                "Description": "Main customer database with sales and customer information",
                "ConnectionString": "Server=customer-db;Database=Customers;User Id=app_user;Password=App_Password123!;TrustServerCertificate=True"
            },
            {
                "Id": "product-db",
                "Name": "Product Database",
                "Description": "Product catalog and inventory database",
                "ConnectionString": "Server=product-db;Database=Products;User Id=app_user;Password=App_Password123!;TrustServerCertificate=True"
            }
        ],
        "RestApi": [
            {
                "Id": "weather-api",
                "Name": "Weather Service API",
                "Description": "External weather data provider",
                "BaseUrl": "https://api.weather.example.com/v1"
            },
            {
                "Id": "exchange-rate-api",
                "Name": "Currency Exchange API",
                "Description": "Real-time currency exchange rates",
                "BaseUrl": "https://api.exchange.example.com"
            }
        ]
    },
    "OpenTelemetry": {
        "ServiceName": "DataLoader.Microservice",
        "Endpoint": "http://otel-collector:4317",
        "SamplingRatio": 0.25
    }
}

// ======================================================================
// Dockerfile
// ======================================================================

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["DataLoader.API/DataLoader.API.csproj", "DataLoader.API/"]
COPY ["DataLoader.Core/DataLoader.Core.csproj", "DataLoader.Core/"]
COPY ["DataLoader.Infrastructure/DataLoader.Infrastructure.csproj", "DataLoader.Infrastructure/"]
COPY ["DataLoader.Shared/DataLoader.Shared.csproj", "DataLoader.Shared/"]
RUN dotnet restore "DataLoader.API/DataLoader.API.csproj"
COPY . .
WORKDIR "/src/DataLoader.API"
RUN dotnet build "DataLoader.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "DataLoader.API.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DataLoader.API.dll"]

// ======================================================================
// docker-compose.yml
// ======================================================================

version: '3.8'

services:
  dataloader-api:
    image: dataloader-microservice:latest
    build:
      context: .
      dockerfile: Dockerfile
    ports:
      - "5000:80"
      - "5001:443"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__DefaultConnection=Server=sql-server;Database=DataLoader;User Id=sa;Password=YourStrongPassword!;TrustServerCertificate=True
      - ConnectionStrings__Redis=redis:6379
    depends_on:
      - sql-server
      - redis

  sql-server:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      - ACCEPT_EULA=Y
      - SA_PASSWORD=YourStrongPassword!
    ports:
      - "1433:1433"
    volumes:
      - sql-data:/var/opt/mssql

  redis:
    image: redis:alpine
    ports:
      - "6379:6379"
    volumes:
      - redis-data:/data

  otel-collector:
    image: otel/opentelemetry-collector-contrib:latest
    command: ["--config=/etc/otel-collector-config.yaml"]
    volumes:
      - ./otel-collector-config.yaml:/etc/otel-collector-config.yaml
    ports:
      - "4317:4317"

volumes:
  sql-data:
  redis-data:
