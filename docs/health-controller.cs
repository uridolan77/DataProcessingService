using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CachingService.Core.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace CachingService.API.Controllers
{
    [ApiController]
    [Route("api/health")]
    public class HealthController : ControllerBase
    {
        private readonly HealthCheckService _healthCheckService;
        private readonly ILogger<HealthController> _logger;
        private readonly ICacheManager _cacheManager;
        
        public HealthController(
            HealthCheckService healthCheckService,
            ILogger<HealthController> logger,
            ICacheManager cacheManager)
        {
            _healthCheckService = healthCheckService;
            _logger = logger;
            _cacheManager = cacheManager;
        }
        
        /// <summary>
        /// Gets the health status of the service
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Health check result</returns>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<ActionResult<HealthCheckResponse>> GetHealthAsync(
            CancellationToken cancellationToken)
        {
            var report = await _healthCheckService.CheckHealthAsync(cancellationToken);
            
            var response = new HealthCheckResponse
            {
                Status = report.Status.ToString(),
                Checks = new List<HealthCheckItem>(),
                TotalDuration = report.TotalDuration
            };
            
            foreach (var entry in report.Entries)
            {
                response.Checks.Add(new HealthCheckItem
                {
                    Name = entry.Key,
                    Status = entry.Value.Status.ToString(),
                    Duration = entry.Value.Duration,
                    Description = entry.Value.Description,
                    Error = entry.Value.Exception?.Message
                });
            }
            
            var statusCode = report.Status == HealthStatus.Healthy ? 
                StatusCodes.Status200OK : 
                StatusCodes.Status503ServiceUnavailable;
                
            return StatusCode(statusCode, response);
        }
        
        /// <summary>
        /// Performs a liveness check
        /// </summary>
        /// <returns>Status code indicating whether the service is alive</returns>
        [HttpGet("liveness")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public ActionResult Liveness()
        {
            // A simple liveness check just verifies that the application is running
            // and can handle HTTP requests
            return Ok();
        }
        
        /// <summary>
        /// Performs a readiness check
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Status code indicating whether the service is ready to handle requests</returns>
        [HttpGet("readiness")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<ActionResult> ReadinessAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Check if the cache is accessible
                const string key = "health-check";
                const string value = "ok";
                
                // Try to set and get a test value
                await _cacheManager.SetAsync(key, value, TimeSpan.FromSeconds(5), null, null, cancellationToken);
                var result = await _cacheManager.GetAsync<string>(key, null, cancellationToken);
                
                if (result != value)
                {
                    _logger.LogWarning("Readiness check failed: Cache not working correctly");
                    return StatusCode(StatusCodes.Status503ServiceUnavailable);
                }
                
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Readiness check failed");
                return StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
        }
        
        /// <summary>
        /// Health check response
        /// </summary>
        public class HealthCheckResponse
        {
            /// <summary>
            /// Overall health status
            /// </summary>
            public string Status { get; set; } = default!;
            
            /// <summary>
            /// Individual health checks
            /// </summary>
            public List<HealthCheckItem> Checks { get; set; } = new();
            
            /// <summary>
            /// Total duration of the health check
            /// </summary>
            public TimeSpan TotalDuration { get; set; }
        }
        
        /// <summary>
        /// Individual health check item
        /// </summary>
        public class HealthCheckItem
        {
            /// <summary>
            /// Name of the health check
            /// </summary>
            public string Name { get; set; } = default!;
            
            /// <summary>
            /// Status of the health check
            /// </summary>
            public string Status { get; set; } = default!;
            
            /// <summary>
            /// Duration of the health check
            /// </summary>
            public TimeSpan Duration { get; set; }
            
            /// <summary>
            /// Description of the health check
            /// </summary>
            public string? Description { get; set; }
            
            /// <summary>
            /// Error message if the health check failed
            /// </summary>
            public string? Error { get; set; }
        }
    }
}
