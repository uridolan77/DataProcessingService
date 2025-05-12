using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using DataProcessingService.API.Middlewares;
using DataProcessingService.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

var builder = WebApplication.CreateBuilder(args);

// Configure services
ConfigureServices(builder.Services, builder.Configuration, builder.Environment);

var app = builder.Build();

// Configure the HTTP request pipeline
ConfigureApp(app, app.Environment);

app.Run();

void ConfigureServices(IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
{
    // Add infrastructure services
    services.AddInfrastructure(configuration);

    // Add controllers
    services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

    // Add GraphQL
    services
        .AddGraphQLServer()
        .AddQueryType<DataProcessingService.API.GraphQL.Query>()
        .AddMutationType<DataProcessingService.API.GraphQL.Mutation>()
        .AddFiltering()
        .AddSorting()
        .AddProjections();

    // Add API Versioning
    services.AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
        options.ApiVersionReader = ApiVersionReader.Combine(
            new UrlSegmentApiVersionReader(),
            new HeaderApiVersionReader("X-API-Version"),
            new QueryStringApiVersionReader("api-version"));
    });

    services.AddVersionedApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

    // Add Swagger
    services.AddEndpointsApiExplorer();
    services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
    services.AddSwaggerGen(options =>
    {
        // Add XML comments if available
        var xmlFile = "DataProcessingService.API.xml";
        var xmlPath = System.IO.Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (System.IO.File.Exists(xmlPath))
        {
            options.IncludeXmlComments(xmlPath);
        }
    });

    // Add health checks
    services.AddHealthChecks()
        .AddCheck("database_health", () => HealthCheckResult.Healthy(), tags: new[] { "ready" });

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

    // Add rate limiting
    services.AddRateLimiter(options =>
    {
        options.GlobalLimiter = Microsoft.AspNetCore.RateLimiting.PartitionedRateLimiter.Create<Microsoft.AspNetCore.Http.HttpContext, string>(context =>
        {
            return Microsoft.AspNetCore.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? context.Request.Headers.Host.ToString(),
                factory: partition => new Microsoft.AspNetCore.RateLimiting.FixedWindowRateLimiterOptions
                {
                    AutoReplenishment = true,
                    PermitLimit = 100,
                    QueueLimit = 0,
                    Window = TimeSpan.FromMinutes(1)
                });
        });

        options.OnRejected = async (context, token) =>
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.HttpContext.Response.ContentType = "application/json";

            var response = new
            {
                success = false,
                message = "Too many requests. Please try again later.",
                retryAfter = context.Lease.TryGetMetadata(Microsoft.AspNetCore.RateLimiting.MetadataName.RetryAfter, out var retryAfter) ? retryAfter.TotalSeconds : null
            };

            await context.HttpContext.Response.WriteAsJsonAsync(response, token);
        };
    });

    // Add request logging
    services.AddHttpLogging(logging =>
    {
        logging.LoggingFields = Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.All;
        logging.RequestBodyLogLimit = 4096;
        logging.ResponseBodyLogLimit = 4096;
    });
}

void ConfigureApp(WebApplication app, IHostEnvironment environment)
{
    // Global error handling
    app.UseMiddleware<ErrorHandlingMiddleware>();

    // Development-specific middleware
    if (environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
        app.UseHttpLogging();
    }
    else
    {
        // Production-specific middleware
        app.UseHsts();
        app.UseHttpsRedirection();
    }

    // Configure Swagger
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();

        foreach (var description in provider.ApiVersionDescriptions)
        {
            options.SwaggerEndpoint(
                $"/swagger/{description.GroupName}/swagger.json",
                $"Data Processing Service API {description.GroupName}");
        }
    });

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

    // Enable rate limiting
    app.UseRateLimiter();

    // Enable routing and endpoints
    app.UseRouting();
    app.UseAuthorization();
    app.MapControllers();
    app.MapGraphQL();
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
