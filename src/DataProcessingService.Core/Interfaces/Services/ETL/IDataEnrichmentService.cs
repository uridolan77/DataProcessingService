using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.Core.Domain.Entities;

namespace DataProcessingService.Core.Interfaces.Services.ETL;

public interface IDataEnrichmentService
{
    Task<IEnumerable<ExpandoObject>> EnrichFromLookupTableAsync(
        IEnumerable<ExpandoObject> data,
        DataSource lookupSource,
        string lookupTable,
        string sourceKeyField,
        string lookupKeyField,
        string[] lookupValueFields,
        bool includeNullMatches = false,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<ExpandoObject>> EnrichFromApiAsync(
        IEnumerable<ExpandoObject> data,
        string apiUrl,
        string requestTemplate,
        string sourceKeyField,
        Dictionary<string, string> responseMapping,
        int batchSize = 10,
        int rateLimitPerMinute = 60,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<ExpandoObject>> EnrichWithGeocodeAsync(
        IEnumerable<ExpandoObject> data,
        string addressField,
        string? cityField = null,
        string? stateField = null,
        string? countryField = null,
        string? postalCodeField = null,
        string geocodeProvider = "default",
        string apiKey = "",
        CancellationToken cancellationToken = default);

    Task<IEnumerable<ExpandoObject>> EnrichWithSentimentAnalysisAsync(
        IEnumerable<ExpandoObject> data,
        string textField,
        string targetField = "sentiment",
        string language = "en",
        CancellationToken cancellationToken = default);

    Task<IEnumerable<ExpandoObject>> EnrichWithEntityExtractionAsync(
        IEnumerable<ExpandoObject> data,
        string textField,
        string targetField = "entities",
        string[] entityTypes = null!,
        string language = "en",
        CancellationToken cancellationToken = default);

    Task<IEnumerable<ExpandoObject>> EnrichWithCalculatedFieldsAsync(
        IEnumerable<ExpandoObject> data,
        Dictionary<string, CalculatedField> calculatedFields,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<ExpandoObject>> EnrichWithMlPredictionAsync(
        IEnumerable<ExpandoObject> data,
        string modelEndpoint,
        string[] inputFields,
        string targetField,
        string apiKey = "",
        CancellationToken cancellationToken = default);

    Task<IEnumerable<ExpandoObject>> EnrichWithCustomFunctionAsync(
        IEnumerable<ExpandoObject> data,
        string functionScript,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default);
}

public class CalculatedField
{
    public string TargetField { get; set; } = null!;
    public CalculationType Type { get; set; }
    public string[] SourceFields { get; set; } = null!;
    public string? Expression { get; set; }
    public Dictionary<string, object>? Parameters { get; set; }

    public static CalculatedField Simple(
        string targetField,
        CalculationType type,
        string[] sourceFields)
    {
        return new CalculatedField
        {
            TargetField = targetField,
            Type = type,
            SourceFields = sourceFields
        };
    }

    public static CalculatedField CreateExpression(
        string targetField,
        string expression,
        string[] sourceFields)
    {
        return new CalculatedField
        {
            TargetField = targetField,
            Type = CalculationType.Expression,
            SourceFields = sourceFields,
            Expression = expression
        };
    }

    public static CalculatedField Custom(
        string targetField,
        string[] sourceFields,
        Dictionary<string, object> parameters)
    {
        return new CalculatedField
        {
            TargetField = targetField,
            Type = CalculationType.Custom,
            SourceFields = sourceFields,
            Parameters = parameters
        };
    }
}

public enum CalculationType
{
    Sum,
    Average,
    Concatenate,
    DateDiff,
    Expression,
    Custom
}
