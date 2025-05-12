# Infrastructure Layer Implementation

## src/DataProcessingService.Infrastructure/DependencyInjection.cs
```csharp
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using DataProcessingService.Core.Interfaces.Repositories;
using DataProcessingService.Core.Interfaces.Services;
using DataProcessingService.Core.Interfaces.Services.ETL;
using DataProcessingService.Core.Interfaces.Messaging;
using DataProcessingService.Infrastructure.Data;
using DataProcessingService.Infrastructure.Data.Repositories;
using DataProcessingService.Infrastructure.Messaging;
using DataProcessingService.Infrastructure.Messaging.Kafka;
using DataProcessingService.Infrastructure.Services;
using DataProcessingService.Infrastructure.Services.ETL;
using MediatR;

namespace DataProcessingService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register DbContext
        services.AddDbContext<AppDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null);
            });
        });
        
        // Register repositories
        services.AddScoped<IDataSourceRepository, DataSourceRepository>();
        services.AddScoped<IDataPipelineRepository, DataPipelineRepository>();
        services.AddScoped<IPipelineExecutionRepository, PipelineExecutionRepository>();
        
        // Register services
        services.AddScoped<IDataSourceService, DataSourceService>();
        services.AddScoped<IDataPipelineService, DataPipelineService>();
        services.AddScoped<IPipelineExecutionService, PipelineExecutionService>();
        
        // Register ETL services
        services.AddScoped<IDataExtractionService, DataExtractionService>();
        services.AddScoped<IDataTransformationService, DataTransformationService>();
        services.AddScoped<IDataLoadService, DataLoadService>();
        services.AddScoped<IDataConsistencyService, DataConsistencyService>();
        services.AddScoped<IDataReplicationService, DataReplicationService>();
        
        // Register messaging
        var messagingProvider = configuration.GetValue<string>("Messaging:Provider");
        
        switch (messagingProvider?.ToLowerInvariant())
        {
            case "kafka":
                services.AddKafkaMessaging(configuration);
                break;
            case "rabbitmq":
                services.AddRabbitMqMessaging(configuration);
                break;
            case "azureservicebus":
                services.AddAzureServiceBusMessaging(configuration);
                break;
            default:
                services.AddInMemoryMessaging();
                break;
        }
        
        // Register MediatR
        services.AddMediatR(typeof(DependencyInjection).Assembly);
        
        // Register other services
        services.AddHttpClient();
        
        return services;
    }
    
    private static IServiceCollection AddKafkaMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<KafkaConfiguration>(configuration.GetSection("Messaging:Kafka"));
        services.AddSingleton<IMessagePublisher, KafkaMessagePublisher>();
        services.AddSingleton<IMessageConsumer, KafkaMessageConsumer>();
        services.AddSingleton<IEventBus, KafkaEventBus>();
        
        return services;
    }
    
    private static IServiceCollection AddRabbitMqMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Implementation details for RabbitMQ would go here
        services.AddSingleton<IMessagePublisher, InMemoryMessagePublisher>();
        services.AddSingleton<IMessageConsumer, InMemoryMessageConsumer>();
        services.AddSingleton<IEventBus, InMemoryEventBus>();
        
        return services;
    }
    
    private static IServiceCollection AddAzureServiceBusMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Implementation details for Azure Service Bus would go here
        services.AddSingleton<IMessagePublisher, InMemoryMessagePublisher>();
        services.AddSingleton<IMessageConsumer, InMemoryMessageConsumer>();
        services.AddSingleton<IEventBus, InMemoryEventBus>();
        
        return services;
    }
    
    private static IServiceCollection AddInMemoryMessaging(this IServiceCollection services)
    {
        services.AddSingleton<IMessagePublisher, InMemoryMessagePublisher>();
        services.AddSingleton<IMessageConsumer, InMemoryMessageConsumer>();
        services.AddSingleton<IEventBus, InMemoryEventBus>();
        
        return services;
    }
}

## src/DataProcessingService.Infrastructure/Data/AppDbContext.cs
```csharp
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DataProcessingService.Core.Domain.Entities;
using DataProcessingService.Core.Domain.Entities.Base;
using DataProcessingService.Core.Domain.Events;
using DataProcessingService.Core.Interfaces.Messaging;
using MediatR;

namespace DataProcessingService.Infrastructure.Data;

