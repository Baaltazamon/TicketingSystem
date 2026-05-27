namespace SupportPilot.Application.Common;

public enum ApplicationError
{
    None = 0,
    Validation = 1,
    NotFound = 2,
    Forbidden = 3,
    Conflict = 4
}

public sealed record ApplicationResult<T>(T? Value, ApplicationError Error, string? Message)
{
    public bool IsSuccess => Error == ApplicationError.None;

    public static ApplicationResult<T> Success(T value) => new(value, ApplicationError.None, null);

    public static ApplicationResult<T> Failure(ApplicationError error, string message) => new(default, error, message);
}

public sealed record ApplicationResult(ApplicationError Error, string? Message)
{
    public bool IsSuccess => Error == ApplicationError.None;

    public static ApplicationResult Success() => new(ApplicationError.None, null);

    public static ApplicationResult Failure(ApplicationError error, string message) => new(error, message);
}
