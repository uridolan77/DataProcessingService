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
