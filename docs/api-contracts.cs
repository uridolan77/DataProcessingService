using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace CachingService.Contracts.Requests
{
    /// <summary>
    /// Base class for all cache requests
    /// </summary>
    public abstract class CacheRequestBase
    {
        /// <summary>
        /// Optional cache provider to use
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ProviderId { get; set; }
    }
    
    /// <summary>
    /// Request to get an item from the cache
    /// </summary>
    public class GetCacheItemRequest : CacheRequestBase
    {
        /// <summary>
        /// Key of the item to retrieve
        /// </summary>
        [Required]
        public string Key { get; set; } = default!;
        
        /// <summary>
        /// Optional region/partition
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Region { get; set; }
    }
    
    /// <summary>
    /// Request to set an item in the cache
    /// </summary>
    public class SetCacheItemRequest<T> : CacheRequestBase
    {
        /// <summary>
        /// Key of the item to set
        /// </summary>
        [Required]
        public string Key { get; set; } = default!;
        
        /// <summary>
        /// Value to cache
        /// </summary>
        [Required]
        public T Value { get; set; } = default!;
        
        /// <summary>
        /// Optional region/partition
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Region { get; set; }
        
        /// <summary>
        /// Optional expiration time in seconds
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int? ExpirationSeconds { get; set; }
        
        /// <summary>
        /// Optional tags for the item
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IEnumerable<string>? Tags { get; set; }
    }
    
    /// <summary>
    /// Request to remove an item from the cache
    /// </summary>
    public class RemoveCacheItemRequest : CacheRequestBase
    {
        /// <summary>
        /// Key of the item to remove
        /// </summary>
        [Required]
        public string Key { get; set; } = default!;
        
        /// <summary>
        /// Optional region/partition
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Region { get; set; }
    }
    
    /// <summary>
    /// Request to remove items by tag
    /// </summary>
    public class RemoveByTagRequest : CacheRequestBase
    {
        /// <summary>
        /// Tag to remove items by
        /// </summary>
        [Required]
        public string Tag { get; set; } = default!;
    }
    
    /// <summary>
    /// Request to clear the cache
    /// </summary>
    public class ClearCacheRequest : CacheRequestBase
    {
        /// <summary>
        /// Optional region/partition to clear
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Region { get; set; }
    }
    
    /// <summary>
    /// Request to get all items from a region
    /// </summary>
    public class GetAllRequest : CacheRequestBase
    {
        /// <summary>
        /// Region to get items from
        /// </summary>
        [Required]
        public string Region { get; set; } = default!;
    }
    
    /// <summary>
    /// Request to refresh the expiration of an item
    /// </summary>
    public class RefreshExpirationRequest : CacheRequestBase
    {
        /// <summary>
        /// Key of the item to refresh
        /// </summary>
        [Required]
        public string Key { get; set; } = default!;
        
        /// <summary>
        /// Optional region/partition
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Region { get; set; }
        
        /// <summary>
        /// New expiration time in seconds
        /// </summary>
        [Required]
        public int ExpirationSeconds { get; set; }
    }
}

namespace CachingService.Contracts.Responses
{
    /// <summary>
    /// Base class for all cache responses
    /// </summary>
    public abstract class CacheResponseBase
    {
        /// <summary>
        /// Whether the operation was successful
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// Error message if the operation failed
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Error { get; set; }
        
        /// <summary>
        /// Cache provider used for the operation
        /// </summary>
        public string ProviderId { get; set; } = default!;
        
        /// <summary>
        /// When the response was created
        /// </summary>
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    }
    
    /// <summary>
    /// Response for getting an item from the cache
    /// </summary>
    /// <typeparam name="T">Type of the cached value</typeparam>
    public class GetCacheItemResponse<T> : CacheResponseBase
    {
        /// <summary>
        /// Key of the item
        /// </summary>
        public string Key { get; set; } = default!;
        
        /// <summary>
        /// Region of the item
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Region { get; set; }
        
        /// <summary>
        /// Whether the item was found in the cache
        /// </summary>
        public bool Found { get; set; }
        
        /// <summary>
        /// The cached value
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public T? Value { get; set; }
        
        /// <summary>
        /// When the item expires
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DateTimeOffset? ExpiresAt { get; set; }
    }
    
    /// <summary>
    /// Response for setting an item in the cache
    /// </summary>
    public class SetCacheItemResponse : CacheResponseBase
    {
        /// <summary>
        /// Key of the item
        /// </summary>
        public string Key { get; set; } = default!;
        
        /// <summary>
        /// Region of the item
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Region { get; set; }
        
        /// <summary>
        /// When the item expires
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DateTimeOffset? ExpiresAt { get; set; }
    }
    
