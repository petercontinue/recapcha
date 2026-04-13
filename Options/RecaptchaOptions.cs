namespace recapcha.Options;

public sealed class RecaptchaOptions
{
    public const string SectionName = "GoogleReCaptcha";

    public string SiteKey { get; set; } = "";

    /// <summary>Server-side only. Prefer User Secrets or environment variables in production.</summary>
    public string SecretKey { get; set; } = "";

    /// <summary>reCAPTCHA v3 score threshold (0.0–1.0).</summary>
    public double MinimumScore { get; set; } = 0.5;
}
