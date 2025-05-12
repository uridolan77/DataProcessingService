# ETL Service Implementations

## src/DataProcessingService.Infrastructure/Services/ETL/DataExtractionService.cs
```csharp
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Dynamic;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using DataProcessingService.Core.Domain.Entities;
using DataProcessingService.Core.Domain.Enums;
using DataProcessingService.Core.Interfaces.Services.ETL;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Npgsql;
using MySqlConnector;

namespace DataProcessingService.Infrastructure.Services.ETL;

public class DataExtractionService : IDataExtractionService
{
    private readonly ILogger<DataExtractionService> _logger;
    
    public DataExtractionService(ILogger<DataExtractionService> logger)
    {
        _logger = logger;
    }
    
    public async Task<IEnumerable<ExpandoObject>> ExtractDataAsync(
        DataSource source,
        string query,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        using var connection = CreateConnection(source);
        await connection.OpenAsync(cancellationToken);
        
        try
        {
            _logger.LogInformation("Extracting data from {SourceName} with query: {Query}", 
                source.Name, query);
            
            var command = new CommandDefinition(
                query,
                parameters,
                commandTimeout: 300,
                cancellationToken: cancellationToken);
            
            var result = await connection.QueryAsync<dynamic>(command);
            
            // Convert dynamic to ExpandoObject for consistent interface
            var expandoResults = new List<ExpandoObject>();
            
            foreach (var item in result)
            {
                IDictionary<string, object?> expando = new ExpandoObject();
                
                foreach (var property in item as IDictionary<string, object?>)
                {
                    expando[property.Key] = property.Value;
                }
                
                expandoResults.Add((ExpandoObject)expando);
            }
            
            _logger.LogInformation("Extracted {Count} records from {SourceName}", 
                expandoResults.Count, source.Name);
            
            return expandoResults;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting data from {SourceName}", source.Name);
            throw;
        }
    }
    
    public async Task<IAsyncEnumerable<ExpandoObject>> ExtractDataStreamAsync(
        DataSource source,
        string query,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var connection = CreateConnection(source);
        await connection.OpenAsync(cancellationToken);
        
        try
        {
            _logger.LogInformation("Streaming data from {SourceName} with query: {Query}", 
                source.Name, query);
            
            var command = connection.CreateCommand();
            command.CommandText = query;
            command.CommandTimeout = 300;
            
            if (parameters != null)
            {
                foreach (var parameter in parameters)
                {
                    var dbParameter = command.CreateParameter();
                    dbParameter.ParameterName = parameter.Key;
                    dbParameter.Value = parameter.Value ?? DBNull.Value;
                    command.Parameters.Add(dbParameter);
                }
            }
            
            // Create a stream that will handle cleanup of resources
            return new DataReaderAsyncEnumerable(
                connection, 
                await command.ExecuteReaderAsync(CommandBehavior.CloseConnection, cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error streaming data from {SourceName}", source.Name);
            await connection.DisposeAsync();
            throw;
        }
    }
    
    public async Task<IEnumerable<string>> GetAvailableTablesAsync(
        DataSource source,
        CancellationToken cancellationToken = default)
    {
        using var connection = CreateConnection(source);
        await connection.OpenAsync(cancellationToken);
        
        try
        {
            string query = GetTablesQuery(source.Type, source.Schema);
            
            var tables = await connection.QueryAsync<string>(
                new CommandDefinition(query, cancellationToken: cancellationToken));
            
            return tables.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available tables from {SourceName}", source.Name);
            throw;
        }
    }
    
    public async Task<IEnumerable<(string Name, string Type)>> GetTableSchemaAsync(
        DataSource source,
        string tableName,
        CancellationToken cancellationToken = default)
    {
        using var connection = CreateConnection(source);
        await connection.OpenAsync(cancellationToken);
        
        try
        {
            string query = GetTableSchemaQuery(source.Type, tableName, source.Schema);
            
            var columns = await connection.QueryAsync<(string Name, string Type)>(
                new CommandDefinition(query, cancellationToken: cancellationToken));
            
            return columns.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting schema for table {TableName} from {SourceName}", 
                tableName, source.Name);
            throw;
        }
    }
    
    private DbConnection CreateConnection(DataSource source)
    {
        return source.Type switch
        {
            DataSourceType.SqlServer => new SqlConnection(source.ConnectionString),
            DataSourceType.PostgreSql => new NpgsqlConnection(source.ConnectionString),
            DataSourceType.MySql => new MySqlConnection(source.ConnectionString),
            _ => throw new NotSupportedException($"Data source type {source.Type} is not supported")
        };
    }
    
    private string GetTablesQuery(DataSourceType sourceType, string? schema)
    {
        return sourceType switch
        {
            DataSourceType.SqlServer => 
                $"SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = '{(schema ?? "dbo")}' AND TABLE_TYPE = 'BASE TABLE'",
            
            DataSourceType.PostgreSql => 
                $"SELECT table_name FROM information_schema.tables WHERE table_schema = '{(schema ?? "public")}' AND table_type = 'BASE TABLE'",
            
            DataSourceType.MySql => 
                $"SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = '{(schema ?? "database()")}' AND TABLE_TYPE = 'BASE TABLE'",
            
            _ => throw new NotSupportedException($"Data source type {sourceType} is not supported")
        };
    }
    
    private string GetTableSchemaQuery(DataSourceType sourceType, string tableName, string? schema)
    {
        return sourceType switch
        {
            DataSourceType.SqlServer => 
                $"SELECT COLUMN_NAME as Name, DATA_TYPE as Type FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = '{(schema ?? "dbo")}' AND TABLE_NAME = '{tableName}'",
            
            DataSourceType.PostgreSql => 
                $"SELECT column_name as Name, data_type as Type FROM information_schema.columns WHERE table_schema = '{(schema ?? "public")}' AND table_name = '{tableName}'",
            
            DataSourceType.MySql => 
                $"SELECT COLUMN_NAME as Name, DATA_TYPE as Type FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = '{(schema ?? "database()")}' AND TABLE_NAME = '{tableName}'",
            
            _ => throw new NotSupportedException($"Data source type {sourceType} is not supported")
        };
    }
    
    private class DataReaderAsyncEnumerable : IAsyncEnumerable<ExpandoObject>
    {
        private readonly DbConnection _connection;
        private readonly DbDataReader _reader;
        
        public DataReaderAsyncEnumerable(DbConnection connection, DbDataReader reader)
        {
            _connection = connection;
            _reader = reader;
        }
        
        public IAsyncEnumerator<ExpandoObject> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new DataReaderAsyncEnumerator(_connection, _reader, cancellationToken);
        }
        
        private class DataReaderAsyncEnumerator : IAsyncEnumerator<ExpandoObject>
        {
            private readonly DbConnection _connection;
            private readonly DbDataReader _reader;
            private readonly CancellationToken _cancellationToken;
            private bool _disposed;
            
            public DataReaderAsyncEnumerator(
                DbConnection connection, 
                DbDataReader reader, 
                CancellationToken cancellationToken)
            {
                _connection = connection;
                _reader = reader;
                _cancellationToken = cancellationToken;
            }
            
            public ExpandoObject Current { get; private set; } = new ExpandoObject();
            
            public async ValueTask<bool> MoveNextAsync()
            {
                if (_disposed)
                {
                    return false;
                }
                
                if (await _reader.ReadAsync(_cancellationToken))
                {
                    IDictionary<string, object?> expando = new ExpandoObject();
                    
                    for (int i = 0; i < _reader.FieldCount; i++)
                    {
                        expando[_reader.GetName(i)] = _reader.IsDBNull(i) ? null : _reader.GetValue(i);
                    }
                    
                    Current = (ExpandoObject)expando;
                    return true;
                }
                
                return false;
            }
            
            public async ValueTask DisposeAsync()
            {
                if (_disposed)
                {
                    return;
                }
                
                await _reader.DisposeAsync();
                await _connection.DisposeAsync();
                _disposed = true;
            }
        }
    }
}

## src/DataProcessingService.Infrastructure/Services/ETL/DataTransformationService.cs
```csharp
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.Core.Domain.Enums;
using DataProcessingService.Core.Domain.ValueObjects;
using DataProcessingService.Core.Interfaces.Services.ETL;
using Microsoft.Extensions.Logging;

