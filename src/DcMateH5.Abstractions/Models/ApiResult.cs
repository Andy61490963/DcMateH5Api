namespace DcMateH5Api.Models;

public class Result<T>
{
    public bool IsSuccess { get; }
    public T Data { get; }
    public string Code { get; }
    public string Message { get; }
    public object ErrorData { get; }

    private Result(T data)
    {
        IsSuccess = true;
        Data = data;
        Code = string.Empty;
        Message = string.Empty;
        ErrorData = null!;
    }

    private Result(string code, string message, object errorData = null!)
    {
        IsSuccess = false;
        Data = default!;
        Code = code;
        Message = message;
        ErrorData = errorData;
    }

    public static Result<T> Ok(T data)
        => new(data);

    public static Result<T> Fail(Enum code, string message, object errorData = null!)
        => new(code.ToString(), message, errorData);
}
