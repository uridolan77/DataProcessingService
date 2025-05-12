using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using CachingService.Contracts.Requests;
using CachingService.Contracts.Responses;
using CachingService.Core.Interfaces;
using CachingService.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace CachingService.API.Controllers
{
    [ApiController]
    [Route("api/cache")]
    public class CacheController : ControllerBase
    {
        private readonly ICacheManager _cacheManager;
        private readonly ILogger<CacheController> _logger;
        
        public CacheController(
            ICacheManager cacheManager,
            ILogger<CacheController> logger)
        {
            _cacheManager = cacheManager;
            _logger = logger;
        }
        
        /// <summary>
        /// Gets an item from the cache
        /// </summary>
        /// <param name="request">Get request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The cached item or null if not found</returns>
        [HttpPost("get")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<GetCacheItemResponse<JsonNode>>> GetAsync(
            [FromBody] GetCacheItemRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var value = await _cacheManager.GetAsync<JsonNode>(request.Key, request.Region, cancellationToken);
                
                var response = new GetCacheItemResponse<JsonNode>
                {
                    Success = true,
                    Key = request.Key,
                    Region = request.Region,
                    Found = value != null,
                    Value = value,
                    ProviderId = "default" // We don't have direct access to the provider ID
                };
                
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cache item with key {Key} in region {Region}", request.Key, request.Region);
                
                return StatusCode(StatusCodes.Status500InternalServerError, new GetCacheItemResponse<JsonNode>
                {
                    Success = false,
                    Key = request.Key,
                    Region = request.Region,
                    Found = false,
                    Error = $"Error getting cache item: {ex.Message}",
                    ProviderId = "default"
                });
            }
        }
        
        /// <summary>
        /// Sets an item in the cache
        /// </summary>
        /// <param name="request">Set request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Status of the operation</returns>
        [HttpPost("set")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<SetCacheItemResponse>> SetAsync(
            [FromBody] SetCacheItemRequest<JsonNode> request,
            CancellationToken cancellationToken)
        {
            try
            {
                TimeSpan? expiration = request.ExpirationSeconds.HasValue
                    ? TimeSpan.FromSeconds(request.ExpirationSeconds.Value)
                    : null;
                    
                bool success = await _cacheManager.SetAsync(
                    request.Key,
                    request.Value,
                    expiration,
                    request.Region,
                    request.Tags,
                    cancellationToken);
                    
                var response = new SetCacheItemResponse
                {
                    Success = success,
                    Key = request.Key,
                    Region = request.Region,
                    ExpiresAt = expiration.HasValue ? DateTimeOffset.UtcNow.Add(expiration.Value) : null,
                    ProviderId = "default"
                };
                
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting cache item with key {Key} in region {Region}", request.Key, request.Region);
                
                return StatusCode(StatusCodes.Status500InternalServerError, new SetCacheItemResponse
                {
                    Success = false,
                    Key = request.Key,
                    Region = request.Region,
                    Error = $"Error setting cache item: {ex.Message}",
                    ProviderId = "default"
                });
            }
        }
        
        /// <summary>
        /// Removes an item from the cache
        /// </summary>
        /// <param name="request">Remove request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Status of the operation</returns>
        [HttpPost("remove")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<RemoveCacheItemResponse>> RemoveAsync(
            [FromBody] RemoveCacheItemRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                bool removed = await _cacheManager.RemoveAsync(request.Key, request.Region, cancellationToken);
                
                var response = new RemoveCacheItemResponse
                {
                    Success = true,
                    Key = request.Key,
                    Region = request.Region,
                    Removed = removed,
                    ProviderId = "default"
                };
                
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cache item with key {Key} in region {Region}", request.Key, request.Region);
                
                return StatusCode(StatusCodes.Status500InternalServerError, new RemoveCacheItemResponse
                {
                    Success = false,
                    Key = request.Key,
                    Region = request.Region,
                    Removed = false,
                    Error = $"Error removing cache item: {ex.Message}",
                    ProviderId = "default"
                });
            }
        }
        
        /// <summary>
        /// Removes items from the cache by tag
        /// </summary>
        /// <param name="request">Remove by tag request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Status of the operation</returns>
        [HttpPost("remove-by-tag")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<RemoveByTagResponse>> RemoveByTagAsync(
            [FromBody] RemoveByTagRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                int removed = await _cacheManager.RemoveByTagAsync(request.Tag, cancellationToken);
                
                var response = new RemoveByTagResponse
                {
                    Success = true,
                    Tag = request.Tag,
                    RemovedCount = removed,
                    ProviderId = "default"
                };
                
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cache items with tag {Tag}", request.Tag);
                
                return StatusCode(StatusCodes.Status500InternalServerError, new RemoveByTagResponse
                {
                    Success = false,
                    Tag = request.Tag,
                    RemovedCount = 0,
                    Error = $"Error removing cache items: {ex.Message}",
                    ProviderId = "default"
                });
            }
        }
        
        /// <summary>
        /// Clears the cache or a specific region
        /// </summary>
        /// <param name="request">Clear request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Status of the operation</returns>
        [HttpPost("clear")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ClearCacheResponse>> ClearAsync(
            [FromBody] ClearCacheRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                bool success = await _cacheManager.ClearAsync(request.Region, cancellationToken);
                
                var response = new ClearCacheResponse
                {
                    Success = success,
                    Region = request.Region,
                    ProviderId = "default"
                };
                
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing cache in region {Region}", request.Region);
                
                return StatusCode(StatusCodes.Status500InternalServerError, new ClearCacheResponse
                {
                    Success = false,
                    Region = request.Region,
                    Error = $"Error clearing cache: {ex.Message}",
                    ProviderId = "default"
                });
            }
        }
        
        /// <summary>
        /// Gets all items from a region
        /// </summary>
        /// <param name="request">Get all request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>All items in the region</returns>
        [HttpPost("get-all")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<GetAllResponse<JsonNode>>> GetAllAsync(
            [FromBody] GetAllRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var items = await _cacheManager.GetAllAsync<JsonNode>(request.Region, cancellationToken);
                
                var response = new GetAllResponse<JsonNode>
                {
                    Success = true,
                    Region = request.Region,
                    Items = new Dictionary<string, JsonNode?>(items),
                    ProviderId = "default"
                };
                
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all cache items in region {Region}", request.Region);
                
                return StatusCode(StatusCodes.Status500InternalServerError, new GetAllResponse<JsonNode>
                {
                    Success = false,
                    Region = request.Region,
                    Error = $"Error getting all cache items: {ex.Message}",
                    ProviderId = "default"
                });
            }
        }
        
        /// <summary>
        /// Gets statistics about the cache
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Cache statistics</returns>
        [HttpGet("statistics")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<GetStatisticsResponse>> GetStatisticsAsync(
            CancellationToken cancellationToken)
        {
            try
            {
                var stats = await _cacheManager.GetStatisticsAsync(cancellationToken);
                
                var response = new GetStatisticsResponse
                {
                    Success = true,
                    ProviderId = stats.ProviderId,
                    ItemCount = stats.ItemCount,
                    TotalSizeInBytes = stats.TotalSizeInBytes,
                    Hits = stats.Hits,
                    Misses = stats.Misses,
                    HitRatio = stats.HitRatio,
                    Evictions = stats.Evictions,
                    LastResetTime = stats.LastResetTime
                };
                
                if (stats.RegionStats.Count > 0)
                {
                    response.RegionStats = new Dictionary<string, RegionStatisticsDto>();
                    
                    foreach (var kvp in stats.RegionStats)
                    {
                        response.RegionStats[kvp.Key] = new RegionStatisticsDto
                        {
                            Region = kvp.Value.Region,
                            ItemCount = kvp.Value.ItemCount,
                            SizeInBytes = kvp.Value.SizeInBytes,
                            Hits = kvp.Value.Hits,
                            Misses = kvp.Value.Misses,
                            HitRatio = kvp.Value.HitRatio
                        };
                    }
                }
                
                if (stats.MemoryUsage != null)
                {
                    response.MemoryUsage = new MemoryUsageDto
                    {
                        AllocatedBytes = stats.MemoryUsage.AllocatedBytes,
                        UsedBytes = stats.MemoryUsage.UsedBytes,
                        UsagePercentage = stats.MemoryUsage.UsagePercentage,
                        MaxBytes = stats.MemoryUsage.MaxBytes
                    };
                }
                
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cache statistics");
                
                return StatusCode(StatusCodes.Status500InternalServerError, new GetStatisticsResponse
                {
                    Success = false,
                    Error = $"Error getting cache statistics: {ex.Message}",
                    ProviderId = "default"
                });
            }
        }
        
        /// <summary>
        /// Refreshes the expiration of a cache item
        /// </summary>
        /// <param name="request">Refresh expiration request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Status of the operation</returns>
        [HttpPost("refresh-expiration")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<RefreshExpirationResponse>> RefreshExpirationAsync(
            [FromBody] RefreshExpirationRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                TimeSpan expiration = TimeSpan.FromSeconds(request.ExpirationSeconds);
                
                bool refreshed = await _cacheManager.RefreshExpirationAsync(
                    request.Key,
                    expiration,
                    request.Region,
                    cancellationToken);
                    
                var response = new RefreshExpirationResponse
                {
                    Success = true,
                    Key = request.Key,
                    Region = request.Region,
                    Refreshed = refreshed,
                    ExpiresAt = refreshed ? DateTimeOffset.UtcNow.Add(expiration) : null,
                    ProviderId = "default"
                };
                
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing expiration for cache item with key {Key} in region {Region}", request.Key, request.Region);
                
                return StatusCode(StatusCodes.Status500InternalServerError, new RefreshExpirationResponse
                {
                    Success = false,
                    Key = request.Key,
                    Region = request.Region,
                    Refreshed = false,
                    Error = $"Error refreshing expiration: {ex.Message}",
                    ProviderId = "default"
                });
            }
        }
    }
}