namespace DataProcessingService.Infrastructure.Services.ETL;

public class DataTransformationService : IDataTransformationService
{
    private readonly ILogger<DataTransformationService> _logger;
    
    public DataTransformationService(ILogger<DataTransformationService> logger)
    {
        _logger = logger;
    }
    
    public async Task<IEnumerable<ExpandoObject>> TransformDataAsync(
        IEnumerable<ExpandoObject> data,
        TransformationRules rules,
        CancellationToken cancellationToken = default)
    {
        var result = new List<ExpandoObject>();
        
        foreach (var record in data)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var transformedRecord = await TransformRecordAsync(record, rules, cancellationToken);
            result.Add(transformedRecord);
        }
        
        _logger.LogInformation("Transformed {Count} records using {RuleCount} rules", 
            result.Count, rules.Rules.Count);
        
        return result;
    }
    
    public async Task<IAsyncEnumerable<ExpandoObject>> TransformDataStreamAsync(
        IAsyncEnumerable<ExpandoObject> dataStream,
        TransformationRules rules,
        CancellationToken cancellationToken = default)
    {
        return new TransformingAsyncEnumerable(this, dataStream, rules, cancellationToken);
    }
    
    public async Task<ExpandoObject> TransformRecordAsync(
        ExpandoObject record,
        TransformationRules rules,
        CancellationToken cancellationToken = default)
    {
        var recordDict = record as IDictionary<string, object?>;
        var result = new ExpandoObject();
        var resultDict = result as IDictionary<string, object?>;
        
        // Initialize the result with all fields from the original record
        foreach (var field in recordDict)
        {
            resultDict[field.Key] = field.Value;
        }
        
        // Apply rules sorted by order
        foreach (var rule in rules.Rules.OrderBy(r => r.Order))
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            try
            {
                ApplyRule(rule, recordDict, resultDict);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying transformation rule {RuleType} on field {SourceField}", 
                    rule.Type, rule.SourceField);
            }
        }
        
        return result;
    }
    
    public IReadOnlyList<string> ValidateRules(TransformationRules rules)
    {
        var errors = new List<string>();
        
        foreach (var rule in rules.Rules)
        {
            if (string.IsNullOrWhiteSpace(rule.SourceField))
            {
                errors.Add($"Rule #{rule.Id}: Source field is required");
                continue;
            }
            
            if (rule.Type != TransformationType.Custom && string.IsNullOrWhiteSpace(rule.TargetField))
            {
                errors.Add($"Rule #{rule.Id}: Target field is required for {rule.Type} transformation");
            }
            
            switch (rule.Type)
            {
                case TransformationType.Format when string.IsNullOrWhiteSpace(rule.FormatString):
                    errors.Add($"Rule #{rule.Id}: Format string is required for Format transformation");
                    break;
                
                case TransformationType.DateFormat when string.IsNullOrWhiteSpace(rule.FormatString):
                    errors.Add($"Rule #{rule.Id}: Format string is required for DateFormat transformation");
                    break;
                
                case TransformationType.NumberFormat when string.IsNullOrWhiteSpace(rule.FormatString):
                    errors.Add($"Rule #{rule.Id}: Format string is required for NumberFormat transformation");
                    break;
                
                case TransformationType.Custom when rule.Parameters.Count == 0:
                    errors.Add($"Rule #{rule.Id}: Parameters are required for Custom transformation");
                    break;
            }
        }
        
        return errors;
    }
    
    private void ApplyRule(
        TransformationRule rule, 
        IDictionary<string, object?> source, 
        IDictionary<string, object?> target)
    {
        switch (rule.Type)
        {
            case TransformationType.Copy:
                ApplyCopyRule(rule, source, target);
                break;
            
            case TransformationType.Format:
                ApplyFormatRule(rule, source, target);
                break;
            
            case TransformationType.Concatenate:
                ApplyConcatenateRule(rule, source, target);
                break;
            
            case TransformationType.Split:
                ApplySplitRule(rule, source, target);
                break;
            
            case TransformationType.Replace:
                ApplyReplaceRule(rule, source, target);
                break;
            
            case TransformationType.Trim:
                ApplyTrimRule(rule, source, target);
                break;
            
            case TransformationType.Uppercase:
                ApplyUppercaseRule(rule, source, target);
                break;
            
            case TransformationType.Lowercase:
                ApplyLowercaseRule(rule, source, target);
                break;
            
            case TransformationType.Substring:
                ApplySubstringRule(rule, source, target);
                break;
            
            case TransformationType.DateFormat:
                ApplyDateFormatRule(rule, source, target);
                break;
            
            case TransformationType.NumberFormat:
                ApplyNumberFormatRule(rule, source, target);
                break;
            
            case TransformationType.Round:
            case TransformationType.Floor:
            case TransformationType.Ceiling:
                ApplyRoundingRule(rule, source, target);
                break;
            
            case TransformationType.Conditional:
                ApplyConditionalRule(rule, source, target);
                break;
            
            case TransformationType.Custom:
                ApplyCustomRule(rule, source, target);
                break;
        }
    }
    
    private void ApplyCopyRule(
        TransformationRule rule, 
        IDictionary<string, object?> source, 
        IDictionary<string, object?> target)
    {
        if (source.TryGetValue(rule.SourceField, out var value))
        {
            target[rule.TargetField!] = value;
        }
    }
    
    private void ApplyFormatRule(
        TransformationRule rule, 
        IDictionary<string, object?> source, 
        IDictionary<string, object?> target)
    {
        if (source.TryGetValue(rule.SourceField, out var value) && value != null)
        {
            try
            {
                target[rule.TargetField!] = string.Format(rule.FormatString!, value);
            }
            catch (Exception)
            {
                target[rule.TargetField!] = value;
            }
        }
    }
    
    private void ApplyConcatenateRule(
        TransformationRule rule, 
        IDictionary<string, object?> source, 
        IDictionary<string, object?> target)
    {
        var sourceFields = rule.SourceField.Split(',');
        var separator = rule.Parameters.TryGetValue("separator", out var sep) ? sep : " ";
        
        var values = sourceFields
            .Where(field => source.TryGetValue(field.Trim(), out var value) && value != null)
            .Select(field => source[field.Trim()]?.ToString() ?? "")
            .ToList();
        
        target[rule.TargetField!] = string.Join(separator, values);
    }
    
    private void ApplySplitRule(
        TransformationRule rule, 
        IDictionary<string, object?> source, 
        IDictionary<string, object?> target)
    {
        if (source.TryGetValue(rule.SourceField, out var value) && value != null)
        {
            var separator = rule.Parameters.TryGetValue("separator", out var sep) ? sep : ",";
            var index = rule.Parameters.TryGetValue("index", out var idx) && int.TryParse(idx, out var idxValue) ? idxValue : 0;
            
            var stringValue = value.ToString() ?? "";
            var parts = stringValue.Split(new[] { separator }, StringSplitOptions.None);
            
            if (index >= 0 && index < parts.Length)
            {
                target[rule.TargetField!] = parts[index];
            }
        }
    }
    
    private void ApplyReplaceRule(
        TransformationRule rule, 
        IDictionary<string, object?> source, 
        IDictionary<string, object?> target)
    {
        if (source.TryGetValue(rule.SourceField, out var value) && value != null)
        {
            var oldValue = rule.Parameters.TryGetValue("oldValue", out var old) ? old : "";
            var newValue = rule.Parameters.TryGetValue("newValue", out var newVal) ? newVal : "";
            
            var stringValue = value.ToString() ?? "";
            target[rule.TargetField!] = stringValue.Replace(oldValue, newValue);
        }
    }
    
    private void ApplyTrimRule(
        TransformationRule rule, 
        IDictionary<string, object?> source, 
        IDictionary<string, object?> target)
    {
        if (source.TryGetValue(rule.SourceField, out var value) && value != null)
        {
            var stringValue = value.ToString() ?? "";
            target[rule.TargetField!] = stringValue.Trim();
        }
    }
    
    private void ApplyUppercaseRule(
        TransformationRule rule, 
        IDictionary<string, object?> source, 
        IDictionary<string, object?> target)
    {
        if (source.TryGetValue(rule.SourceField, out var value) && value != null)
        {
            var stringValue = value.ToString() ?? "";
            target[rule.TargetField!] = stringValue.ToUpper();
        }
    }
    
    private void ApplyLowercaseRule(
        TransformationRule rule, 
        IDictionary<string, object?> source, 
        IDictionary<string, object?> target)
    {
        if (source.TryGetValue(rule.SourceField, out var value) && value != null)
        {
            var stringValue = value.ToString() ?? "";
            target[rule.TargetField!] = stringValue.ToLower();
        }
    }
    
    private void ApplySubstringRule(
        TransformationRule rule, 
        IDictionary<string, object?> source, 
        IDictionary<string, object?> target)
    {
        if (source.TryGetValue(rule.SourceField, out var value) && value != null)
        {
            var startIndex = rule.Parameters.TryGetValue("startIndex", out var start) && int.TryParse(start, out var startValue) ? startValue : 0;
            var length = rule.Parameters.TryGetValue("length", out var len) && int.TryParse(len, out var lenValue) ? lenValue : -1;
            
            var stringValue = value.ToString() ?? "";
            
            if (startIndex < 0)
            {
                startIndex = 0;
            }
            
            if (startIndex >= stringValue.Length)
            {
                target[rule.TargetField!] = "";
                return;
            }
            
            if (length < 0 || startIndex + length > stringValue.Length)
            {
                target[rule.TargetField!] = stringValue.Substring(startIndex);
            }
            else
            {
                target[rule.TargetField!] = stringValue.Substring(startIndex, length);
            }
        }
    }
    
    private void ApplyDateFormatRule(
        TransformationRule rule, 
        IDictionary<string, object?> source, 
        IDictionary<string, object?> target)
    {
        if (source.TryGetValue(rule.SourceField, out var value) && value != null)
        {
            if (value is DateTime dateTime)
            {
                target[rule.TargetField!] = dateTime.ToString(rule.FormatString);
            }
            else if (value is DateTimeOffset dateTimeOffset)
            {
                target[rule.TargetField!] = dateTimeOffset.ToString(rule.FormatString);
            }
            else if (DateTime.TryParse(value.ToString(), out var parsedDateTime))
            {
                target[rule.TargetField!] = parsedDateTime.ToString(rule.FormatString);
            }
            else
            {
                target[rule.TargetField!] = value;
            }
        }
    }
    
    private void ApplyNumberFormatRule(
        TransformationRule rule, 
        IDictionary<string, object?> source, 
        IDictionary<string, object?> target)
    {
        if (source.TryGetValue(rule.SourceField, out var value) && value != null)
        {
            if (value is IFormattable formattable)
            {
                target[rule.TargetField!] = formattable.ToString(rule.FormatString, null);
            }
            else if (double.TryParse(value.ToString(), out var number))
            {
                target[rule.TargetField!] = number.ToString(rule.FormatString);
            }
            else
            {
                target[rule.TargetField!] = value;
            }
        }
    }
    
    private void ApplyRoundingRule(
        TransformationRule rule, 
        IDictionary<string, object?> source, 
        IDictionary<string, object?> target)
    {
        if (source.TryGetValue(rule.SourceField, out var value) && value != null)
        {
            if (double.TryParse(value.ToString(), out var number))
            {
                target[rule.TargetField!] = rule.Type switch
                {
                    TransformationType.Round => Math.Round(number),
                    TransformationType.Floor => Math.Floor(number),
                    TransformationType.Ceiling => Math.Ceiling(number),
                    _ => number
                };
            }
            else
            {
                target[rule.TargetField!] = value;
            }
        }
    }
    
    private void ApplyConditionalRule(
        TransformationRule rule, 
        IDictionary<string, object?> source, 
        IDictionary<string, object?> target)
    {
        if (source.TryGetValue(rule.SourceField, out var value))
        {
            var condition = rule.Parameters.TryGetValue("condition", out var cond) ? cond : "";
            var trueValue = rule.Parameters.TryGetValue("trueValue", out var trueVal) ? trueVal : "";
            var falseValue = rule.Parameters.TryGetValue("falseValue", out var falseVal) ? falseVal : "";
            
            bool conditionMet = EvaluateCondition(condition, value);
            
            target[rule.TargetField!] = conditionMet ? trueValue : falseValue;
        }
    }
    
    private bool EvaluateCondition(string condition, object? value)
    {
        if (string.IsNullOrWhiteSpace(condition) || value == null)
        {
            return false;
        }
        
        // Simple condition evaluation, in a real app this would be more sophisticated
        if (condition.StartsWith("=="))
        {
            var compareTo = condition.Substring(2).Trim();
            return value.ToString() == compareTo;
        }
        
        if (condition.StartsWith("!="))
        {
            var compareTo = condition.Substring(2).Trim();
            return value.ToString() != compareTo;
        }
        
        if (condition.StartsWith(">") && double.TryParse(value.ToString(), out var doubleValue))
        {
            var compareTo = condition.Substring(1).Trim();
            if (double.TryParse(compareTo, out var compareToValue))
            {
                return doubleValue > compareToValue;
            }
        }
        
        if (condition.StartsWith("<") && double.TryParse(value.ToString(), out var doubleValue2))
        {
            var compareTo = condition.Substring(1).Trim();
            if (double.TryParse(compareTo, out var compareToValue))
            {
                return doubleValue2 < compareToValue;
            }
        }
        
        if (condition.StartsWith("contains"))
        {
            var compareTo = condition.Substring(8).Trim();
            return value.ToString()?.Contains(compareTo) ?? false;
        }
        
        if (condition.StartsWith("startsWith"))
        {
            var compareTo = condition.Substring(10).Trim();
            return value.ToString()?.StartsWith(compareTo) ?? false;
        }
        
        if (condition.StartsWith("endsWith"))
        {
            var compareTo = condition.Substring(8).Trim();
            return value.ToString()?.EndsWith(compareTo) ?? false;
        }
        
        return false;
    }
    
    private void ApplyCustomRule(
        TransformationRule rule, 
        IDictionary<string, object?> source, 
        IDictionary<string, object?> target)
    {
        // Custom rules are handled by external plugins in a real app
        // For now, we'll just copy the source field to the target field if provided
        if (rule.TargetField != null && source.TryGetValue(rule.SourceField, out var value))
        {
            target[rule.TargetField] = value;
        }
    }
    
    private class TransformingAsyncEnumerable : IAsyncEnumerable<ExpandoObject>
    {
        private readonly DataTransformationService _service;
        private readonly IAsyncEnumerable<ExpandoObject> _source;
        private readonly TransformationRules _rules;
        private readonly CancellationToken _cancellationToken;
        
        public TransformingAsyncEnumerable(
            DataTransformationService service,
            IAsyncEnumerable<ExpandoObject> source,
            TransformationRules rules,
            CancellationToken cancellationToken)
        {
            _service = service;
            _source = source;
            _rules = rules;
            _cancellationToken = cancellationToken;
        }
        
        public IAsyncEnumerator<ExpandoObject> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(
                _cancellationToken, cancellationToken).Token;
            
            return new TransformingAsyncEnumerator(_service, _source.GetAsyncEnumerator(combinedToken), _rules, combinedToken);
        }
        
        private class TransformingAsyncEnumerator : IAsyncEnumerator<ExpandoObject>
        {
            private readonly DataTransformationService _service;
            private readonly IAsyncEnumerator<ExpandoObject> _source;
            private readonly TransformationRules _rules;
            private readonly CancellationToken _cancellationToken;
            
            public TransformingAsyncEnumerator(
                DataTransformationService service,
                IAsyncEnumerator<ExpandoObject> source,
                TransformationRules rules,
                CancellationToken cancellationToken)
            {
                _service = service;
                _source = source;
                _rules = rules;
                _cancellationToken = cancellationToken;
            }
            
            public ExpandoObject Current { get; private set; } = new ExpandoObject();
            
            public async ValueTask<bool> MoveNextAsync()
            {
                if (await _source.MoveNextAsync())
                {
                    Current = await _service.TransformRecordAsync(_source.Current, _rules, _cancellationToken);
                    return true;
                }
                
                return false;
            }
            
            public ValueTask DisposeAsync()
            {
                return _source.DisposeAsync();
            }
        }
    }
}