public class AppDbContext : DbContext
{
    private readonly IMediator _mediator;
    private readonly IEventBus _eventBus;
    
    public DbSet<DataSource> DataSources => Set<DataSource>();
    public DbSet<DataPipeline> DataPipelines => Set<DataPipeline>();
    public DbSet<PipelineExecution> PipelineExecutions => Set<PipelineExecution>();
    public DbSet<ExecutionMetric> ExecutionMetrics => Set<ExecutionMetric>();
    
    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        IMediator mediator,
        IEventBus eventBus) : base(options)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
    }
    
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Audit logic
        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTimeOffset.UtcNow;
                    entry.Entity.CreatedBy = GetCurrentUser();
                    break;
                case EntityState.Modified:
                    entry.Entity.LastModifiedAt = DateTimeOffset.UtcNow;
                    entry.Entity.LastModifiedBy = GetCurrentUser();
                    break;
            }
        }
        
        var result = await base.SaveChangesAsync(cancellationToken);
        
        // Dispatch domain events
        await DispatchDomainEventsAsync(cancellationToken);
        
        return result;
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Apply EF configurations from assembly
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
    
    private async Task DispatchDomainEventsAsync(CancellationToken cancellationToken)
    {
        var entities = ChangeTracker
            .Entries<Entity>()
            .Where(e => e.Entity.DomainEvents.Any())
            .Select(e => e.Entity)
            .ToList();
        
        var domainEvents = entities
            .SelectMany(e => e.DomainEvents)
            .ToList();
        
        entities.ForEach(e => e.ClearDomainEvents());
        
        foreach (var domainEvent in domainEvents)
        {
            await _mediator.Publish(domainEvent, cancellationToken);
            await _eventBus.PublishDomainEventAsync(domainEvent, cancellationToken);
        }
    }
    
    private string GetCurrentUser()
    {
        // In a real application, this would get the current user from a context
        // For now, we'll return a placeholder
        return "system";
    }
}

## src/DataProcessingService.Infrastructure/Data/Configurations/DataSourceConfiguration.cs
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;
using DataProcessingService.Core.Domain.Entities;

namespace DataProcessingService.Infrastructure.Data.Configurations;

public class DataSourceConfiguration : IEntityTypeConfiguration<DataSource>
{
    public void Configure(EntityTypeBuilder<DataSource> builder)
    {
        builder.HasKey(ds => ds.Id);
        
        builder.Property(ds => ds.Name)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(ds => ds.ConnectionString)
            .IsRequired();
        
        builder.Property(ds => ds.Type)
            .IsRequired();
        
        builder.Property(ds => ds.Schema)
            .HasMaxLength(100);
        
        builder.Property(ds => ds.IsActive)
            .IsRequired();
        
        builder.Property(ds => ds.LastSyncTime)
            .IsRequired();
        
        builder.Property(ds => ds.Properties)
            .HasConversion(
                p => JsonSerializer.Serialize(p, new JsonSerializerOptions()),
                p => p == null 
                    ? new Dictionary<string, string>() 
                    : JsonSerializer.Deserialize<Dictionary<string, string>>(p, new JsonSerializerOptions()) 
                      ?? new Dictionary<string, string>());
        
        builder.HasMany(ds => ds.DataPipelines)
            .WithOne(dp => dp.Source)
            .HasForeignKey(dp => dp.SourceId)
            .OnDelete(DeleteBehavior.Restrict);
        
        builder.HasIndex(ds => ds.Name)
            .IsUnique();
    }
}

## src/DataProcessingService.Infrastructure/Data/Configurations/DataPipelineConfiguration.cs
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;
using DataProcessingService.Core.Domain.Entities;
using DataProcessingService.Core.Domain.ValueObjects;

namespace DataProcessingService.Infrastructure.Data.Configurations;

