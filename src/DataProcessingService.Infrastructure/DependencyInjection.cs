using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using DataProcessingService.Core.Interfaces.Repositories;
using DataProcessingService.Core.Interfaces.Services;
using DataProcessingService.Core.Interfaces.Services.ETL;
using DataProcessingService.Core.Interfaces.Services.DataQuality;
using DataProcessingService.Core.Interfaces.Messaging;
using DataProcessingService.Infrastructure.Data;
using DataProcessingService.Infrastructure.Data.Repositories;
using DataProcessingService.Infrastructure.Messaging;
using DataProcessingService.Infrastructure.Messaging.Kafka;
using DataProcessingService.Infrastructure.Services;
using DataProcessingService.Infrastructure.Services.ETL;
using DataProcessingService.Infrastructure.Services.DataQuality;
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

        // Register Data Quality services
        services.AddScoped<ISchemaValidationService, SchemaValidationService>();
        services.AddScoped<IRepository<DataProcessingService.Core.Domain.DataQuality.DataSchema>, RepositoryBase<DataProcessingService.Core.Domain.DataQuality.DataSchema>>();

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
        services.AddMediatR(cfg => {
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
        });

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