## src/DataProcessingService.Infrastructure/Services/ETL/DataLoadService.cs
```csharp
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using DataProcessingService.Core.Domain.Entities;
using DataProcessingService.Core.Domain.Enums;
using DataProcessingService.Core.Interfaces.Services.ETL;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Npgsql;
using MySqlConnector;

namespace DataProcessingService.Infrastructure.Services.ETL;

public class DataLoadService : IDataLoadService
{
    private readonly ILogger<DataLoadService> _logger;
    
    public DataLoadService(ILogger<DataLoadService> logger)
    {
        _logger = logger;
    }
    
    public async Task<int> LoadDataAsync(
        DataSource destination,
        string tableName,
        IEnumerable<ExpandoObject> data,
        bool createIfNotExists = false,
        bool truncateBeforeLoad = false,
        CancellationToken cancellationToken = default)
    {
        var dataList = data.ToList();
        if (!dataList.Any())
        {
            _logger.LogWarning("No data to load into table {TableName}", tableName);
            return 0;
        }
        
        using var connection = CreateConnection(destination);
        await connection.OpenAsync(cancellationToken);
        using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        
        try
        {
            // Check if table exists
            bool tableExists = await TableExistsAsync(destination, tableName, connection, transaction, cancellationToken);
            
            if (!tableExists)
            {
                if (createIfNotExists)
                {
                    // Infer schema from the first record
                    var columns = InferSchema(dataList.First());
                    await CreateTableAsync(destination, tableName, columns, connection, transaction, cancellationToken);
                }
                else
                {
                    throw new InvalidOperationException($"Table {tableName} does not exist in destination database");
                }
            }
            
            // Truncate if requested
            if (truncateBeforeLoad && tableExists)
            {
                string truncateQuery = GetTruncateTableQuery(destination.Type, tableName, destination.Schema);
                await connection.ExecuteAsync(truncateQuery, transaction: transaction);
                
                _logger.LogInformation("Truncated table {TableName}", tableName);
            }
            
            // Insert data in batches
            int batchSize = 1000;
            int totalRowsInserted = 0;
            
            for (int i = 0; i < dataList.Count; i += batchSize)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var batch = dataList.Skip(i).Take(batchSize).ToList();
                if (!batch.Any()) continue;
                
                int rowsInserted = await InsertBatchAsync(
                    connection, 
                    transaction, 
                    destination.Type, 
                    tableName, 
                    destination.Schema,
                    batch,
                    cancellationToken);
                
                totalRowsInserted += rowsInserted;
            }
            
            await transaction.CommitAsync(cancellationToken);
            
            _logger.LogInformation("Loaded {Count} records into table {TableName}", 
                totalRowsInserted, tableName);
            
            return totalRowsInserted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading data into table {TableName}", tableName);
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
    
    public async Task<int> LoadDataStreamAsync(
        DataSource destination,
        string tableName,
        IAsyncEnumerable<ExpandoObject> dataStream,
        bool createIfNotExists = false,
        bool truncateBeforeLoad = false,
        CancellationToken cancellationToken = default)
    {
        // Buffer some data to determine schema if needed
        var buffer = new List<ExpandoObject>();
        int bufferSize = createIfNotExists ? 1 : 0;
        
        await foreach (var item in dataStream.WithCancellation(cancellationToken))
        {
            buffer.Add(item);
            if (buffer.Count >= bufferSize && bufferSize > 0)
            {
                break;
            }
        }
        
        if (buffer.Count == 0)
        {
            _logger.LogWarning("No data to load into table {TableName}", tableName);
            return 0;
        }
        
        using var connection = CreateConnection(destination);
        await connection.OpenAsync(cancellationToken);
        using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        
        try
        {
            // Check if table exists
            bool tableExists = await TableExistsAsync(destination, tableName, connection, transaction, cancellationToken);
            
            if (!tableExists)
            {
                if (createIfNotExists)
                {
                    // Infer schema from the first record
                    var columns = InferSchema(buffer.First());
                    await CreateTableAsync(destination, tableName, columns, connection, transaction, cancellationToken);
                }
                else
                {
                    throw new InvalidOperationException($"Table {tableName} does not exist in destination database");
                }
            }
            
            // Truncate if requested
            if (truncateBeforeLoad && tableExists)
            {
                string truncateQuery = GetTruncateTableQuery(destination.Type, tableName, destination.Schema);
                await connection.ExecuteAsync(truncateQuery, transaction: transaction);
                
                _logger.LogInformation("Truncated table {TableName}", tableName);
            }
            
            // Insert buffered data
            int totalRowsInserted = 0;
            
            if (buffer.Count > 0)
            {
                int rowsInserted = await InsertBatchAsync(
                    connection, 
                    transaction, 
                    destination.Type, 
                    tableName, 
                    destination.Schema,
                    buffer,
                    cancellationToken);
                
                totalRowsInserted += rowsInserted;
            }
            
            // Continue processing the stream in batches
            buffer.Clear();
            int batchSize = 1000;
            
            await foreach (var item in dataStream.WithCancellation(cancellationToken))
            {
                buffer.Add(item);
                
                if (buffer.Count >= batchSize)
                {
                    int rowsInserted = await InsertBatchAsync(
                        connection, 
                        transaction, 
                        destination.Type, 
                        tableName, 
                        destination.Schema,
                        buffer,
                        cancellationToken);
                    
                    totalRowsInserted += rowsInserted;
                    buffer.Clear();
                }
            }
            
            // Insert any remaining items
            if (buffer.Count > 0)
            {
                int rowsInserted = await InsertBatchAsync(
                    connection, 
                    transaction, 
                    destination.Type, 
                    tableName, 
                    destination.Schema,
                    buffer,
                    cancellationToken);
                
                totalRowsInserted += rowsInserted;
            }
            
            await transaction.CommitAsync(cancellationToken);
            
            _logger.LogInformation("Loaded {Count} records into table {TableName}", 
                totalRowsInserted, tableName);
            
            return totalRowsInserted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading data stream into table {TableName}", tableName);
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
    
    public async Task<bool> TableExistsAsync(
        DataSource destination,
        string tableName,
        CancellationToken cancellationToken = default)
    {
        using var connection = CreateConnection(destination);
        await connection.OpenAsync(cancellationToken);
        
        return await TableExistsAsync(destination, tableName, connection, null, cancellationToken);
    }
    
    public async Task<bool> CreateTableAsync(
        DataSource destination,
        string tableName,
        IEnumerable<(string Name, string Type)> columns,
        CancellationToken cancellationToken = default)
    {
        using var connection = CreateConnection(destination);
        await connection.OpenAsync(cancellationToken);
        using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        
        try
        {
            await CreateTableAsync(destination, tableName, columns, connection, transaction, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating table {TableName}", tableName);
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }
    }
    
    private async Task<bool> TableExistsAsync(
        DataSource destination,
        string tableName,
        DbConnection connection,
        DbTransaction? transaction,
        CancellationToken cancellationToken = default)
    {
        string query = GetTableExistsQuery(destination.Type, tableName, destination.Schema);
        
        var result = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(query, transaction: transaction, cancellationToken: cancellationToken));
        
        return result > 0;
    }
    
    private async Task CreateTableAsync(
        DataSource destination,
        string tableName,
        IEnumerable<(string Name, string Type)> columns,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        string createTableQuery = GetCreateTableQuery(destination.Type, tableName, destination.Schema, columns);
        
        await connection.ExecuteAsync(
            new CommandDefinition(createTableQuery, transaction: transaction, cancellationToken: cancellationToken));
        
        _logger.LogInformation("Created table {TableName} with {ColumnCount} columns", 
            tableName, columns.Count());
    }
    
    private async Task<int> InsertBatchAsync(
        DbConnection connection,
        DbTransaction transaction,
        DataSourceType destinationType,
        string tableName,
        string? schema,
        IEnumerable<ExpandoObject> batch,
        CancellationToken cancellationToken = default)
    {
        var items = batch.ToList();
        if (!items.Any())
        {
            return 0;
        }
        
        // Get all column names from the first record
        var firstItem = items.First() as IDictionary<string, object?>;
        var columnNames = firstItem.Keys.ToList();
        
        string insertQuery = GetBulkInsertQuery(destinationType, tableName, schema, columnNames);
        
        int rowsInserted = 0;
        
        // Different approaches based on database type
        switch (destinationType)
        {
            case DataSourceType.SqlServer:
                // Use SqlBulkCopy for SQL Server
                if (connection is SqlConnection sqlConnection)
                {
                    using var bulkCopy = new SqlBulkCopy(sqlConnection, SqlBulkCopyOptions.Default, (SqlTransaction?)transaction);
                    bulkCopy.DestinationTableName = GetFullTableName(destinationType, tableName, schema);
                    
                    foreach (var column in columnNames)
                    {
                        bulkCopy.ColumnMappings.Add(column, column);
                    }
                    
                    var dataTable = new DataTable();
                    foreach (var column in columnNames)
                    {
                        dataTable.Columns.Add(column, typeof(object));
                    }
                    
                    foreach (var item in items)
                    {
                        var row = dataTable.NewRow();
                        var itemDict = item as IDictionary<string, object?>;
                        
                        foreach (var column in columnNames)
                        {
                            row[column] = itemDict[column] ?? DBNull.Value;
                        }
                        
                        dataTable.Rows.Add(row);
                    }
                    
                    await bulkCopy.WriteToServerAsync(dataTable, cancellationToken);
                    rowsInserted = items.Count;
                }
                break;
            
            case DataSourceType.PostgreSql:
                // Use Npgsql copy for PostgreSQL
                if (connection is NpgsqlConnection npgsqlConnection)
                {
                    using var writer = await npgsqlConnection.BeginBinaryImportAsync(
                        $"COPY {GetFullTableName(destinationType, tableName, schema)} ({string.Join(", ", columnNames.Select(c => "\"" + c + "\""))}) FROM STDIN (FORMAT BINARY)",
                        cancellationToken);
                    
                    foreach (var item in items)
                    {
                        await writer.StartRowAsync(cancellationToken);
                        var itemDict = item as IDictionary<string, object?>;
                        
                        foreach (var column in columnNames)
                        {
                            if (itemDict[column] == null)
                            {
                                await writer.WriteNullAsync(cancellationToken);
                            }
                            else
                            {
                                await writer.WriteValueAsync(itemDict[column], cancellationToken);
                            }
                        }
                    }
                    
                    rowsInserted = (int)await writer.CompleteAsync(cancellationToken);
                }
                break;
            
            default:
                // Use standard Dapper insert for other databases
                foreach (var item in items)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    await connection.ExecuteAsync(
                        new CommandDefinition(insertQuery, item, transaction, cancellationToken: cancellationToken));
                    
                    rowsInserted++;
                }
                break;
        }
        
        return rowsInserted;
    }
    
    private DbConnection CreateConnection(DataSource destination)
    {
        return destination.Type switch
        {
            DataSourceType.SqlServer => new SqlConnection(destination.ConnectionString),
            DataSourceType.PostgreSql => new NpgsqlConnection(destination.ConnectionString),
            DataSourceType.MySql => new MySqlConnection(destination.ConnectionString),
            _ => throw new NotSupportedException($"Data source type {destination.Type} is not supported")
        };
    }
    
    private string GetTableExistsQuery(DataSourceType sourceType, string tableName, string? schema)
    {
        return sourceType switch
        {
            DataSourceType.SqlServer => 
                $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = '{(schema ?? "dbo")}' AND TABLE_NAME = '{tableName}'",
            
            DataSourceType.PostgreSql => 
                $"SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = '{(schema ?? "public")}' AND table_name = '{tableName}'",
            
            DataSourceType.MySql => 
                $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = '{(schema ?? "database()")}' AND TABLE_NAME = '{tableName}'",
            
            _ => throw new NotSupportedException($"Data source type {sourceType} is not supported")
        };
    }
    
    private string GetTruncateTableQuery(DataSourceType sourceType, string tableName, string? schema)
    {
        string fullTableName = GetFullTableName(sourceType, tableName, schema);
        
        return sourceType switch
        {
            DataSourceType.SqlServer => $"TRUNCATE TABLE {fullTableName}",
            DataSourceType.PostgreSql => $"TRUNCATE TABLE {fullTableName}",
            DataSourceType.MySql => $"TRUNCATE TABLE {fullTableName}",
            _ => throw new NotSupportedException($"Data source type {sourceType} is not supported")
        };
    }
    
    private string GetCreateTableQuery(
        DataSourceType sourceType, 
        string tableName, 
        string? schema, 
        IEnumerable<(string Name, string Type)> columns)
    {
        string fullTableName = GetFullTableName(sourceType, tableName, schema);
        
        var columnDefinitions = new StringBuilder();
        bool isFirst = true;
        
        foreach (var column in columns)
        {
            if (!isFirst)
            {
                columnDefinitions.Append(", ");
            }
            
            string columnType = MapColumnType(sourceType, column.Type);
            
            switch (sourceType)
            {
                case DataSourceType.SqlServer:
                    columnDefinitions.Append($"[{column.Name}] {columnType}");
                    break;
                case DataSourceType.PostgreSql:
                    columnDefinitions.Append($"\"{column.Name}\" {columnType}");
                    break;
                case DataSourceType.MySql:
                    columnDefinitions.Append($"`{column.Name}` {columnType}");
                    break;
                default:
                    columnDefinitions.Append($"{column.Name} {columnType}");
                    break;
            }
            
            isFirst = false;
        }
        
        return $"CREATE TABLE {fullTableName} ({columnDefinitions})";
    }
    
    private string GetBulkInsertQuery(
        DataSourceType sourceType, 
        string tableName, 
        string? schema, 
        IEnumerable<string> columnNames)
    {
        string fullTableName = GetFullTableName(sourceType, tableName, schema);
        var columns = string.Join(", ", columnNames.Select(c => GetQuotedColumnName(sourceType, c)));
        var parameters = string.Join(", ", columnNames.Select(c => $"@{c}"));
        
        return $"INSERT INTO {fullTableName} ({columns}) VALUES ({parameters})";
    }
    
    private string GetFullTableName(DataSourceType sourceType, string tableName, string? schema)
    {
        return sourceType switch
        {
            DataSourceType.SqlServer => schema != null ? $"[{schema}].[{tableName}]" : $"[{tableName}]",
            DataSourceType.PostgreSql => schema != null ? $"\"{schema}\".\"{tableName}\"" : $"\"{tableName}\"",
            DataSourceType.MySql => schema != null ? $"`{schema}`.`{tableName}`" : $"`{tableName}`",
            _ => tableName
        };
    }
    
    private string GetQuotedColumnName(DataSourceType sourceType, string columnName)
    {
        return sourceType switch
        {
            DataSourceType.SqlServer => $"[{columnName}]",
            DataSourceType.PostgreSql => $"\"{columnName}\"",
            DataSourceType.MySql => $"`{columnName}`",
            _ => columnName
        };
    }
    
    private string MapColumnType(DataSourceType sourceType, string type)
    {
        // Simple mapping for common types, in a real app this would be more comprehensive
        string normalizedType = type.ToLower();
        
        return sourceType switch
        {
            DataSourceType.SqlServer => normalizedType switch
            {
                "string" => "NVARCHAR(MAX)",
                "int" => "INT",
                "integer" => "INT",
                "long" => "BIGINT",
                "double" => "FLOAT",
                "decimal" => "DECIMAL(18, 6)",
                "bool" => "BIT",
                "boolean" => "BIT",
                "datetime" => "DATETIME2",
                "date" => "DATE",
                "time" => "TIME",
                "guid" => "UNIQUEIDENTIFIER",
                "binary" => "VARBINARY(MAX)",
                _ => "NVARCHAR(MAX)"
            },
            
            DataSourceType.PostgreSql => normalizedType switch
            {
                "string" => "TEXT",
                "int" => "INTEGER",
                "integer" => "INTEGER",
                "long" => "BIGINT",
                "double" => "DOUBLE PRECISION",
                "decimal" => "NUMERIC(18, 6)",
                "bool" => "BOOLEAN",
                "boolean" => "BOOLEAN",
                "datetime" => "TIMESTAMP",
                "date" => "DATE",
                "time" => "TIME",
                "guid" => "UUID",
                "binary" => "BYTEA",
                _ => "TEXT"
            },
            
            DataSourceType.MySql => normalizedType switch
            {
                "string" => "TEXT",
                "int" => "INT",
                "integer" => "INT",
                "long" => "BIGINT",
                "double" => "DOUBLE",
                "decimal" => "DECIMAL(18, 6)",
                "bool" => "TINYINT(1)",
                "boolean" => "TINYINT(1)",
                "datetime" => "DATETIME",
                "date" => "DATE",
                "time" => "TIME",
                "guid" => "CHAR(36)",
                "binary" => "BLOB",
                _ => "TEXT"
            },
            
            _ => type
        };
    }
    
    private IEnumerable<(string Name, string Type)> InferSchema(ExpandoObject record)
    {
        var result = new List<(string Name, string Type)>();
        var recordDict = record as IDictionary<string, object?>;
        
        foreach (var field in recordDict)
        {
            string type = field.Value?.GetType().Name ?? "String";
            
            // Map .NET types to database types
            type = type.ToLower() switch
            {
                "string" => "String",
                "int32" => "Int",
                "int64" => "Long",
                "double" => "Double",
                "decimal" => "Decimal",
                "boolean" => "Boolean",
                "datetime" => "DateTime",
                "datetimeoffset" => "DateTime",
                "guid" => "Guid",
                "byte[]" => "Binary",
                _ => "String"
            };
            
            result.Add((field.Key, type));
        }
        
        return result;
    }
}

## src/DataProcessingService.Infrastructure/Services/ETL/DataConsistencyService.cs
```csharp
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Dynamic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using DataProcessingService.Core.Domain.Entities;
using DataProcessingService.Core.Interfaces.Services.ETL;
using Microsoft.Extensions.Logging;

