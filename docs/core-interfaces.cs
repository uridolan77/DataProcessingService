# Core Interfaces and Service Abstractions

## src/DataProcessingService.Core/Interfaces/Repositories/IRepository.cs
```csharp
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.Core.Domain.Entities.Base;

namespace DataProcessingService.Core.Interfaces.Repositories;

public interface IRepository<TEntity> where TEntity : Entity
{
    Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<TEntity>> FindAsync(
        Expression<Func<TEntity, bool>> predicate, 
        CancellationToken cancellationToken = default);
    
    Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default);
    
    Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);
    
    Task DeleteAsync(TEntity entity, CancellationToken cancellationToken = default);
    
    Task DeleteByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    Task<int> CountAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default);
    
    Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
}

## src/DataProcessingService.Core/Interfaces/Repositories/IDataSourceRepository.cs
```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.Core.Domain.Entities;
using DataProcessingService.Core.Domain.Enums;

namespace DataProcessingService.Core.Interfaces.Repositories;

public interface IDataSourceRepository : IRepository<DataSource>
{
    Task<IReadOnlyList<DataSource>> GetActiveDataSourcesAsync(CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<DataSource>> GetDataSourcesByTypeAsync(
        DataSourceType type, 
        CancellationToken cancellationToken = default);
    
    Task<DataSource?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
}

## src/DataProcessingService.Core/Interfaces/Repositories/IDataPipelineRepository.cs
```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.Core.Domain.Entities;
using DataProcessingService.Core.Domain.Enums;

namespace DataProcessingService.Core.Interfaces.Repositories;

public interface IDataPipelineRepository : IRepository<DataPipeline>
{
    Task<IReadOnlyList<DataPipeline>> GetPipelinesByStatusAsync(
        PipelineStatus status, 
        CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<DataPipeline>> GetPipelinesByTypeAsync(
        PipelineType type, 
        CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<DataPipeline>> GetPipelinesDueForExecutionAsync(
        DateTimeOffset currentTime, 
        CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<DataPipeline>> GetPipelinesBySourceIdAsync(
        Guid sourceId, 
        CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<DataPipeline>> GetPipelinesByDestinationIdAsync(
        Guid destinationId, 
        CancellationToken cancellationToken = default);
    
    Task<DataPipeline?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    
    Task<DataPipeline?> GetWithRelatedDataAsync(Guid id, CancellationToken cancellationToken = default);
}

## src/DataProcessingService.Core/Interfaces/Repositories/IPipelineExecutionRepository.cs
```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.Core.Domain.Entities;
using DataProcessingService.Core.Domain.Enums;

namespace DataProcessingService.Core.Interfaces.Repositories;

public interface IPipelineExecutionRepository : IRepository<PipelineExecution>
{
    Task<IReadOnlyList<PipelineExecution>> GetExecutionsByPipelineIdAsync(
        Guid pipelineId, 
        CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<PipelineExecution>> GetExecutionsByStatusAsync(
        ExecutionStatus status, 
        CancellationToken cancellationToken = default);
    
    Task<PipelineExecution?> GetLatestExecutionByPipelineIdAsync(
        Guid pipelineId, 
        CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<PipelineExecution>> GetExecutionsInDateRangeAsync(
        DateTimeOffset start, 
        DateTimeOffset end, 
        CancellationToken cancellationToken = default);
}

## src/DataProcessingService.Core/Interfaces/Services/IDataSourceService.cs
```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.Core.Domain.Entities;
using DataProcessingService.Core.Domain.Enums;

namespace DataProcessingService.Core.Interfaces.Services;

public interface IDataSourceService
{
    Task<DataSource?> GetDataSourceByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    Task<DataSource?> GetDataSourceByNameAsync(string name, CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<DataSource>> GetAllDataSourcesAsync(CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<DataSource>> GetActiveDataSourcesAsync(CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<DataSource>> GetDataSourcesByTypeAsync(
        DataSourceType type, 
        CancellationToken cancellationToken = default);
    
    Task<DataSource> CreateDataSourceAsync(
        string name,
        string connectionString,
        DataSourceType type,
        string? schema = null,
        Dictionary<string, string>? properties = null,
        CancellationToken cancellationToken = default);
    
    Task UpdateDataSourceAsync(
        Guid id,
        string? connectionString = null,
        string? schema = null,
        bool? isActive = null,
        Dictionary<string, string>? propertiesToAdd = null,
        IEnumerable<string>? propertiesToRemove = null,
        CancellationToken cancellationToken = default);
    
    Task DeleteDataSourceAsync(Guid id, CancellationToken cancellationToken = default);
    
    Task<bool> TestConnectionAsync(Guid id, CancellationToken cancellationToken = default);
}

## src/DataProcessingService.Core/Interfaces/Services/IDataPipelineService.cs
```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.Core.Domain.Entities;
using DataProcessingService.Core.Domain.Enums;
using DataProcessingService.Core.Domain.ValueObjects;

namespace DataProcessingService.Core.Interfaces.Services;

public interface IDataPipelineService
{
    Task<DataPipeline?> GetPipelineByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    Task<DataPipeline?> GetPipelineByNameAsync(string name, CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<DataPipeline>> GetAllPipelinesAsync(CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<DataPipeline>> GetPipelinesByStatusAsync(
        PipelineStatus status, 
        CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<DataPipeline>> GetPipelinesByTypeAsync(
        PipelineType type, 
        CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<DataPipeline>> GetPipelinesBySourceIdAsync(
        Guid sourceId, 
        CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<DataPipeline>> GetPipelinesDueForExecutionAsync(
        DateTimeOffset currentTime, 
        CancellationToken cancellationToken = default);
    
    Task<DataPipeline> CreatePipelineAsync(
        string name,
        string description,
        PipelineType type,
        Guid sourceId,
        ExecutionSchedule schedule,
        TransformationRules transformationRules,
        Guid? destinationId = null,
        CancellationToken cancellationToken = default);
    
    Task UpdatePipelineAsync(
        Guid id,
        string? description = null,
        ExecutionSchedule? schedule = null,
        TransformationRules? transformationRules = null,
        CancellationToken cancellationToken = default);
    
    Task DeletePipelineAsync(Guid id, CancellationToken cancellationToken = default);
    
    Task<PipelineExecution> ExecutePipelineAsync(Guid id, CancellationToken cancellationToken = default);
    
    Task PausePipelineAsync(Guid id, CancellationToken cancellationToken = default);
    
    Task ResumePipelineAsync(Guid id, CancellationToken cancellationToken = default);
}

## src/DataProcessingService.Core/Interfaces/Services/IPipelineExecutionService.cs
```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.Core.Domain.Entities;
using DataProcessingService.Core.Domain.Enums;

namespace DataProcessingService.Core.Interfaces.Services;

public interface IPipelineExecutionService
{
    Task<PipelineExecution?> GetExecutionByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<PipelineExecution>> GetExecutionsByPipelineIdAsync(
        Guid pipelineId, 
        CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<PipelineExecution>> GetExecutionsByStatusAsync(
        ExecutionStatus status, 
        CancellationToken cancellationToken = default);
    
    Task<PipelineExecution?> GetLatestExecutionByPipelineIdAsync(
        Guid pipelineId, 
        CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<PipelineExecution>> GetExecutionsInDateRangeAsync(
        DateTimeOffset start, 
        DateTimeOffset end, 
        CancellationToken cancellationToken = default);
    
    Task<Dictionary<string, int>> GetExecutionStatisticsAsync(
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken = default);
}

## src/DataProcessingService.Core/Interfaces/Services/ETL/IDataExtractionService.cs
```csharp
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.Core.Domain.Entities;

namespace DataProcessingService.Core.Interfaces.Services.ETL;

public interface IDataExtractionService
{
    Task<IEnumerable<ExpandoObject>> ExtractDataAsync(
        DataSource source,
        string query,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default);
    
    Task<IAsyncEnumerable<ExpandoObject>> ExtractDataStreamAsync(
        DataSource source,
        string query,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default);
    
    Task<IEnumerable<string>> GetAvailableTablesAsync(
        DataSource source,
        CancellationToken cancellationToken = default);
    
    Task<IEnumerable<(string Name, string Type)>> GetTableSchemaAsync(
        DataSource source,
        string tableName,
        CancellationToken cancellationToken = default);
}

## src/DataProcessingService.Core/Interfaces/Services/ETL/IDataTransformationService.cs
```csharp
using System.Collections.Generic;
using System.Dynamic;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.Core.Domain.ValueObjects;

namespace DataProcessingService.Core.Interfaces.Services.ETL;

public interface IDataTransformationService
{
    Task<IEnumerable<ExpandoObject>> TransformDataAsync(
        IEnumerable<ExpandoObject> data,
        TransformationRules rules,
        CancellationToken cancellationToken = default);
    
    Task<IAsyncEnumerable<ExpandoObject>> TransformDataStreamAsync(
        IAsyncEnumerable<ExpandoObject> dataStream,
        TransformationRules rules,
        CancellationToken cancellationToken = default);
    
    Task<ExpandoObject> TransformRecordAsync(
        ExpandoObject record,
        TransformationRules rules,
        CancellationToken cancellationToken = default);
    
    IReadOnlyList<string> ValidateRules(TransformationRules rules);
}

## src/DataProcessingService.Core/Interfaces/Services/ETL/IDataLoadService.cs
```csharp
using System.Collections.Generic;
using System.Dynamic;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.Core.Domain.Entities;

namespace DataProcessingService.Core.Interfaces.Services.ETL;

public interface IDataLoadService
{
    Task<int> LoadDataAsync(
        DataSource destination,
        string tableName,
        IEnumerable<ExpandoObject> data,
        bool createIfNotExists = false,
        bool truncateBeforeLoad = false,
        CancellationToken cancellationToken = default);
    
    Task<int> LoadDataStreamAsync(
        DataSource destination,
        string tableName,
        IAsyncEnumerable<ExpandoObject> dataStream,
        bool createIfNotExists = false,
        bool truncateBeforeLoad = false,
        CancellationToken cancellationToken = default);
    
    Task<bool> TableExistsAsync(
        DataSource destination,
        string tableName,
        CancellationToken cancellationToken = default);
    
    Task<bool> CreateTableAsync(
        DataSource destination,
        string tableName,
        IEnumerable<(string Name, string Type)> columns,
        CancellationToken cancellationToken = default);
}

## src/DataProcessingService.Core/Interfaces/Services/ETL/IDataConsistencyService.cs
```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.Core.Domain.Entities;

namespace DataProcessingService.Core.Interfaces.Services.ETL;

public interface IDataConsistencyService
{
    Task<bool> ValidateDataConsistencyAsync(
        DataSource source,
        DataSource destination,
        string sourceTable,
        string destinationTable,
        IEnumerable<string> keyColumns,
        CancellationToken cancellationToken = default);
    
    Task<(int MissingRecords, int DifferentRecords, int TotalRecords)> GetConsistencyMetricsAsync(
        DataSource source,
        DataSource destination,
        string sourceTable,
        string destinationTable,
        IEnumerable<string> keyColumns,
        IEnumerable<string>? compareColumns = null,
        CancellationToken cancellationToken = default);
    
    Task<int> SynchronizeDataAsync(
        DataSource source,
        DataSource destination,
        string sourceTable,
        string destinationTable,
        IEnumerable<string> keyColumns,
        IEnumerable<string>? syncColumns = null,
        CancellationToken cancellationToken = default);
    
    Task<(string Hash, DateTimeOffset Timestamp)> ComputeTableChecksumAsync(
        DataSource source,
        string tableName,
        IEnumerable<string>? columns = null,
        CancellationToken cancellationToken = default);
}

## src/DataProcessingService.Core/Interfaces/Services/ETL/IDataReplicationService.cs
```csharp
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.Core.Domain.Entities;

namespace DataProcessingService.Core.Interfaces.Services.ETL;

public interface IDataReplicationService
{
    Task<int> ReplicateTableAsync(
        DataSource source,
        DataSource destination,
        string sourceTable,
        string? destinationTable = null,
        bool createIfNotExists = false,
        IEnumerable<string>? keyColumns = null,
        ReplicationMode mode = ReplicationMode.Full,
        CancellationToken cancellationToken = default);
    
    Task<IDictionary<string, int>> ReplicateAllTablesAsync(
        DataSource source,
        DataSource destination,
        bool createIfNotExists = false,
        IEnumerable<string>? excludedTables = null,
        ReplicationMode mode = ReplicationMode.Full,
        CancellationToken cancellationToken = default);
    
    Task<int> ReplicateIncrementalAsync(
        DataSource source,
        DataSource destination,
        string sourceTable,
        string? destinationTable = null,
        string changeTrackingColumn = "LastModified",
        CancellationToken cancellationToken = default);
    
    Task ConfigureChangeDataCaptureAsync(
        DataSource source,
        string tableName,
        IEnumerable<string>? columns = null,
        CancellationToken cancellationToken = default);
}

public enum ReplicationMode
{
    Full,
    Incremental,
    ChangeDataCapture
}

## src/DataProcessingService.Core/Interfaces/Messaging/IMessagePublisher.cs
```csharp
using System.Threading;
using System.Threading.Tasks;

namespace DataProcessingService.Core.Interfaces.Messaging;

public interface IMessagePublisher
{
    Task PublishAsync<TMessage>(
        string topicName,
        TMessage message,
        CancellationToken cancellationToken = default) 
        where TMessage : class;
    
    Task PublishAsync<TMessage>(
        string topicName,
        TMessage message,
        string? partitionKey = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default) 
        where TMessage : class;
}

## src/DataProcessingService.Core/Interfaces/Messaging/IMessageConsumer.cs
```csharp
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DataProcessingService.Core.Interfaces.Messaging;

public interface IMessageConsumer
{
    Task SubscribeAsync<TMessage>(
        string topicName,
        string subscriptionName,
        Func<TMessage, IDictionary<string, string>, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default) 
        where TMessage : class;
    
    Task UnsubscribeAsync(
        string topicName,
        string subscriptionName,
        CancellationToken cancellationToken = default);
}

## src/DataProcessingService.Core/Interfaces/Messaging/IEventBus.cs
```csharp
using System.Threading;
using System.Threading.Tasks;
using DataProcessingService.Core.Domain.Events;

namespace DataProcessingService.Core.Interfaces.Messaging;

public interface IEventBus
{
    Task PublishDomainEventAsync<TEvent>(
        TEvent domainEvent,
        CancellationToken cancellationToken = default) 
        where TEvent : DomainEvent;
    
    Task PublishIntegrationEventAsync<TEvent>(
        TEvent integrationEvent,
        CancellationToken cancellationToken = default) 
        where TEvent : class;
}

## src/DataProcessingService.Core/CQRS/Commands/ICommandHandler.cs
```csharp
using System.Threading;
using System.Threading.Tasks;

namespace DataProcessingService.Core.CQRS.Commands;

public interface ICommandHandler<in TCommand, TResult> where TCommand : ICommand<TResult>
{
    Task<TResult> Handle(TCommand command, CancellationToken cancellationToken);
}

## src/DataProcessingService.Core/CQRS/Commands/ICommand.cs
```csharp
using MediatR;

namespace DataProcessingService.Core.CQRS.Commands;

public interface ICommand<out TResult> : IRequest<TResult> { }

## src/DataProcessingService.Core/CQRS/Queries/IQueryHandler.cs
```csharp
using System.Threading;
using System.Threading.Tasks;

namespace DataProcessingService.Core.CQRS.Queries;

public interface IQueryHandler<in TQuery, TResult> where TQuery : IQuery<TResult>
{
    Task<TResult> Handle(TQuery query, CancellationToken cancellationToken);
}

## src/DataProcessingService.Core/CQRS/Queries/IQuery.cs
```csharp
using MediatR;

namespace DataProcessingService.Core.CQRS.Queries;

public interface IQuery<out TResult> : IRequest<TResult> { }
