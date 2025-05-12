using MediatR;

namespace DataProcessingService.Core.CQRS.Queries;

public interface IQuery<out TResult> : IRequest<TResult> { }