namespace DataProcessingService.Infrastructure.Services.ETL;

public class DataConsistencyService : IDataConsistencyService
{
    private readonly IDataExtractionService _extractionService;
    private readonly IDataLoadService _loadService;
    private readonly ILogger<DataConsistencyService> _logger;
    
    public DataConsistencyService(
        IDataExtractionService extractionService,
        IDataLoadService loadService,
        ILogger<DataConsistencyService> logger)
    {
        _extractionService = extractionService;
        _loadService = loadService;
        _logger = logger;
    }
    
    public async Task<bool> ValidateDataConsistencyAsync(
        DataSource source,
        DataSource destination,
        string sourceTable,
        string destinationTable,
        IEnumerable<string> keyColumns,
        CancellationToken cancellationToken = default)
    {
        var metrics = await GetConsistencyMetricsAsync(
            source, 
            destination, 
            sourceTable, 
            destinationTable, 
            keyColumns, 
            null, 
            cancellationToken);
        
        _logger.LogInformation(
            "Consistency validation: MissingRecords={MissingRecords}, DifferentRecords={DifferentRecords}, TotalRecords={TotalRecords}",
            metrics.MissingRecords, metrics.DifferentRecords, metrics.TotalRecords);
        
        return metrics.MissingRecords == 0 && metrics.DifferentRecords == 0;
    }
    
