using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using recapcha.Options;
using recapcha.Services;

namespace recapcha.Pages;

public class IndexModel : PageModel
{
    public const string DemoAction = "demo_submit";

    private readonly ILogger<IndexModel> _logger;
    private readonly IRecaptchaService _recaptcha;
    private readonly RecaptchaOptions _options;

    public IndexModel(
        ILogger<IndexModel> logger,
        IRecaptchaService recaptcha,
        IOptions<RecaptchaOptions> options)
    {
        _logger = logger;
        _recaptcha = recaptcha;
        _options = options.Value;
    }

    public string SiteKey => _options.SiteKey;

    [BindProperty]
    public string Message { get; set; } = "";

    [BindProperty]
    public string RecaptchaToken { get; set; } = "";

    public string? StatusMessage { get; set; }

    public bool VerificationSucceeded { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(RecaptchaToken))
        {
            ModelState.AddModelError(string.Empty, "reCAPTCHA token was not received. Check that the script loaded and try again.");
            return Page();
        }

        var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _recaptcha.VerifyAsync(RecaptchaToken, remoteIp, DemoAction, cancellationToken);

        if (!result.Success)
        {
            _logger.LogWarning(
                "reCAPTCHA failed. Score {Score}, action {Action}, errors: {Errors}",
                result.Score,
                result.Action,
                string.Join(",", result.ErrorCodes));

            ModelState.AddModelError(
                string.Empty,
                $"reCAPTCHA verification failed (score: {result.Score:F2}, minimum: {_options.MinimumScore:F2}).");
            return Page();
        }

        VerificationSucceeded = true;
        StatusMessage = $"Verified. Message: {Message}";
        return Page();
    }
}
