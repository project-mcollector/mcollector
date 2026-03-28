using Shared;

namespace Contracts;

public interface ICommand<out TResponse> { }


public interface ICommand : ICommand<Result> { }