    public async Task<(int MissingRecords, int DifferentRecords, int TotalRecords)> GetConsistencyMetricsAsync(
        DataSource source,
        DataSource destination,
        string sourceTable,
        string destinationTable,
        IEnumerable<string> keyColumns,
        IEnumerable<string>? compareColumns = null,
        CancellationToken cancellationToken = default)
    {
        var keysList = keyColumns.ToList();
        
        if (!keysList.Any())
        {
            throw new ArgumentException("At least one key column must be provided", nameof(keyColumns));
        }
        
        // Get source and destination records
        var sourceRecords = await GetRecordsWithKeysAsync(
            source, 
            sourceTable, 
            keysList, 
            compareColumns, 
            cancellationToken);
        
        var destinationRecords = await GetRecordsWithKeysAsync(
            destination, 
            destinationTable, 
            keysList, 
            compareColumns, 
            cancellationToken);
        
        int missingRecords = 0;
        int differentRecords = 0;
        
        // Check for missing or different records
        foreach (var sourceRecord in sourceRecords)
        {
            string recordKey = GetRecordKey(sourceRecord, keysList);
            
            if (!destinationRecords.TryGetValue(recordKey, out var destinationRecord))
            {
                missingRecords++;
                continue;
            }
            
            if (!AreRecordsEqual(sourceRecord, destinationRecord, compareColumns?.ToList()))
            {
                differentRecords++;
            }
        }
        
        return (missingRecords, differentRecords, sourceRecords.Count);
    }
    
