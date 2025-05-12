using System;
using System.Threading;
using System.Threading.Tasks;
using CachingService.Core.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace CachingService.API.Health
{
    /// <summary>
    /// Health check for cache providers
    /// </summary>
    public class CacheHealthCheck : IHealthCheck
    {
        private readonly ICacheManager _cacheManager;
        private readonly ILogger<CacheHealthCheck> _logger;
        
        public CacheHealthCheck(
            ICacheManager cacheManager,
            ILogger<CacheHealthCheck> logger)
        {
            _cacheManager = cacheManager;
            _logger = logger;
        }
        
        /// <inheritdoc />
        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Try to set and get a test value
                const string key = "health-check";
                const string value = "ok";
                const string region = "health";
                
                await _cacheManager.SetAsync(key, value, TimeSpan.FromSeconds(5), region, null, cancellationToken);
                var result = await _cacheManager.GetAsync<string>(key, region, cancellationToken);
                
                if (result != value)
                {
                    return HealthCheckResult.Degraded(
                        description: "Cache is not storing or retrieving values correctly");
                }
                
                // Get cache statistics
                var stats = await _cacheManager.GetStatisticsAsync(cancellationToken);
                
                // Check hit ratio if there have been enough requests
                if (stats.Hits + stats.Misses > 100)
                {
                    if (stats.HitRatio < 0.5)
                    {
                        return HealthCheckResult.Degraded(
                            description: $"Cache hit ratio is low: {stats.HitRatio:P2}");
                    }
                }
                
                // Check memory usage if available
                if (stats.MemoryUsage != null)
                {
                    if (stats.MemoryUsage.UsagePercentage > 90)
                    {
                        return HealthCheckResult.Degraded(
                            description: $"Cache memory usage is high: {stats.MemoryUsage.UsagePercentage:F1}%");
                    }
                }
                
                return HealthCheckResult.Healthy(
                    description: $"Cache is healthy. Items: {stats.ItemCount}, Hit ratio: {stats.HitRatio:P2}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking cache health");
                
                return HealthCheckResult.Unhealthy(
                    description: "Error checking cache health",
                    exception: ex);
            }
        }
    }
}