public class DataPipelineConfiguration : IEntityTypeConfiguration<DataPipeline>
{
    public void Configure(EntityTypeBuilder<DataPipeline> builder)
    {
        builder.HasKey(dp => dp.Id);
        
        builder.Property(dp => dp.Name)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(dp => dp.Description)
            .IsRequired()
            .HasMaxLength(500);
        
        builder.Property(dp => dp.Type)
            .IsRequired();
        
        builder.Property(dp => dp.Status)
            .IsRequired();
        
        builder.Property(dp => dp.Schedule)
            .HasConversion(
                s => JsonSerializer.Serialize(s, new JsonSerializerOptions()),
                s => s == null 
                    ? null
                    : JsonSerializer.Deserialize<ExecutionSchedule>(s, new JsonSerializerOptions()));
        
        builder.Property(dp => dp.TransformationRules)
            .HasConversion(
                tr => JsonSerializer.Serialize(tr, new JsonSerializerOptions()),
                tr => tr == null 
                    ? TransformationRules.Empty
                    : JsonSerializer.Deserialize<TransformationRules>(tr, new JsonSerializerOptions()) 
                      ?? TransformationRules.Empty);
        
        builder.HasOne(dp => dp.Source)
            .WithMany(ds => ds.DataPipelines)
            .HasForeignKey(dp => dp.SourceId)
            .OnDelete(DeleteBehavior.Restrict);
        
        builder.HasOne(dp => dp.Destination)
            .WithMany()
            .HasForeignKey(dp => dp.DestinationId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        
        builder.HasMany(dp => dp.Executions)
            .WithOne(pe => pe.Pipeline)
            .HasForeignKey(pe => pe.PipelineId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasIndex(dp => dp.Name)
            .IsUnique();
    }
}

## src/DataProcessingService.Infrastructure/Data/Configurations/PipelineExecutionConfiguration.cs
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DataProcessingService.Core.Domain.Entities;

namespace DataProcessingService.Infrastructure.Data.Configurations;

public class PipelineExecutionConfiguration : IEntityTypeConfiguration<PipelineExecution>
{
    public void Configure(EntityTypeBuilder<PipelineExecution> builder)
    {
        builder.HasKey(pe => pe.Id);
        
        builder.Property(pe => pe.StartTime)
            .IsRequired();
        
        builder.Property(pe => pe.Status)
            .IsRequired();
        
        builder.Property(pe => pe.ProcessedRecords)
            .IsRequired();
        
        builder.Property(pe => pe.FailedRecords)
            .IsRequired();
        
        builder.HasOne(pe => pe.Pipeline)
            .WithMany(dp => dp.Executions)
            .HasForeignKey(pe => pe.PipelineId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasMany(pe => pe.Metrics)
            .WithOne()
            .HasForeignKey("PipelineExecutionId")
            .OnDelete(DeleteBehavior.Cascade);
    }
}

## src/DataProcessingService.Infrastructure/Data/Configurations/ExecutionMetricConfiguration.cs
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DataProcessingService.Core.Domain.Entities;

namespace DataProcessingService.Infrastructure.Data.Configurations;

public class ExecutionMetricConfiguration : IEntityTypeConfiguration<ExecutionMetric>
{
    public void Configure(EntityTypeBuilder<ExecutionMetric> builder)
    {
        builder.HasKey(em => em.Id);
        
        builder.Property(em => em.Name)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(em => em.Value)
            .IsRequired();
        
        builder.Property(em => em.Type)
            .IsRequired();
        
        builder.Property(em => em.Timestamp)
            .IsRequired();
        
        builder.Property<Guid>("PipelineExecutionId")
            .IsRequired();
    }
}

## src/DataProcessingService.Infrastructure/Data/Repositories/RepositoryBase.cs
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DataProcessingService.Core.Domain.Entities.Base;
using DataProcessingService.Core.Interfaces.Repositories;

namespace DataProcessingService.Infrastructure.Data.Repositories;

public abstract class RepositoryBase<TEntity> : IRepository<TEntity> where TEntity : Entity
{
    protected readonly AppDbContext DbContext;
    protected readonly DbSet<TEntity> DbSet;
    
    protected RepositoryBase(AppDbContext dbContext)
    {
        DbContext = dbContext;
        DbSet = dbContext.Set<TEntity>();
    }
    
    public virtual async Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbSet.FindAsync(new object[] { id }, cancellationToken);
    }
    
    public virtual async Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet.ToListAsync(cancellationToken);
    }
    
    public virtual async Task<IReadOnlyList<TEntity>> FindAsync(
        Expression<Func<TEntity, bool>> predicate, 
        CancellationToken cancellationToken = default)
    {
        return await DbSet.Where(predicate).ToListAsync(cancellationToken);
    }
    
    public virtual async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        await DbSet.AddAsync(entity, cancellationToken);
        await DbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }
    
