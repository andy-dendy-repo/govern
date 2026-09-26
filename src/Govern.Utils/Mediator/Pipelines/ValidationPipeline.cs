using FluentValidation;
using Govern.Utils.Mediator.Abstractions;
using Govern.Utils.Results;

namespace Govern.Utils.Mediator.Pipelines;

/// <summary>
/// Runs every FluentValidation <see cref="IValidator{T}"/> registered for the request and short-circuits with
/// <see cref="Error.Validation"/> (field errors keyed by property name) when any rule fails.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public sealed class ValidationPipeline<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipeline<TRequest, TResponse>
    where TRequest : IRequest<TRequest, TResponse>
    where TResponse : IResponse
{
    private readonly IValidator<TRequest>[] _validators = [.. validators];

    /// <inheritdoc />
    public async ValueTask<Result<TResponse>> HandleAsync(
        TRequest request,
        Func<ValueTask<Result<TResponse>>> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        if (_validators.Length == 0)
        {
            return await next();
        }

        var failures = new List<FluentValidation.Results.ValidationFailure>();
        foreach (var validator in _validators)
        {
            var validation = await validator.ValidateAsync(request, cancellationToken);
            failures.AddRange(validation.Errors);
        }

        if (failures.Count == 0)
        {
            return await next();
        }

        var errors = failures
            .GroupBy(static failure => failure.PropertyName, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => group.Select(static failure => failure.ErrorMessage).Distinct(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);

        return Error.Validation(errors);
    }
}
