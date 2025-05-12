using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DataProcessingService.Core.Domain.Entities;
using DataProcessingService.Core.Domain.Entities.Base;
using DataProcessingService.Core.Domain.Events;
using DataProcessingService.Core.Domain.DataQuality;
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

    // Data Quality entities
    public DbSet<DataSchema> DataSchemas => Set<DataSchema>();
    public DbSet<DataQualityRule> DataQualityRules => Set<DataQualityRule>();
    public DbSet<DataProfile> DataProfiles => Set<DataProfile>();
    public DbSet<DataLineage> DataLineages => Set<DataLineage>();
    public DbSet<DataContract> DataContracts => Set<DataContract>();

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
