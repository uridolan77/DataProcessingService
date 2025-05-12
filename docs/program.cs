using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using CachingService.API.Health;
using CachingService.Core.Configuration;
using CachingService.Core.Events;
using CachingService.Core.Interfaces;
using CachingService.Infrastructure.Cache;
using CachingService.Infrastructure.Events;
using CachingService.Infrastructure.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Configure services
ConfigureServices(builder.Services, builder.Configuration, builder.Environment);

var app = builder.Build();

// Configure the HTTP request pipeline
ConfigureApp(app, app.Environment);

app.Run();

void ConfigureServices(IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
{
    // Add options
    services.Configure<CacheOptions>(configuration.GetSection("Cache"));
    
    // Add cache services
    services.AddSingleton<ICacheEventPublisher, InMemoryCacheEventPublisher>();
    services.AddSingleton<ICacheProvider, InMemoryCacheProvider>();
    
    // Add Redis cache provider if enabled
    if (configuration.GetValue<bool>("Cache:Redis:Enabled"))
    {
        services.AddSingleton<ICacheProvider, RedisCacheProvider>();
    }
    
    services.AddSingleton<ICacheManager, CacheManager>();
    
    // Add background services
    services.AddHostedService<CacheWarmerService>();
    services.AddHostedService<CacheMaintenanceService>();
    
    // Add controllers
    services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });
        
    // Add Swagger
    services.AddEndpointsApiExplorer();
    services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Caching Microservice API",
            Version = "v1",
            Description = "A dedicated microservice for managing cached data"
        });
        
        // Add XML comments if available
        var xmlFile = "CachingService.API.xml";
        var xmlPath = System.IO.Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (System.IO.File.Exists(xmlPath))
        {
            c.IncludeXmlComments(xmlPath);
        }
    });
    
    // Add health checks
    services.AddHealthChecks()
        .AddCheck<CacheHealthCheck>("cache_health", tags: new[] { "ready" });
        
    // Add CORS
    services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.WithOrigins(configuration.GetSection("AllowedOrigins").Get<string[]>() ?? Array.Empty<string>())
                .AllowAnyMethod()
                .AllowAnyHeader();
        });
    });
    
    // Add request logging
    services.AddHttpLogging(logging =>
    {
        logging.LoggingFields = Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.All;
        logging.RequestBodyLogLimit = 4096;
        logging.ResponseBodyLogLimit = 4096;
    });
}

void ConfigureApp(WebApplication app, IWebHostEnvironment environment)
{
    // Development-specific middleware
    if (environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
        app.UseSwagger();
        app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Caching Microservice API v1"));
        app.UseHttpLogging();
    }
    
    // Configure health checks
    app.UseHealthChecks("/health", new HealthCheckOptions
    {
        Predicate = _ => true,
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });
    
    app.UseHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready")
    });
    
    app.UseHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = _ => false // Liveness just checks if the app is running
    });
    
    // Enable CORS
    app.UseCors();
    
    // Enable routing and endpoints
    app.UseRouting();
    app.UseAuthorization();
    app.MapControllers();
}

// Helper class for health check UI response
public static class UIResponseWriter
{
    public static Task WriteHealthCheckUIResponse(HttpContext context, HealthReport healthReport)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        
        var response = new
        {
            Status = healthReport.Status.ToString(),
            Duration = healthReport.TotalDuration,
            Checks = healthReport.Entries.Select(entry => new
            {
                Name = entry.Key,
                Status = entry.Value.Status.ToString(),
                Duration = entry.Value.Duration,
                Description = entry.Value.Description,
                Error = entry.Value.Exception?.Message
            })
        };
        
        return context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
    }
}
