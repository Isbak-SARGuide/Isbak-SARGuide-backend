namespace AramaKurtarma.Business.Common;

public class Result
{
    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error? Error { get; }

    protected Result(bool isSuccess, Error? error)
    {
        if (isSuccess && error is not null)
        {
            throw new InvalidOperationException("Basarili bir Result hata tasiyamaz.");
        }

        if (!isSuccess && error is null)
        {
            throw new InvalidOperationException("Basarisiz bir Result hatasiz olamaz.");
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new(true, null);

    public static Result Failure(Error error) => new(false, error);

    public static Result<T> Success<T>(T value) => new(value, true, null);

    public static Result<T> Failure<T>(Error error) => new(default, false, error);
}

public sealed class Result<T> : Result
{
    private readonly T? _value;

    internal Result(T? value, bool isSuccess, Error? error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    /// <summary>
    /// Basarisiz bir Result'tan Value okumak programlama hatasidir - bu yuzden
    /// exception firlatiyoruz. Cagiran taraf IsSuccess'i once kontrol etmeli.
    /// </summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Basarisiz Result'tan deger okunamaz. Once IsSuccess kontrol edin.");
}
