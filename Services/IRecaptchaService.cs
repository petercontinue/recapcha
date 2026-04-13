namespace recapcha.Services;

public interface IRecaptchaService
{
    Task<RecaptchaVerificationResult> VerifyAsync(
        string token,
        string? remoteIp,
        string expectedAction,
        CancellationToken cancellationToken = default);
}

public sealed class RecaptchaVerificationResult
{
    public bool Success { get; init; }

    public double Score { get; init; }

    public string? Action { get; init; }

    public IReadOnlyList<string> ErrorCodes { get; init; } = Array.Empty<string>();
}
