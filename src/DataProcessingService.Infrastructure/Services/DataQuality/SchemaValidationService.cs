using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using DataProcessingService.Core.Domain.DataQuality;
using DataProcessingService.Core.Interfaces.Repositories;
using DataProcessingService.Core.Interfaces.Services.DataQuality;

namespace DataProcessingService.Infrastructure.Services.DataQuality;

public class SchemaValidationService : ISchemaValidationService
{
    private readonly IRepository<DataSchema> _schemaRepository;
    private readonly ILogger<SchemaValidationService> _logger;
    
    public SchemaValidationService(
        IRepository<DataSchema> schemaRepository,
        ILogger<SchemaValidationService> logger)
    {
        _schemaRepository = schemaRepository;
        _logger = logger;
    }
    
    public async Task<DataSchema?> GetSchemaByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _schemaRepository.GetByIdAsync(id, cancellationToken);
    }
    
    public async Task<DataSchema?> GetSchemaByNameAsync(string name, string version, CancellationToken cancellationToken = default)
    {
        var schemas = await _schemaRepository.FindAsync(
            s => s.Name == name && s.Version == version, 
            cancellationToken);
        
        return schemas.FirstOrDefault();
    }
    
    public async Task<IReadOnlyList<DataSchema>> GetAllSchemasAsync(CancellationToken cancellationToken = default)
    {
        return await _schemaRepository.GetAllAsync(cancellationToken);
    }
    
    public async Task<DataSchema> CreateSchemaAsync(
        string name,
        string description,
        string version,
        List<SchemaField> fields,
        CancellationToken cancellationToken = default)
    {
        var existingSchema = await GetSchemaByNameAsync(name, version, cancellationToken);
        if (existingSchema != null)
        {
            throw new InvalidOperationException($"Schema with name '{name}' and version '{version}' already exists");
        }
        
        var schema = new DataSchema(name, description, version, fields);
        
        await _schemaRepository.AddAsync(schema, cancellationToken);
        
        _logger.LogInformation("Created schema {SchemaId} with name {SchemaName} and version {SchemaVersion}", 
            schema.Id, schema.Name, schema.Version);
        
        return schema;
    }
    
    public async Task UpdateSchemaAsync(
        Guid id,
        string description,
        string version,
        List<SchemaField> fields,
        CancellationToken cancellationToken = default)
    {
        var schema = await _schemaRepository.GetByIdAsync(id, cancellationToken);
        if (schema == null)
        {
            throw new KeyNotFoundException($"Schema with ID {id} not found");
        }
        
        schema.Update(description, version, fields);
        
        await _schemaRepository.UpdateAsync(schema, cancellationToken);
        
        _logger.LogInformation("Updated schema {SchemaId}", schema.Id);
    }
    
    public async Task DeleteSchemaAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var schema = await _schemaRepository.GetByIdAsync(id, cancellationToken);
        if (schema == null)
        {
            throw new KeyNotFoundException($"Schema with ID {id} not found");
        }
        
        await _schemaRepository.DeleteAsync(schema, cancellationToken);
        
        _logger.LogInformation("Deleted schema {SchemaId}", id);
    }
    
    public async Task<List<SchemaValidationError>> ValidateDataAsync(
        Guid schemaId,
        IDictionary<string, object?> data,
        CancellationToken cancellationToken = default)
    {
        var schema = await _schemaRepository.GetByIdAsync(schemaId, cancellationToken);
        if (schema == null)
        {
            throw new KeyNotFoundException($"Schema with ID {schemaId} not found");
        }
        
        return await ValidateDataAsync(schema, data, cancellationToken);
    }
    
    public async Task<List<SchemaValidationError>> ValidateDataAsync(
        Guid schemaId,
        ExpandoObject data,
        CancellationToken cancellationToken = default)
    {
        return await ValidateDataAsync(schemaId, data as IDictionary<string, object?>, cancellationToken);
    }
    
    public async Task<List<SchemaValidationError>> ValidateDataAsync(
        DataSchema schema,
        IDictionary<string, object?> data,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Validating data against schema {SchemaId}", schema.Id);
        
        var errors = schema.Validate(data);
        
        if (errors.Any())
        {
            _logger.LogWarning("Validation failed with {ErrorCount} errors for schema {SchemaId}", 
                errors.Count, schema.Id);
        }
        
        return await Task.FromResult(errors);
    }
    
    public async Task<List<SchemaValidationError>> ValidateDataBatchAsync(
        Guid schemaId,
        IEnumerable<IDictionary<string, object?>> dataBatch,
        CancellationToken cancellationToken = default)
    {
        var schema = await _schemaRepository.GetByIdAsync(schemaId, cancellationToken);
        if (schema == null)
        {
            throw new KeyNotFoundException($"Schema with ID {schemaId} not found");
        }
        
        var allErrors = new List<SchemaValidationError>();
        
        foreach (var data in dataBatch)
        {
            var errors = schema.Validate(data);
            allErrors.AddRange(errors);
            
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
        
        return allErrors;
    }
    
    public async Task<SchemaField> InferSchemaFieldFromSampleAsync(
        string fieldName,
        IEnumerable<object?> samples,
        CancellationToken cancellationToken = default)
    {
        var field = new SchemaField
        {
            Name = fieldName,
            Description = $"Auto-inferred field for {fieldName}",
            IsRequired = true,
            AllowUnknownFields = false
        };
        
        // Count occurrences of each type
        var typeCount = new Dictionary<SchemaFieldType, int>();
        var nonNullCount = 0;
        var totalCount = 0;
        
        foreach (var sample in samples)
        {
            totalCount++;
            
            if (sample == null)
            {
                continue;
            }
            
            nonNullCount++;
            
            var type = InferType(sample);
            if (!typeCount.ContainsKey(type))
            {
                typeCount[type] = 0;
            }
            
            typeCount[type]++;
        }
        
        // Determine if field is required
        field.IsRequired = nonNullCount == totalCount;
        
        // Determine the most common type
        if (typeCount.Any())
        {
            field.Type = typeCount.OrderByDescending(kv => kv.Value).First().Key;
        }
        else
        {
            field.Type = SchemaFieldType.String; // Default to string if no samples
        }
        
        // Infer constraints based on the type
        field.Constraints = InferConstraints(field.Type, samples.Where(s => s != null));
        
        return await Task.FromResult(field);
    }
    
    public async Task<List<SchemaField>> InferSchemaFromSampleAsync(
        IDictionary<string, object?> sample,
        CancellationToken cancellationToken = default)
    {
        var fields = new List<SchemaField>();
        
        foreach (var (key, value) in sample)
        {
            var field = new SchemaField
            {
                Name = key,
                Description = $"Auto-inferred field for {key}",
                IsRequired = value != null,
                AllowUnknownFields = false,
                Type = InferType(value)
            };
            
            // Add basic constraints based on the sample
            if (value != null)
            {
                field.Constraints = InferConstraints(field.Type, new[] { value });
            }
            
            fields.Add(field);
        }
        
        return await Task.FromResult(fields);
    }
    
    private static SchemaFieldType InferType(object? value)
    {
        if (value == null)
            return SchemaFieldType.String;
        
        return value switch
        {
            string => SchemaFieldType.String,
            int or long or short or byte => SchemaFieldType.Integer,
            float or double or decimal => SchemaFieldType.Decimal,
            bool => SchemaFieldType.Boolean,
            DateTime or DateTimeOffset => SchemaFieldType.DateTime,
            System.Collections.IEnumerable and not string => SchemaFieldType.Array,
            IDictionary<string, object> => SchemaFieldType.Object,
            _ => SchemaFieldType.String // Default to string for unknown types
        };
    }
    
    private static Dictionary<string, object> InferConstraints(SchemaFieldType type, IEnumerable<object> samples)
    {
        var constraints = new Dictionary<string, object>();
        
        switch (type)
        {
            case SchemaFieldType.String:
                InferStringConstraints(constraints, samples);
                break;
            case SchemaFieldType.Integer:
                InferNumericConstraints<long>(constraints, samples.Select(s => Convert.ToInt64(s)));
                break;
            case SchemaFieldType.Decimal:
                InferNumericConstraints<decimal>(constraints, samples.Select(s => Convert.ToDecimal(s)));
                break;
            case SchemaFieldType.Array:
                InferArrayConstraints(constraints, samples);
                break;
        }
        
        return constraints;
    }
    
    private static void InferStringConstraints(Dictionary<string, object> constraints, IEnumerable<object> samples)
    {
        var strings = samples.Select(s => s.ToString() ?? string.Empty).ToList();
        
        if (!strings.Any())
            return;
        
        var minLength = strings.Min(s => s.Length);
        var maxLength = strings.Max(s => s.Length);
        
        constraints["minLength"] = minLength;
        constraints["maxLength"] = maxLength;
        
        // Check if all values are from a small set (potential enum)
        var distinctValues = strings.Distinct().ToList();
        if (distinctValues.Count <= 10 && distinctValues.Count < strings.Count / 2)
        {
            constraints["enum"] = distinctValues;
        }
    }
    
    private static void InferNumericConstraints<T>(Dictionary<string, object> constraints, IEnumerable<T> samples) 
        where T : IComparable
    {
        var values = samples.ToList();
        
        if (!values.Any())
            return;
        
        var min = values.Min();
        var max = values.Max();
        
        constraints["minimum"] = min;
        constraints["maximum"] = max;
    }
    
    private static void InferArrayConstraints(Dictionary<string, object> constraints, IEnumerable<object> samples)
    {
        var arrays = samples.Cast<System.Collections.IEnumerable>().ToList();
        
        if (!arrays.Any())
            return;
        
        var minItems = int.MaxValue;
        var maxItems = 0;
        
        foreach (var array in arrays)
        {
            var count = 0;
            foreach (var _ in array)
            {
                count++;
            }
            
            minItems = Math.Min(minItems, count);
            maxItems = Math.Max(maxItems, count);
        }
        
        constraints["minItems"] = minItems;
        constraints["maxItems"] = maxItems;
    }
}
