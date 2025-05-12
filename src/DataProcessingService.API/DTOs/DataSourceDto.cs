using System;
using System.Collections.Generic;
using DataProcessingService.Core.Domain.Enums;

namespace DataProcessingService.API.DTOs;

public class DataSourceDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string ConnectionString { get; set; } = null!;
    public DataSourceType Type { get; set; }
    public string? Schema { get; set; }
    public Dictionary<string, string> Properties { get; set; } = new();
    public bool IsActive { get; set; }
    public DateTimeOffset LastSyncTime { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTimeOffset? LastModifiedAt { get; set; }
    public string? LastModifiedBy { get; set; }
}

public class CreateDataSourceDto
{
    public string Name { get; set; } = null!;
    public string ConnectionString { get; set; } = null!;
    public DataSourceType Type { get; set; }
    public string? Schema { get; set; }
    public Dictionary<string, string>? Properties { get; set; }
}

public class UpdateDataSourceDto
{
    public string? ConnectionString { get; set; }
    public string? Schema { get; set; }
    public bool? IsActive { get; set; }
    public Dictionary<string, string>? PropertiesToAdd { get; set; }
    public List<string>? PropertiesToRemove { get; set; }
}

public class TestConnectionDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
}
