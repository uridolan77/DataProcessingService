using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using DataProcessingService.API.DTOs;

namespace DataProcessingService.API.Middlewares;

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;
    
    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }
    
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }
    
    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var code = HttpStatusCode.InternalServerError; // 500 if unexpected
        var message = "An unexpected error occurred";
        
        if (exception is KeyNotFoundException)
        {
            code = HttpStatusCode.NotFound; // 404
            message = exception.Message;
        }
        else if (exception is InvalidOperationException)
        {
            code = HttpStatusCode.BadRequest; // 400
            message = exception.Message;
        }
        else if (exception is ArgumentException)
        {
            code = HttpStatusCode.BadRequest; // 400
            message = exception.Message;
        }
        
        var response = ApiResponse<object>.ErrorResponse(message);
        
        var result = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)code;
        
        return context.Response.WriteAsync(result);
    }
}
