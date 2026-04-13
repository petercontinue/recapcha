using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using recapcha.Options;

namespace recapcha.Services;

public sealed class GoogleRecaptchaService : IRecaptchaService
{
    private const string SiteVerifyUrl = "https://www.google.com/recaptcha/api/siteverify";

    private readonly HttpClient _httpClient;
    private readonly RecaptchaOptions _options;

    public GoogleRecaptchaService(HttpClient httpClient, IOptions<RecaptchaOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<RecaptchaVerificationResult> VerifyAsync(
        string token,
        string? remoteIp,
        string expectedAction,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            return new RecaptchaVerificationResult
            {
                Success = false,
                ErrorCodes = new[] { "missing-input-secret" },
            };
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return new RecaptchaVerificationResult
            {
                Success = false,
                ErrorCodes = new[] { "missing-input-response" },
            };
        }

        var pairs = new List<KeyValuePair<string, string>>
        {
            new("secret", _options.SecretKey),
            new("response", token),
        };

        if (!string.IsNullOrEmpty(remoteIp))
            pairs.Add(new KeyValuePair<string, string>("remoteip", remoteIp));

        using var content = new FormUrlEncodedContent(pairs);
        using var response = await _httpClient.PostAsync(SiteVerifyUrl, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new RecaptchaVerificationResult
            {
                Success = false,
                ErrorCodes = new[] { $"http-{(int)response.StatusCode}" },
            };
        }

        var body = await response.Content.ReadFromJsonAsync<SiteVerifyResponseDto>(cancellationToken);

        if (body is null)
        {
            return new RecaptchaVerificationResult { Success = false, ErrorCodes = new[] { "invalid-response" } };
        }

        var score = body.Score ?? 0;
        var actionOk = string.IsNullOrEmpty(body.Action) ||
                       string.Equals(body.Action, expectedAction, StringComparison.Ordinal);

        var ok = body.Success
                 && score >= _options.MinimumScore
                 && actionOk;

        return new RecaptchaVerificationResult
        {
            Success = ok,
            Score = score,
            Action = body.Action,
            ErrorCodes = body.ErrorCodes ?? Array.Empty<string>(),
        };
    }
}

internal sealed class SiteVerifyResponseDto
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("score")]
    public double? Score { get; set; }

    [JsonPropertyName("action")]
    public string? Action { get; set; }

    [JsonPropertyName("error-codes")]
    public string[]? ErrorCodes { get; set; }
}
