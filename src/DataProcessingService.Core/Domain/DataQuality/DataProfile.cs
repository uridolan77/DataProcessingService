using System;
using System.Collections.Generic;
using DataProcessingService.Core.Domain.Entities.Base;

namespace DataProcessingService.Core.Domain.DataQuality;

public class DataProfile : AuditableEntity
{
    public Guid DataSourceId { get; private set; }
    public string TableName { get; private set; } = null!;
    public DateTimeOffset ProfiledAt { get; private set; }
    public long RowCount { get; private set; }
    public List<ColumnProfile> Columns { get; private set; } = new();
    
    // For EF Core
    private DataProfile() { }
    
    public DataProfile(
        Guid dataSourceId,
        string tableName,
        long rowCount,
        List<ColumnProfile> columns)
    {
        DataSourceId = dataSourceId;
        TableName = tableName;
        ProfiledAt = DateTimeOffset.UtcNow;
        RowCount = rowCount;
        Columns = columns;
    }
    
    public void Update(long rowCount, List<ColumnProfile> columns)
    {
        ProfiledAt = DateTimeOffset.UtcNow;
        RowCount = rowCount;
        Columns = columns;
    }
}

public class ColumnProfile
{
    public string ColumnName { get; set; } = null!;
    public string DataType { get; set; } = null!;
    public long NullCount { get; set; }
    public long UniqueCount { get; set; }
    public long EmptyCount { get; set; }
    public string? MinValue { get; set; }
    public string? MaxValue { get; set; }
    public string? AvgValue { get; set; }
    public string? MedianValue { get; set; }
    public Dictionary<string, long> TopValues { get; set; } = new();
    public Dictionary<string, double> Statistics { get; set; } = new();
    public List<DataQualityIssue> DetectedIssues { get; set; } = new();
}

public class DataQualityIssue
{
    public string IssueType { get; set; } = null!;
    public string Description { get; set; } = null!;
    public RuleSeverity Severity { get; set; }
    public double Confidence { get; set; }
    
    public DataQualityIssue(string issueType, string description, RuleSeverity severity, double confidence)
    {
        IssueType = issueType;
        Description = description;
        Severity = severity;
        Confidence = confidence;
    }
}