    public virtual async Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        DbContext.Entry(entity).State = EntityState.Modified;
        await DbContext.SaveChangesAsync(cancellationToken);
    }
    
    public virtual async Task DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        DbSet.Remove(entity);
        await DbContext.SaveChangesAsync(cancellationToken);
    }
    
    public virtual async Task DeleteByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetByIdAsync(id, cancellationToken);
        if (entity != null)
        {
            await DeleteAsync(entity, cancellationToken);
        }
    }
    
    public virtual async Task<int> CountAsync(
        Expression<Func<TEntity, bool>>? predicate = null, 
        CancellationToken cancellationToken = default)
    {
        return predicate == null
            ? await DbSet.CountAsync(cancellationToken)
            : await DbSet.CountAsync(predicate, cancellationToken);
    }
    
    public virtual async Task<bool> ExistsAsync(
        Expression<Func<TEntity, bool>> predicate, 
        CancellationToken cancellationToken = default)
    {
        return await DbSet.AnyAsync(predicate, cancellationToken);
    }
}

## src/DataProcessingService.Infrastructure/Data/Repositories/DataSourceRepository.cs
```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DataProcessingService.Core.Domain.Entities;
using DataProcessingService.Core.Domain.Enums;
using DataProcessingService.Core.Interfaces.Repositories;

namespace DataProcessingService.Infrastructure.Data.Repositories;

public class DataSourceRepository : RepositoryBase<DataSource>, IDataSourceRepository
{
    public DataSourceRepository(AppDbContext dbContext) : base(dbContext)
    {
    }
    
    public async Task<IReadOnlyList<DataSource>> GetActiveDataSourcesAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(ds => ds.IsActive)
            .ToListAsync(cancellationToken);
    }
    
    public async Task<IReadOnlyList<DataSource>> GetDataSourcesByTypeAsync(
        DataSourceType type, 
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(ds => ds.Type == type)
            .ToListAsync(cancellationToken);
    }
    
    public async Task<DataSource?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .FirstOrDefaultAsync(ds => ds.Name == name, cancellationToken);
    }
}

## src/DataProcessingService.Infrastructure/Data/Repositories/DataPipelineRepository.cs
```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DataProcessingService.Core.Domain.Entities;
using DataProcessingService.Core.Domain.Enums;
using DataProcessingService.Core.Interfaces.Repositories;

namespace DataProcessingService.Infrastructure.Data.Repositories;

public class DataPipelineRepository : RepositoryBase<DataPipeline>, IDataPipelineRepository
{
    public DataPipelineRepository(AppDbContext dbContext) : base(dbContext)
    {
    }
    
    public async Task<IReadOnlyList<DataPipeline>> GetPipelinesByStatusAsync(
        PipelineStatus status, 
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(dp => dp.Status == status)
            .ToListAsync(cancellationToken);
    }
    
    public async Task<IReadOnlyList<DataPipeline>> GetPipelinesByTypeAsync(
        PipelineType type, 
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(dp => dp.Type == type)
            .ToListAsync(cancellationToken);
    }
    
    public async Task<IReadOnlyList<DataPipeline>> GetPipelinesDueForExecutionAsync(
        DateTimeOffset currentTime, 
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(dp => dp.Status == PipelineStatus.Idle && 
                    dp.NextExecutionTime.HasValue && 
                    dp.NextExecutionTime.Value <= currentTime)
            .ToListAsync(cancellationToken);
    }
    
    public async Task<IReadOnlyList<DataPipeline>> GetPipelinesBySourceIdAsync(
        Guid sourceId, 
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(dp => dp.SourceId == sourceId)
            .ToListAsync(cancellationToken);
    }
    
    public async Task<IReadOnlyList<DataPipeline>> GetPipelinesByDestinationIdAsync(
        Guid destinationId, 
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(dp => dp.DestinationId == destinationId)
            .ToListAsync(cancellationToken);
    }
    
    public async Task<DataPipeline?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .FirstOrDefaultAsync(dp => dp.Name == name, cancellationToken);
    }
    
    public async Task<DataPipeline?> GetWithRelatedDataAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(dp => dp.Source)
            .Include(dp => dp.Destination)
            .FirstOrDefaultAsync(dp => dp.Id == id, cancellationToken);
    }
}