    public async Task<int> SynchronizeDataAsync(
        DataSource source,
        DataSource destination,
        string sourceTable,
        string destinationTable,
        IEnumerable<string> keyColumns,
        IEnumerable<string>? syncColumns = null,
        CancellationToken cancellationToken = default)
    {
        var keysList = keyColumns.ToList();
        var syncColumnsList = syncColumns?.ToList() ?? new List<string>();
        
        if (!keysList.Any())
        {
            throw new ArgumentException("At least one key column must be provided", nameof(keyColumns));
        }
        
        // If no sync columns specified, get all columns from source table
        if (!syncColumnsList.Any())
        {
            var sourceSchema = await _extractionService.GetTableSchemaAsync(source, sourceTable, cancellationToken);
            syncColumnsList = sourceSchema.Select(s => s.Name).Except(keysList).ToList();
        }
        
        var allColumns = keysList.Concat(syncColumnsList).ToList();
        
        // Get source and destination records
        var sourceRecords = await GetRecordsWithKeysAsync(
            source, 
            sourceTable, 
            keysList, 
            allColumns, 
            cancellationToken);
        
        var destinationRecords = await GetRecordsWithKeysAsync(
            destination, 
            destinationTable, 
            keysList, 
            allColumns, 
            cancellationToken);
        
        int recordsUpdated = 0;
        var recordsToInsert = new List<ExpandoObject>();
        var recordsToUpdate = new List<(ExpandoObject Record, string RecordKey)>();
        
        // Identify records to insert or update
        foreach (var sourceRecord in sourceRecords.Values)
        {
            string recordKey = GetRecordKey(sourceRecord, keysList);
            
            if (!destinationRecords.TryGetValue(recordKey, out var destinationRecord))
            {
                recordsToInsert.Add(sourceRecord);
            }
            else if (!AreRecordsEqual(sourceRecord, destinationRecord, syncColumnsList))
            {
                recordsToUpdate.Add((sourceRecord, recordKey));
            }
        }
        
        _logger.LogInformation(
            "Data synchronization: InsertCount={InsertCount}, UpdateCount={UpdateCount}",
            recordsToInsert.Count, recordsToUpdate.Count);
        
        // Insert new records
        if (recordsToInsert.Any())
        {
            int inserted = await _loadService.LoadDataAsync(
                destination,
                destinationTable,
                recordsToInsert,
                false,
                false,
                cancellationToken);
            
            recordsUpdated += inserted;
        }
        
        // Update existing records
        if (recordsToUpdate.Any())
        {
            int updated = await UpdateRecordsAsync(
                destination,
                destinationTable,
                recordsToUpdate,
                keysList,
                syncColumnsList,
                cancellationToken);
            
            recordsUpdated += updated;
        }
        
        return recordsUpdated;
    }
    
