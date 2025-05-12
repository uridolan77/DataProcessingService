using System.Threading;
using System.Threading.Tasks;

namespace DataProcessingService.Core.CQRS.Queries;

public interface IQueryHandler<in TQuery, TResult> where TQuery : IQuery<TResult>
{
    Task<TResult> Handle(TQuery query, CancellationToken cancellationToken);
}