## src/DataProcessingService.Infrastructure/Data/Repositories/PipelineExecutionRepository.cs
```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DataProcessingService.Core.Domain.Entities;
using DataProcessingService.Core.Domain.Enums;
using DataProcessingService.Core.Interfaces.Repositories;

namespace DataProcessingService.Infrastructure.Data.Repositories;

public class PipelineExecutionRepository : RepositoryBase<PipelineExecution>, IPipelineExecutionRepository
{
    public PipelineExecutionRepository(AppDbContext dbContext) : base(dbContext)
    {
    }
    
    public async Task<IReadOnlyList<PipelineExecution>> GetExecutionsByPipelineIdAsync(
        Guid pipelineId, 
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(pe => pe.PipelineId == pipelineId)
            .OrderByDescending(pe => pe.StartTime)
            .ToListAsync(cancellationToken);
    }
    
    public async Task<IReadOnlyList<PipelineExecution>> GetExecutionsByStatusAsync(
        ExecutionStatus status, 
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(pe => pe.Status == status)
            .OrderByDescending(pe => pe.StartTime)
            .ToListAsync(cancellationToken);
    }
    
    public async Task<PipelineExecution?> GetLatestExecutionByPipelineIdAsync(
        Guid pipelineId, 
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(pe => pe.PipelineId == pipelineId)
            .OrderByDescending(pe => pe.StartTime)
            .FirstOrDefaultAsync(cancellationToken);
    }
    
    public async Task<IReadOnlyList<PipelineExecution>> GetExecutionsInDateRangeAsync(
        DateTimeOffset start, 
        DateTimeOffset end, 
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(pe => pe.StartTime >= start && pe.StartTime <= end)
            .OrderByDescending(pe => pe.StartTime)
            .ToListAsync(cancellationToken);
    }
}

## src/DataProcessingService.Infrastructure/Messaging/InMemoryMessagePublisher.cs
```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.Core.Interfaces.Messaging;
using Microsoft.Extensions.Logging;

namespace DataProcessingService.Infrastructure.Messaging;

public class InMemoryMessagePublisher : IMessagePublisher
{
    private static readonly Dictionary<string, List<object>> Messages = new();
    
    private readonly ILogger<InMemoryMessagePublisher> _logger;
    
    public InMemoryMessagePublisher(ILogger<InMemoryMessagePublisher> logger)
    {
        _logger = logger;
    }
    
    public Task PublishAsync<TMessage>(
        string topicName,
        TMessage message,
        CancellationToken cancellationToken = default) 
        where TMessage : class
    {
        return PublishAsync(topicName, message, null, null, cancellationToken);
    }
    
    public Task PublishAsync<TMessage>(
        string topicName,
        TMessage message,
        string? partitionKey = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default) 
        where TMessage : class
    {
        if (!Messages.ContainsKey(topicName))
        {
            Messages[topicName] = new List<object>();
        }
        
        Messages[topicName].Add(message);
        
        _logger.LogInformation("Published message to topic {TopicName}", topicName);
        
        return Task.CompletedTask;
    }
}

## src/DataProcessingService.Infrastructure/Messaging/InMemoryMessageConsumer.cs
```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.Core.Interfaces.Messaging;
using Microsoft.Extensions.Logging;

namespace DataProcessingService.Infrastructure.Messaging;

public class InMemoryMessageConsumer : IMessageConsumer
{
    private readonly Dictionary<string, object> _subscriptions = new();
    private readonly ILogger<InMemoryMessageConsumer> _logger;
    
    public InMemoryMessageConsumer(ILogger<InMemoryMessageConsumer> logger)
    {
        _logger = logger;
    }
    
    public Task SubscribeAsync<TMessage>(
        string topicName,
        string subscriptionName,
        Func<TMessage, IDictionary<string, string>, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default) 
        where TMessage : class
    {
        var key = $"{topicName}:{subscriptionName}";
        
        if (_subscriptions.ContainsKey(key))
        {
            throw new InvalidOperationException($"Subscription {subscriptionName} to topic {topicName} already exists");
        }
        
        _subscriptions[key] = handler;
        
        _logger.LogInformation("Subscribed to topic {TopicName} with subscription {SubscriptionName}", 
            topicName, subscriptionName);
        
        return Task.CompletedTask;
    }
    
    public Task UnsubscribeAsync(
        string topicName,
        string subscriptionName,
        CancellationToken cancellationToken = default)
    {
        var key = $"{topicName}:{subscriptionName}";
        
        if (_subscriptions.ContainsKey(key))
        {
            _subscriptions.Remove(key);
            
            _logger.LogInformation("Unsubscribed from topic {TopicName} with subscription {SubscriptionName}", 
                topicName, subscriptionName);
        }
        
        return Task.CompletedTask;
    }
}

## src/DataProcessingService.Infrastructure/Messaging/InMemoryEventBus.cs
```csharp
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.Core.Domain.Events;
using DataProcessingService.Core.Interfaces.Messaging;
using Microsoft.Extensions.Logging;

