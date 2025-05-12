using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Threading;
using System.Threading.Tasks;

namespace DataProcessingService.Core.Interfaces.Services.ETL;

public interface IComplexTransformationService
{
    Task<IEnumerable<ExpandoObject>> ApplyWindowFunctionAsync(
        IEnumerable<ExpandoObject> data,
        WindowFunctionType functionType,
        string[] partitionByFields,
        string[] orderByFields,
        string targetField,
        string? sourceField = null,
        CancellationToken cancellationToken = default);
    
    Task<IEnumerable<ExpandoObject>> ApplyAggregationAsync(
        IEnumerable<ExpandoObject> data,
        string[] groupByFields,
        Dictionary<string, AggregationFunction> aggregations,
        CancellationToken cancellationToken = default);
    
    Task<IEnumerable<ExpandoObject>> ApplyJoinAsync(
        IEnumerable<ExpandoObject> leftData,
        IEnumerable<ExpandoObject> rightData,
        JoinType joinType,
        JoinCondition[] joinConditions,
        CancellationToken cancellationToken = default);
    
    Task<IEnumerable<ExpandoObject>> ApplyPivotAsync(
        IEnumerable<ExpandoObject> data,
        string[] rowFields,
        string columnField,
        string valueField,
        AggregationFunction aggregationFunction,
        CancellationToken cancellationToken = default);
    
    Task<IEnumerable<ExpandoObject>> ApplyUnpivotAsync(
        IEnumerable<ExpandoObject> data,
        string[] keyFields,
        string[] valueFields,
        string newColumnField,
        string newValueField,
        CancellationToken cancellationToken = default);
    
    Task<IEnumerable<ExpandoObject>> ApplyCustomTransformationAsync(
        IEnumerable<ExpandoObject> data,
        string transformationScript,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default);
}

public enum WindowFunctionType
{
    RowNumber,
    Rank,
    DenseRank,
    Lead,
    Lag,
    FirstValue,
    LastValue,
    Sum,
    Average,
    Min,
    Max,
    Count,
    Custom
}

public class AggregationFunction
{
    public AggregationType Type { get; set; }
    public string? SourceField { get; set; }
    public string? CustomExpression { get; set; }
    
    public static AggregationFunction Count() => new() { Type = AggregationType.Count };
    
    public static AggregationFunction Sum(string sourceField) => new() 
    { 
        Type = AggregationType.Sum, 
        SourceField = sourceField 
    };
    
    public static AggregationFunction Min(string sourceField) => new() 
    { 
        Type = AggregationType.Min, 
        SourceField = sourceField 
    };
    
    public static AggregationFunction Max(string sourceField) => new() 
    { 
        Type = AggregationType.Max, 
        SourceField = sourceField 
    };
    
    public static AggregationFunction Average(string sourceField) => new() 
    { 
        Type = AggregationType.Average, 
        SourceField = sourceField 
    };
    
    public static AggregationFunction First(string sourceField) => new() 
    { 
        Type = AggregationType.First, 
        SourceField = sourceField 
    };
    
    public static AggregationFunction Last(string sourceField) => new() 
    { 
        Type = AggregationType.Last, 
        SourceField = sourceField 
    };
    
    public static AggregationFunction Custom(string customExpression) => new() 
    { 
        Type = AggregationType.Custom, 
        CustomExpression = customExpression 
    };
}

public enum JoinType
{
    Inner,
    Left,
    Right,
    Full,
    Cross
}

public class JoinCondition
{
    public string LeftField { get; set; } = null!;
    public string RightField { get; set; } = null!;
    public JoinOperator Operator { get; set; } = JoinOperator.Equal;
    
    public static JoinCondition Equal(string leftField, string rightField) => new()
    {
        LeftField = leftField,
        RightField = rightField,
        Operator = JoinOperator.Equal
    };
}

public enum JoinOperator
{
    Equal,
    NotEqual,
    LessThan,
    LessThanOrEqual,
    GreaterThan,
    GreaterThanOrEqual
}
