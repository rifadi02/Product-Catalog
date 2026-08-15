namespace Catalog.Domain.Common;

public enum ErrorKind { None, Validation, NotFound, Conflict, Unauthorized, Forbidden }

public sealed record Error(
    ErrorKind Kind,
    string Code,
    string Message,
    IReadOnlyDictionary<string, string[]>? Fields = null)
{
    public static Error Validation(string code, string msg,
        IReadOnlyDictionary<string, string[]>? fields = null) => new(ErrorKind.Validation, code, msg, fields);

    public static Error NotFound(string code, string msg) => new(ErrorKind.NotFound, code, msg);
    public static Error Conflict(string code, string msg) => new(ErrorKind.Conflict, code, msg);
    public static Error Unauthorized(string code, string msg) => new(ErrorKind.Unauthorized, code, msg);
    public static Error Forbidden(string code, string msg) => new(ErrorKind.Forbidden, code, msg);
}

public readonly record struct Result<T>
{
    public T? Value { get; }
    public Error? Error { get; }
    public bool IsSuccess { get; }

    private Result(T value) { Value = value; Error = null; IsSuccess = true; }
    private Result(Error err) { Value = default; Error = err; IsSuccess = false; }

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(Error error) => new(error);
    public static Result<T> Invalid(string message) => new(Error.Validation("validation_failed", message));

    /// <summary>
    /// Field-scoped validation failure. The API layer copies <see cref="Common.Error.Fields"/>
    /// straight into the RFC 7807 <c>errors</c> object (§3.0.1), so a rule such as
    /// "minPrice must be &lt;= maxPrice" lands on the field the user can actually fix.
    /// </summary>
    public static Result<T> Invalid(string field, string message) =>
        new(Error.Validation("validation_failed", message,
            new Dictionary<string, string[]> { [field] = [message] }));
}

/// <summary>Non-generic companion for operations that succeed with no payload (logout, delete).</summary>
public readonly record struct Result
{
    public Error? Error { get; }
    public bool IsSuccess { get; }

    private Result(bool success, Error? error) { IsSuccess = success; Error = error; }

    public static Result Success() => new(true, null);
    public static Result Failure(Error error) => new(false, error);
}
