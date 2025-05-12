using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.Core.Domain.DataQuality;

namespace DataProcessingService.Core.Interfaces.Services.DataQuality;

public interface ISchemaValidationService
{
    Task<DataSchema?> GetSchemaByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    Task<DataSchema?> GetSchemaByNameAsync(string name, string version, CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<DataSchema>> GetAllSchemasAsync(CancellationToken cancellationToken = default);
    
    Task<DataSchema> CreateSchemaAsync(
        string name,
        string description,
        string version,
        List<SchemaField> fields,
        CancellationToken cancellationToken = default);
    
    Task UpdateSchemaAsync(
        Guid id,
        string description,
        string version,
        List<SchemaField> fields,
        CancellationToken cancellationToken = default);
    
    Task DeleteSchemaAsync(Guid id, CancellationToken cancellationToken = default);
    
    Task<List<SchemaValidationError>> ValidateDataAsync(
        Guid schemaId,
        IDictionary<string, object?> data,
        CancellationToken cancellationToken = default);
    
    Task<List<SchemaValidationError>> ValidateDataAsync(
        Guid schemaId,
        ExpandoObject data,
        CancellationToken cancellationToken = default);
    
    Task<List<SchemaValidationError>> ValidateDataAsync(
        DataSchema schema,
        IDictionary<string, object?> data,
        CancellationToken cancellationToken = default);
    
    Task<List<SchemaValidationError>> ValidateDataBatchAsync(
        Guid schemaId,
        IEnumerable<IDictionary<string, object?>> dataBatch,
        CancellationToken cancellationToken = default);
    
    Task<SchemaField> InferSchemaFieldFromSampleAsync(
        string fieldName,
        IEnumerable<object?> samples,
        CancellationToken cancellationToken = default);
    
    Task<List<SchemaField>> InferSchemaFromSampleAsync(
        IDictionary<string, object?> sample,
        CancellationToken cancellationToken = default);
}
