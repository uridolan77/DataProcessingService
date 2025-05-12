using MediatR;

namespace DataProcessingService.Core.CQRS.Commands;

public interface ICommand<out TResult> : IRequest<TResult> { }