    public async Task<(string Hash, DateTimeOffset Timestamp)> ComputeTableChecksumAsync(
        DataSource source,
        string tableName,
        IEnumerable<string>? columns = null,
        CancellationToken cancellationToken = default)
    {
        var columnsList = columns?.ToList();
        
        // Get all columns if not specified
        if (columnsList == null || !columnsList.Any())
        {
            var tableSchema = await _extractionService.GetTableSchemaAsync(source, tableName, cancellationToken);
            columnsList = tableSchema.Select(s => s.Name).ToList();
        }
        
        // Build query to select all data ordered consistently
        string columnsSql = string.Join(", ", columnsList);
        string query = $"SELECT {columnsSql} FROM {tableName} ORDER BY {columnsList.First()}";
        
        // Extract data
        var records = await _extractionService.ExtractDataAsync(source, query, null, cancellationToken);
        
        // Compute hash
        using var sha256 = SHA256.Create();
        var hashBytes = new List<byte>();
        
        // Add each record's values to the hash
        foreach (var record in records)
        {
            var recordDict = record as IDictionary<string, object?>;
            
            foreach (var column in columnsList)
            {
                var value = recordDict.TryGetValue(column, out var val) ? val?.ToString() ?? "NULL" : "NULL";
                hashBytes.AddRange(Encoding.UTF8.GetBytes(value));
            }
        }
        
        string hash = Convert.ToBase64String(sha256.ComputeHash(hashBytes.ToArray()));
        
        _logger.LogInformation("Computed checksum for table {TableName}: {Hash}", tableName, hash);
        
        return (hash, DateTimeOffset.UtcNow);
    }
    
    private async Task<Dictionary<string, ExpandoObject>> GetRecordsWithKeysAsync(
        DataSource source,
        string tableName,
        List<string> keyColumns,
        IEnumerable<string>? additionalColumns = null,
        CancellationToken cancellationToken = default)
    {
        var columns = new HashSet<string>(keyColumns);
        
        if (additionalColumns != null)
        {
            foreach (var column in additionalColumns)
            {
                columns.Add(column);
            }
        }
        
        string columnsSql = string.Join(", ", columns);
        string query = $"SELECT {columnsSql} FROM {tableName}";
        
        var records = await _extractionService.ExtractDataAsync(source, query, null, cancellationToken);
        
        var result = new Dictionary<string, ExpandoObject>();
        
        foreach (var record in records)
        {
            string recordKey = GetRecordKey(record, keyColumns);
            result[recordKey] = record;
        }
        
        return result;
    }
    
    private string GetRecordKey(ExpandoObject record, List<string> keyColumns)
    {
        var recordDict = record as IDictionary<string, object?>;
        var keyParts = new List<string>();
        
        foreach (var column in keyColumns)
        {
            var value = recordDict.TryGetValue(column, out var val) ? val?.ToString() ?? "NULL" : "NULL";
            keyParts.Add(value);
        }
        
        return string.Join("|", keyParts);
    }
    
    private bool AreRecordsEqual(
        ExpandoObject record1, 
        ExpandoObject record2, 
        List<string>? compareColumns = null)
    {
        var dict1 = record1 as IDictionary<string, object?>;
        var dict2 = record2 as IDictionary<string, object?>;
        
        var columns = compareColumns ?? dict1.Keys.ToList();
        
        foreach (var column in columns)
        {
            var value1 = dict1.TryGetValue(column, out var val1) ? val1 : null;
            var value2 = dict2.TryGetValue(column, out var val2) ? val2 : null;
            
            if (value1 == null && value2 == null)
            {
                continue;
            }
            
            if (value1 == null || value2 == null)
            {
                return false;
            }
            
            if (!value1.Equals(value2))
            {
                return false;
            }
        }
        
        return true;
    }
    
    private async Task<int> UpdateRecordsAsync(
        DataSource destination,
        string tableName,
        List<(ExpandoObject Record, string RecordKey)> recordsToUpdate,
        List<string> keyColumns,
        List<string> updateColumns,
        CancellationToken cancellationToken = default)
    {
        int updatedCount = 0;
        
        foreach (var (record, _) in recordsToUpdate)
        {
            var recordDict = record as IDictionary<string, object?>;
            
            var updateParts = updateColumns
                .Select(c => $"{c} = @{c}")
                .ToList();
            
            var whereParts = keyColumns
                .Select(c => $"{c} = @{c}")
                .ToList();
            
            string updateSql = $"UPDATE {tableName} SET {string.Join(", ", updateParts)} WHERE {string.Join(" AND ", whereParts)}";
            
            try
            {
                // In a real app, this would be batched
                await _extractionService.ExtractDataAsync(
                    destination, 
                    updateSql, 
                    recordDict.ToDictionary(kv => kv.Key, kv => kv.Value as object ?? DBNull.Value), 
                    cancellationToken);
                
                updatedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating record in table {TableName}", tableName);
            }
        }
        
        return updatedCount;
    }
}

## src/DataProcessingService.Infrastructure/Services/ETL/DataReplicationService.cs
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.Core.Domain.Entities;
using DataProcessingService.Core.Interfaces.Services.ETL;
using Microsoft.Extensions.Logging;

namespace DataProcessingService.Infrastructure.Services.ETL;

public class DataReplicationService : IDataReplicationService
{
    private readonly IDataExtractionService _extractionService;
    private readonly IDataLoadService _loadService;
    private readonly IDataConsistencyService _consistencyService;
    private readonly ILogger<DataReplicationService> _logger;
    
    public DataReplicationService(
        IDataExtractionService extractionService,
        IDataLoadService loadService,
        IDataConsistencyService consistencyService,
        ILogger<DataReplicationService> logger)
    {
        _extractionService = extractionService;
        _loadService = loadService;
        _consistencyService = consistencyService;
        _logger = logger;
    }
    
