using System;
using System.Collections.Generic;
using DataProcessingService.Core.Domain.Entities.Base;
using DataProcessingService.Core.Domain.Enums;

namespace DataProcessingService.Core.Domain.Entities;

public class DataSource : AuditableEntity
{
    public string Name { get; private set; } = null!;
    public string ConnectionString { get; private set; } = null!;
    public DataSourceType Type { get; private set; }
    public string? Schema { get; private set; }
    public Dictionary<string, string> Properties { get; private set; } = new();
    public bool IsActive { get; private set; }
    public DateTimeOffset LastSyncTime { get; private set; }
    
    public ICollection<DataPipeline> DataPipelines { get; private set; } = new List<DataPipeline>();
    
    // For EF Core
    private DataSource() { }
    
    public DataSource(
        string name, 
        string connectionString, 
        DataSourceType type, 
        string? schema = null,
        Dictionary<string, string>? properties = null)
    {
        Name = name;
        ConnectionString = connectionString;
        Type = type;
        Schema = schema;
        Properties = properties ?? new Dictionary<string, string>();
        IsActive = true;
        LastSyncTime = DateTimeOffset.UtcNow;
    }
    
    public void UpdateConnectionDetails(string connectionString, string? schema)
    {
        ConnectionString = connectionString;
        Schema = schema;
    }
    
    public void Activate() => IsActive = true;
    
    public void Deactivate() => IsActive = false;
    
    public void UpdateLastSyncTime(DateTimeOffset syncTime) => LastSyncTime = syncTime;
    
    public void AddProperty(string key, string value) => Properties[key] = value;
    
    public void RemoveProperty(string key) => Properties.Remove(key);
}
