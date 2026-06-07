namespace SupportPilot.Application.Common;

/// <summary>
/// Standard application-layer error category used by use cases before the API maps the result to HTTP.
/// </summary>
public enum ApplicationError
{
    /// <summary>No error occurred.</summary>
    None = 0,

    /// <summary>The request is syntactically valid but violates business validation rules.</summary>
    Validation = 1,

    /// <summary>The requested resource does not exist.</summary>
    NotFound = 2,

    /// <summary>The current user is authenticated but not allowed to perform the operation.</summary>
    Forbidden = 3,

    /// <summary>The operation conflicts with existing state, for example a duplicate email or category.</summary>
    Conflict = 4,

    /// <summary>The user is not authenticated or supplied invalid credentials.</summary>
    Unauthorized = 5
}

/// <summary>
/// Generic use case result containing either a value or a normalized application error.
/// </summary>
/// <typeparam name="T">Type of the successful result payload.</typeparam>
/// <param name="Value">Successful result payload, or default when the operation failed.</param>
/// <param name="Error">Application error category.</param>
/// <param name="Message">Optional human-readable error message.</param>
public sealed record ApplicationResult<T>(T? Value, ApplicationError Error, string? Message)
{
    /// <summary>Gets whether the result represents a successful operation.</summary>
    public bool IsSuccess => Error == ApplicationError.None;

    /// <summary>Creates a successful result.</summary>
    /// <param name="value">Result payload.</param>
    public static ApplicationResult<T> Success(T value) => new(value, ApplicationError.None, null);

    /// <summary>Creates a failed result.</summary>
    /// <param name="error">Application error category.</param>
    /// <param name="message">Human-readable error message.</param>
    public static ApplicationResult<T> Failure(ApplicationError error, string message) => new(default, error, message);
}

/// <summary>
/// Use case result for commands that do not return a payload.
/// </summary>
/// <param name="Error">Application error category.</param>
/// <param name="Message">Optional human-readable error message.</param>
public sealed record ApplicationResult(ApplicationError Error, string? Message)
{
    /// <summary>Gets whether the result represents a successful operation.</summary>
    public bool IsSuccess => Error == ApplicationError.None;

    /// <summary>Creates a successful empty result.</summary>
    public static ApplicationResult Success() => new(ApplicationError.None, null);

    /// <summary>Creates a failed empty result.</summary>
    /// <param name="error">Application error category.</param>
    /// <param name="message">Human-readable error message.</param>
    public static ApplicationResult Failure(ApplicationError error, string message) => new(error, message);
}