    public async Task<int> ReplicateTableAsync(
        DataSource source,
        DataSource destination,
        string sourceTable,
        string? destinationTable = null,
        bool createIfNotExists = false,
        IEnumerable<string>? keyColumns = null,
        ReplicationMode mode = ReplicationMode.Full,
        CancellationToken cancellationToken = default)
    {
        destinationTable ??= sourceTable;
        var keyColumnsList = keyColumns?.ToList();
        
        _logger.LogInformation(
            "Replicating table {SourceTable} to {DestinationTable} with mode {Mode}",
            sourceTable, destinationTable, mode);
        
        // Check if destination table exists
        bool tableExists = await _loadService.TableExistsAsync(destination, destinationTable, cancellationToken);
        
        if (!tableExists && !createIfNotExists)
        {
            throw new InvalidOperationException(
                $"Destination table {destinationTable} does not exist and createIfNotExists is false");
        }
        
        switch (mode)
        {
            case ReplicationMode.Full:
                return await ReplicateFullAsync(
                    source, 
                    destination, 
                    sourceTable, 
                    destinationTable, 
                    createIfNotExists, 
                    cancellationToken);
            
            case ReplicationMode.Incremental:
                if (keyColumnsList == null || !keyColumnsList.Any())
                {
                    throw new ArgumentException("Key columns must be provided for incremental replication", nameof(keyColumns));
                }
                
                return await _consistencyService.SynchronizeDataAsync(
                    source,
                    destination,
                    sourceTable,
                    destinationTable,
                    keyColumnsList,
                    null,
                    cancellationToken);
            
            case ReplicationMode.ChangeDataCapture:
                return await ReplicateCdcAsync(
                    source, 
                    destination, 
                    sourceTable, 
                    destinationTable, 
                    createIfNotExists, 
                    cancellationToken);
            
            default:
                throw new NotSupportedException($"Replication mode {mode} is not supported");
        }
    }
    
    public async Task<IDictionary<string, int>> ReplicateAllTablesAsync(
        DataSource source,
        DataSource destination,
        bool createIfNotExists = false,
        IEnumerable<string>? excludedTables = null,
        ReplicationMode mode = ReplicationMode.Full,
        CancellationToken cancellationToken = default)
    {
        var excludedTablesList = excludedTables?.ToList() ?? new List<string>();
        
        // Get list of tables from source
        var tables = await _extractionService.GetAvailableTablesAsync(source, cancellationToken);
        
        var result = new Dictionary<string, int>();
        
        foreach (var table in tables)
        {
            if (excludedTablesList.Contains(table))
            {
                _logger.LogInformation("Skipping excluded table {TableName}", table);
                continue;
            }
            
            try
            {
                int recordsReplicated = await ReplicateTableAsync(
                    source,
                    destination,
                    table,
                    null,
                    createIfNotExists,
                    null,
                    mode,
                    cancellationToken);
                
                result[table] = recordsReplicated;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error replicating table {TableName}", table);
                result[table] = -1;
            }
        }
        
        return result;
    }
    
    public async Task<int> ReplicateIncrementalAsync(
        DataSource source,
        DataSource destination,
        string sourceTable,
        string? destinationTable = null,
        string changeTrackingColumn = "LastModified",
        CancellationToken cancellationToken = default)
    {
        destinationTable ??= sourceTable;
        
        _logger.LogInformation(
            "Replicating incremental changes from {SourceTable} to {DestinationTable} using tracking column {TrackingColumn}",
            sourceTable, destinationTable, changeTrackingColumn);
        
        // Determine the last sync time
        DateTimeOffset lastSyncTime = await GetLastSyncTimeAsync(
            destination, 
            destinationTable, 
            changeTrackingColumn, 
            cancellationToken);
        
        // Extract changed records
        string query = $"SELECT * FROM {sourceTable} WHERE {changeTrackingColumn} > @LastSyncTime";
        var parameters = new Dictionary<string, object> { ["LastSyncTime"] = lastSyncTime };
        
        var changedRecords = await _extractionService.ExtractDataAsync(
            source, 
            query, 
            parameters, 
            cancellationToken);
        
        // Load changed records to destination
        int recordsLoaded = await _loadService.LoadDataAsync(
            destination,
            destinationTable,
            changedRecords,
            false,
            false,
            cancellationToken);
        
        _logger.LogInformation(
            "Replicated {RecordCount} records from {SourceTable} to {DestinationTable}",
            recordsLoaded, sourceTable, destinationTable);
        
        return recordsLoaded;
    }
    
    public async Task ConfigureChangeDataCaptureAsync(
        DataSource source,
        string tableName,
        IEnumerable<string>? columns = null,
        CancellationToken cancellationToken = default)
    {
        // This is a placeholder implementation as the actual CDC configuration is very database-specific
        _logger.LogWarning(
            "ConfigureChangeDataCaptureAsync is a placeholder. Actual CDC configuration depends on the database type.");
        
        // In a real application, this would configure CDC based on the database type:
        // - SQL Server: Enable CDC on the database and table
        // - PostgreSQL: Set up logical replication or trigger-based CDC
        // - MySQL: Configure the binlog for CDC
        
        throw new NotImplementedException("CDC configuration is not implemented for this database type");
    }
    
    private async Task<int> ReplicateFullAsync(
        DataSource source,
        DataSource destination,
        string sourceTable,
        string destinationTable,
        bool createIfNotExists,
        CancellationToken cancellationToken)
    {
        // Get all records from source
        string query = $"SELECT * FROM {sourceTable}";
        var records = await _extractionService.ExtractDataAsync(source, query, null, cancellationToken);
        
        // Load to destination with truncate
        int recordsLoaded = await _loadService.LoadDataAsync(
            destination,
            destinationTable,
            records,
            createIfNotExists,
            true, // truncate before load
            cancellationToken);
        
        _logger.LogInformation(
            "Replicated {RecordCount} records from {SourceTable} to {DestinationTable}",
            recordsLoaded, sourceTable, destinationTable);
        
        return recordsLoaded;
    }
    
    private async Task<int> ReplicateCdcAsync(
        DataSource source,
        DataSource destination,
        string sourceTable,
        string destinationTable,
        bool createIfNotExists,
        CancellationToken cancellationToken)
    {
        // This is a simplified implementation - real CDC would use the CDC capabilities of the database
        _logger.LogWarning(
            "Using simplified CDC implementation. For production, use database-native CDC capabilities.");
        
        // In a real application, this would:
        // - For SQL Server: Query the CDC tables for changes
        // - For PostgreSQL: Use logical replication slots
        // - For MySQL: Read from the binary log
        
        // For now, treat it as incremental replication with LastModified
        return await ReplicateIncrementalAsync(
            source,
            destination,
            sourceTable,
            destinationTable,
            "LastModified",
            cancellationToken);
    }
    
    private async Task<DateTimeOffset> GetLastSyncTimeAsync(
        DataSource destination,
        string tableName,
        string changeTrackingColumn,
        CancellationToken cancellationToken)
    {
        try
        {
            // Try to get the maximum timestamp from the destination table
            string query = $"SELECT MAX({changeTrackingColumn}) FROM {tableName}";
            var result = await _extractionService.ExtractDataAsync(destination, query, null, cancellationToken);
            
            var firstRow = result.FirstOrDefault();
            if (firstRow != null)
            {
                var rowDict = firstRow as IDictionary<string, object?>;
                var maxValue = rowDict.Values.FirstOrDefault();
                
                if (maxValue != null && maxValue != DBNull.Value)
                {
                    if (maxValue is DateTimeOffset dto)
                        return dto;
                    
                    if (maxValue is DateTime dt)
                        return new DateTimeOffset(dt);
                    
                    if (DateTimeOffset.TryParse(maxValue.ToString(), out var parsedDto))
                        return parsedDto;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting last sync time, using default");
        }
        
        // If no previous timestamp found, use a default
        return DateTimeOffset.UtcNow.AddYears(-100); // Far in the past to get all records
    }
}