namespace DataProcessingService.Infrastructure.Messaging;

public class InMemoryEventBus : IEventBus
{
    private readonly IMessagePublisher _messagePublisher;
    private readonly ILogger<InMemoryEventBus> _logger;
    
    public InMemoryEventBus(
        IMessagePublisher messagePublisher,
        ILogger<InMemoryEventBus> logger)
    {
        _messagePublisher = messagePublisher;
        _logger = logger;
    }
    
    public async Task PublishDomainEventAsync<TEvent>(
        TEvent domainEvent,
        CancellationToken cancellationToken = default) 
        where TEvent : DomainEvent
    {
        var eventType = domainEvent.GetType().Name;
        
        _logger.LogInformation("Publishing domain event {EventType}", eventType);
        
        await _messagePublisher.PublishAsync($"DomainEvents.{eventType}", domainEvent, cancellationToken);
    }
    
    public async Task PublishIntegrationEventAsync<TEvent>(
        TEvent integrationEvent,
        CancellationToken cancellationToken = default) 
        where TEvent : class
    {
        var eventType = integrationEvent.GetType().Name;
        
        _logger.LogInformation("Publishing integration event {EventType}", eventType);
        
        await _messagePublisher.PublishAsync($"IntegrationEvents.{eventType}", integrationEvent, cancellationToken);
    }
}

## src/DataProcessingService.Infrastructure/Messaging/Kafka/KafkaConfiguration.cs
```csharp
namespace DataProcessingService.Infrastructure.Messaging.Kafka;

public class KafkaConfiguration
{
    public string[] BootstrapServers { get; set; } = Array.Empty<string>();
    public string GroupId { get; set; } = "data-processing-service";
    public bool EnableAutoCommit { get; set; } = false;
    public int AutoCommitIntervalMs { get; set; } = 5000;
    public string AutoOffsetReset { get; set; } = "earliest";
    public int SessionTimeoutMs { get; set; } = 30000;
    public int MaxPollIntervalMs { get; set; } = 300000;
    public bool EnablePartitionEof { get; set; } = false;
}

## src/DataProcessingService.Infrastructure/Messaging/Kafka/KafkaMessagePublisher.cs
```csharp
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using DataProcessingService.Core.Interfaces.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DataProcessingService.Infrastructure.Messaging.Kafka;

public class KafkaMessagePublisher : IMessagePublisher, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaMessagePublisher> _logger;
    private bool _disposed;
    
    public KafkaMessagePublisher(
        IOptions<KafkaConfiguration> kafkaOptions,
        ILogger<KafkaMessagePublisher> logger)
    {
        _logger = logger;
        
        var config = new ProducerConfig
        {
            BootstrapServers = string.Join(",", kafkaOptions.Value.BootstrapServers),
            Acks = Acks.All
        };
        
        _producer = new ProducerBuilder<string, string>(config).Build();
    }
    
    public async Task PublishAsync<TMessage>(
        string topicName,
        TMessage message,
        CancellationToken cancellationToken = default) 
        where TMessage : class
    {
        await PublishAsync(topicName, message, null, null, cancellationToken);
    }
    
    public async Task PublishAsync<TMessage>(
        string topicName,
        TMessage message,
        string? partitionKey = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default) 
        where TMessage : class
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(KafkaMessagePublisher));
        }
        
        var key = partitionKey ?? Guid.NewGuid().ToString();
        var value = JsonSerializer.Serialize(message);
        
        var kafkaMessage = new Message<string, string>
        {
            Key = key,
            Value = value
        };
        
        if (headers != null)
        {
            kafkaMessage.Headers = new Headers();
            
            foreach (var header in headers)
            {
                kafkaMessage.Headers.Add(header.Key, System.Text.Encoding.UTF8.GetBytes(header.Value));
            }
        }
        
        try
        {
            var result = await _producer.ProduceAsync(topicName, kafkaMessage, cancellationToken);
            
            _logger.LogInformation(
                "Published message to topic {TopicName} with partition {Partition} and offset {Offset}",
                topicName, result.Partition.Value, result.Offset.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing message to topic {TopicName}", topicName);
            throw;
        }
    }
    
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        
        _producer.Dispose();
        _disposed = true;
        
        GC.SuppressFinalize(this);
    }
}