    /// <summary>
    /// Response for removing an item from the cache
    /// </summary>
    public class RemoveCacheItemResponse : CacheResponseBase
    {
        /// <summary>
        /// Key of the item
        /// </summary>
        public string Key { get; set; } = default!;
        
        /// <summary>
        /// Region of the item
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Region { get; set; }
        
        /// <summary>
        /// Whether the item was found and removed
        /// </summary>
        public bool Removed { get; set; }
    }
    
    /// <summary>
    /// Response for removing items by tag
    /// </summary>
    public class RemoveByTagResponse : CacheResponseBase
    {
        /// <summary>
        /// Tag used to remove items
        /// </summary>
        public string Tag { get; set; } = default!;
        
        /// <summary>
        /// Number of items removed
        /// </summary>
        public int RemovedCount { get; set; }
    }
    
    /// <summary>
    /// Response for clearing the cache
    /// </summary>
    public class ClearCacheResponse : CacheResponseBase
    {
        /// <summary>
        /// Region that was cleared
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Region { get; set; }
    }
    
    /// <summary>
    /// Response for getting all items from a region
    /// </summary>
    /// <typeparam name="T">Type of the cached values</typeparam>
    public class GetAllResponse<T> : CacheResponseBase
    {
        /// <summary>
        /// Region the items were retrieved from
        /// </summary>
        public string Region { get; set; } = default!;
        
        /// <summary>
        /// Number of items retrieved
        /// </summary>
        public int Count => Items?.Count ?? 0;
        
        /// <summary>
        /// The cached items
        /// </summary>
        public Dictionary<string, T?>? Items { get; set; }
    }
    
    /// <summary>
    /// Response for getting the cache statistics
    /// </summary>
    public class GetStatisticsResponse : CacheResponseBase
    {
        /// <summary>
        /// Total number of items in the cache
        /// </summary>
        public long ItemCount { get; set; }
        
        /// <summary>
        /// Total size of the cache in bytes
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? TotalSizeInBytes { get; set; }
        
        /// <summary>
        /// Number of cache hits
        /// </summary>
        public long Hits { get; set; }
        
        /// <summary>
        /// Number of cache misses
        /// </summary>
        public long Misses { get; set; }
        
        /// <summary>
        /// Hit ratio (hits / total requests)
        /// </summary>
        public double HitRatio { get; set; }
        
        /// <summary>
        /// Number of items evicted from the cache
        /// </summary>
        public long Evictions { get; set; }
        
        /// <summary>
        /// When the statistics were last reset
        /// </summary>
        public DateTimeOffset LastResetTime { get; set; }
        
        /// <summary>
        /// Statistics per region
        /// </summary>
        public Dictionary<string, RegionStatisticsDto>? RegionStats { get; set; }
        
        /// <summary>
        /// Memory usage information
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public MemoryUsageDto? MemoryUsage { get; set; }
    }
    
    /// <summary>
    /// Statistics for a specific cache region
    /// </summary>
    public class RegionStatisticsDto
    {
        /// <summary>
        /// Name of the region
        /// </summary>
        public string Region { get; set; } = default!;
        
        /// <summary>
        /// Number of items in this region
        /// </summary>
        public long ItemCount { get; set; }
        
        /// <summary>
        /// Total size of items in this region in bytes
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? SizeInBytes { get; set; }
        
        /// <summary>
        /// Number of cache hits for this region
        /// </summary>
        public long Hits { get; set; }
        
        /// <summary>
        /// Number of cache misses for this region
        /// </summary>
        public long Misses { get; set; }
        
        /// <summary>
        /// Hit ratio for this region
        /// </summary>
        public double HitRatio { get; set; }
    }
    
    /// <summary>
    /// Memory usage information for the cache
    /// </summary>
    public class MemoryUsageDto
    {
        /// <summary>
        /// Total memory allocated for the cache in bytes
        /// </summary>
        public long AllocatedBytes { get; set; }
        
        /// <summary>
        /// Memory currently used by the cache in bytes
        /// </summary>
        public long UsedBytes { get; set; }
        
        /// <summary>
        /// Memory usage percentage
        /// </summary>
        public double UsagePercentage { get; set; }
        
        /// <summary>
        /// Maximum memory limit for the cache in bytes
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? MaxBytes { get; set; }
    }
    
    /// <summary>
    /// Response for refreshing the expiration of an item
    /// </summary>
    public class RefreshExpirationResponse : CacheResponseBase
    {
        /// <summary>
        /// Key of the item
        /// </summary>
        public string Key { get; set; } = default!;
        
        /// <summary>
        /// Region of the item
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Region { get; set; }
        
        /// <summary>
        /// Whether the item was found and its expiration was refreshed
        /// </summary>
        public bool Refreshed { get; set; }
        
        /// <summary>
        /// New expiration time
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DateTimeOffset? ExpiresAt { get; set; }
    }
}
