namespace ACI.Application.Common;

/// <summary>
/// Represents the result of an operation that can succeed or fail.
/// Provides a standardized way to handle errors without exceptions.
/// </summary>
public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None)
            throw new ArgumentException("Success result cannot have an error", nameof(error));
        
        if (!isSuccess && error == Error.None)
            throw new ArgumentException("Failure result must have an error", nameof(error));

        IsSuccess = isSuccess;
        Error = error;
    }

    /// <summary>
    /// Gets a value indicating whether the operation was successful.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets a value indicating whether the operation failed.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Gets the error if the operation failed, or Error.None if successful.
    /// </summary>
    public Error Error { get; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static Result Success() => new(true, Error.None);

    /// <summary>
    /// Creates a failed result with the specified error.
    /// </summary>
    public static Result Failure(Error error) => new(false, error);

    /// <summary>
    /// Creates a successful result with a value.
    /// </summary>
    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, Error.None);

    /// <summary>
    /// Creates a failed result with the specified error.
    /// </summary>
    public static Result<TValue> Failure<TValue>(Error error) => new(default, false, error);

    /// <summary>
    /// Creates a result based on a condition.
    /// </summary>
    public static Result Create(bool condition, Error error) =>
        condition ? Success() : Failure(error);

    /// <summary>
    /// Creates a result with value based on whether the value is null.
    /// </summary>
    public static Result<TValue> Create<TValue>(TValue? value, Error error) where TValue : class =>
        value is not null ? Success(value) : Failure<TValue>(error);

    /// <summary>
    /// Implicitly converts an error to a failed result.
    /// </summary>
    public static implicit operator Result(Error error) => Failure(error);
}

/// <summary>
/// Represents the result of an operation that returns a value.
/// </summary>
/// <typeparam name="TValue">The type of the value returned on success.</typeparam>
public class Result<TValue> : Result
{
    private readonly TValue? _value;

    protected internal Result(TValue? value, bool isSuccess, Error error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    /// <summary>
    /// Gets the value if the operation was successful.
    /// Throws InvalidOperationException if accessed on a failed result.
    /// </summary>
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access value of a failed result. Check IsSuccess before accessing Value.");

    /// <summary>
    /// Gets the value or a default if the operation failed.
    /// </summary>
    public TValue? GetValueOrDefault() => IsSuccess ? _value : default;

    /// <summary>
    /// Gets the value or the specified default if the operation failed.
    /// </summary>
    public TValue GetValueOrDefault(TValue defaultValue) => IsSuccess ? _value! : defaultValue;

    /// <summary>
    /// Implicitly converts a value to a successful result.
    /// </summary>
    public static implicit operator Result<TValue>(TValue? value) =>
        value is not null ? Success(value) : Failure<TValue>(Error.NullValue);

    /// <summary>
    /// Implicitly converts an error to a failed result.
    /// </summary>
    public static implicit operator Result<TValue>(Error error) => Failure<TValue>(error);

    /// <summary>
    /// Maps the value to a new type if the result is successful.
    /// </summary>
    public Result<TNew> Map<TNew>(Func<TValue, TNew> mapper) =>
        IsSuccess ? Result.Success(mapper(_value!)) : Result.Failure<TNew>(Error);

    /// <summary>
    /// Executes an action if the result is successful.
    /// </summary>
    public Result<TValue> OnSuccess(Action<TValue> action)
    {
        if (IsSuccess)
            action(_value!);
        return this;
    }

    /// <summary>
    /// Executes an action if the result failed.
    /// </summary>
    public Result<TValue> OnFailure(Action<Error> action)
    {
        if (IsFailure)
            action(Error);
        return this;
    }
}

/// <summary>
/// Represents an error with a code and description.
/// </summary>
/// <param name="Code">The unique error code.</param>
/// <param name="Description">A human-readable description of the error.</param>
public record Error(string Code, string Description)
{
    /// <summary>
    /// Represents no error (for successful operations).
    /// </summary>
    public static readonly Error None = new(string.Empty, string.Empty);

    /// <summary>
    /// Represents a null value error.
    /// </summary>
    public static readonly Error NullValue = new("Error.NullValue", "A null value was provided");

    /// <summary>
    /// Represents a generic not found error.
    /// </summary>
    public static readonly Error NotFound = new("Error.NotFound", "The requested resource was not found");

    /// <summary>
    /// Represents a generic unauthorized error.
    /// </summary>
    public static readonly Error Unauthorized = new("Error.Unauthorized", "You are not authorized to perform this action");

    /// <summary>
    /// Represents a generic conflict error.
    /// </summary>
    public static readonly Error Conflict = new("Error.Conflict", "A conflict occurred with the current state");

    /// <summary>
    /// Creates an error from an exception.
    /// </summary>
    public static Error FromException(Exception ex) => new("Error.Exception", ex.Message);
}