## src/DataProcessingService.Infrastructure/Messaging/Kafka/KafkaMessageConsumer.cs
```csharp
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using DataProcessingService.Core.Interfaces.Messaging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DataProcessingService.Infrastructure.Messaging.Kafka;

public class KafkaMessageConsumer : IMessageConsumer, IHostedService, IDisposable
{
    private readonly Dictionary<string, KafkaConsumerInfo> _consumers = new();
    private readonly KafkaConfiguration _kafkaConfig;
    private readonly ILogger<KafkaMessageConsumer> _logger;
    private bool _disposed;
    
    public KafkaMessageConsumer(
        IOptions<KafkaConfiguration> kafkaOptions,
        ILogger<KafkaMessageConsumer> logger)
    {
        _kafkaConfig = kafkaOptions.Value;
        _logger = logger;
    }
    
    public async Task SubscribeAsync<TMessage>(
        string topicName,
        string subscriptionName,
        Func<TMessage, IDictionary<string, string>, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default) 
        where TMessage : class
    {
        var key = $"{topicName}:{subscriptionName}";
        
        if (_consumers.ContainsKey(key))
        {
            throw new InvalidOperationException($"Subscription {subscriptionName} to topic {topicName} already exists");
        }
        
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = string.Join(",", _kafkaConfig.BootstrapServers),
            GroupId = $"{_kafkaConfig.GroupId}-{subscriptionName}",
            EnableAutoCommit = _kafkaConfig.EnableAutoCommit,
            AutoCommitIntervalMs = _kafkaConfig.AutoCommitIntervalMs,
            AutoOffsetReset = Enum.Parse<AutoOffsetReset>(_kafkaConfig.AutoOffsetReset, true),
            SessionTimeoutMs = _kafkaConfig.SessionTimeoutMs,
            MaxPollIntervalMs = _kafkaConfig.MaxPollIntervalMs,
            EnablePartitionEof = _kafkaConfig.EnablePartitionEof
        };
        
        var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        consumer.Subscribe(topicName);
        
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        
        var consumerTask = Task.Run(async () =>
        {
            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        var result = consumer.Consume(cts.Token);
                        
                        if (result == null || result.IsPartitionEOF)
                        {
                            continue;
                        }
                        
                        var headers = new Dictionary<string, string>();
                        
                        foreach (var header in result.Message.Headers)
                        {
                            headers[header.Key] = Encoding.UTF8.GetString(header.GetValueBytes());
                        }
                        
                        var message = JsonSerializer.Deserialize<TMessage>(result.Message.Value);
                        
                        if (message != null)
                        {
                            await handler(message, headers, cts.Token);
                            
                            if (!_kafkaConfig.EnableAutoCommit)
                            {
                                consumer.Commit(result);
                            }
                        }
                    }
                    catch (ConsumeException ex)
                    {
                        _logger.LogError(ex, "Error consuming message from topic {TopicName}", topicName);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing message from topic {TopicName}", topicName);
                    }
                }
            }
            finally
            {
                consumer.Close();
            }
        }, cts.Token);
        
        _consumers[key] = new KafkaConsumerInfo(consumer, cts, consumerTask);
        
        _logger.LogInformation("Subscribed to topic {TopicName} with subscription {SubscriptionName}", 
            topicName, subscriptionName);
    }
    
    public async Task UnsubscribeAsync(
        string topicName,
        string subscriptionName,
        CancellationToken cancellationToken = default)
    {
        var key = $"{topicName}:{subscriptionName}";
        
        if (_consumers.TryGetValue(key, out var consumerInfo))
        {
            consumerInfo.CancellationTokenSource.Cancel();
            
            try
            {
                await consumerInfo.ConsumerTask.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("Timeout waiting for consumer task to complete for {TopicName}:{SubscriptionName}", 
                    topicName, subscriptionName);
            }
            
            consumerInfo.Consumer.Dispose();
            consumerInfo.CancellationTokenSource.Dispose();
            
            _consumers.Remove(key);
            
            _logger.LogInformation("Unsubscribed from topic {TopicName} with subscription {SubscriptionName}", 
                topicName, subscriptionName);
        }
    }
    
    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
    
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        var keys = _consumers.Keys.ToList();
        
        foreach (var key in keys)
        {
            var parts = key.Split(':');
            await UnsubscribeAsync(parts[0], parts[1], cancellationToken);
        }
    }
    
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        
        foreach (var consumerInfo in _consumers.Values)
        {
            consumerInfo.CancellationTokenSource.Cancel();
            consumerInfo.Consumer.Dispose();
            consumerInfo.CancellationTokenSource.Dispose();
        }
        
        _consumers.Clear();
        _disposed = true;
        
        GC.SuppressFinalize(this);
    }
    
    private class KafkaConsumerInfo
    {
        public IConsumer<string, string> Consumer { get; }
        public CancellationTokenSource CancellationTokenSource { get; }
        public Task ConsumerTask { get; }
        
        public KafkaConsumerInfo(
            IConsumer<string, string> consumer,
            CancellationTokenSource cancellationTokenSource,
            Task consumerTask)
        {
            Consumer = consumer;
            CancellationTokenSource = cancellationTokenSource;
            ConsumerTask = consumerTask;
        }
    }
}

## src/DataProcessingService.Infrastructure/Messaging/Kafka/KafkaEventBus.cs
```csharp
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.Core.Domain.Events;
using DataProcessingService.Core.Interfaces.Messaging;
using Microsoft.Extensions.Logging;

