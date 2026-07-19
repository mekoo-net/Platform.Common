using System.Diagnostics.CodeAnalysis;

namespace Platform.Common.Results;

/// <summary>
/// 平台统一返回模型。语义：成功携带值、失败携带错误，禁止两者并存。
/// </summary>
public readonly struct Result<T>
{
    private readonly T? _value;
    private readonly Error? _error;

    private Result(T value)
    {
        _value = value;
        _error = null;
        IsSuccess = true;
    }

    private Result(Error error)
    {
        _value = default;
        _error = error;
        IsSuccess = false;
    }

    [MemberNotNullWhen(true, nameof(_value))]
    [MemberNotNullWhen(false, nameof(_error))]
    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public T Value
        => IsSuccess ? _value! : throw new InvalidOperationException("Result is in failure state.");

    public Error Error
        => IsSuccess ? throw new InvalidOperationException("Result is in success state.") : _error!;

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(Error error) => new(error);

    public static implicit operator Result<T>(T value) => new(value);
    public static implicit operator Result<T>(Error error) => new(error);

    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<Error, TOut> onFailure)
        => IsSuccess ? onSuccess(_value!) : onFailure(_error!);

    public Result<TOut> Map<TOut>(Func<T, TOut> mapper)
        => IsSuccess ? Result<TOut>.Success(mapper(_value!)) : Result<TOut>.Failure(_error!);

    public Result<TOut> Bind<TOut>(Func<T, Result<TOut>> binder)
        => IsSuccess ? binder(_value!) : Result<TOut>.Failure(_error!);
}

/// <summary>非泛型 Result，仅承载成功/失败语义（无返回值的命令）。</summary>
public readonly struct Result
{
    private readonly Error? _error;

    private Result(bool isSuccess, Error? error)
    {
        IsSuccess = isSuccess;
        _error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;

    public Error Error
        => IsSuccess ? throw new InvalidOperationException("Result is in success state.") : _error!;

    public static Result Success() => new(true, null);
    public static Result Failure(Error error) => new(false, error);

    public static implicit operator Result(Error error) => new(false, error);
}