namespace DataProcessingService.Infrastructure.Messaging.Kafka;

public class KafkaEventBus : IEventBus
{
    private readonly IMessagePublisher _messagePublisher;
    private readonly ILogger<KafkaEventBus> _logger;
    
    public KafkaEventBus(
        IMessagePublisher messagePublisher,
        ILogger<KafkaEventBus> logger)
    {
        _messagePublisher = messagePublisher;
        _logger = logger;
    }
    
    public async Task PublishDomainEventAsync<TEvent>(
        TEvent domainEvent,
        CancellationToken cancellationToken = default) 
        where TEvent : DomainEvent
    {
        var eventType = domainEvent.GetType().Name;
        var topicName = $"DomainEvents.{eventType}";
        
        _logger.LogInformation("Publishing domain event {EventType} to topic {TopicName}", 
            eventType, topicName);
        
        await _messagePublisher.PublishAsync(
            topicName,
            domainEvent,
            domainEvent.Id.ToString(),
            new Dictionary<string, string>
            {
                ["EventType"] = eventType,
                ["EventId"] = domainEvent.Id.ToString(),
                ["OccurredOn"] = domainEvent.OccurredOn.ToString("o")
            },
            cancellationToken);
    }
    
    public async Task PublishIntegrationEventAsync<TEvent>(
        TEvent integrationEvent,
        CancellationToken cancellationToken = default) 
        where TEvent : class
    {
        var eventType = integrationEvent.GetType().Name;
        var topicName = $"IntegrationEvents.{eventType}";
        
        _logger.LogInformation("Publishing integration event {EventType} to topic {TopicName}", 
            eventType, topicName);
        
        // Assuming integration events have Id and OccurredOn properties
        var eventId = integrationEvent.GetType().GetProperty("Id")?.GetValue(integrationEvent)?.ToString() 
                      ?? Guid.NewGuid().ToString();
        
        var occurredOn = integrationEvent.GetType().GetProperty("OccurredOn")?.GetValue(integrationEvent)?.ToString() 
                         ?? DateTimeOffset.UtcNow.ToString("o");
        
        await _messagePublisher.PublishAsync(
            topicName,
            integrationEvent,
            eventId,
            new Dictionary<string, string>
            {
                ["EventType"] = eventType,
                ["EventId"] = eventId,
                ["OccurredOn"] = occurredOn
            },
            cancellationToken);
    }
}
