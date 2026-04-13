# Recapcha — ASP.NET Core 8 Razor Pages + Google reCAPTCHA v3

This repository documents a small **ASP.NET Core 8** web application built with **Razor Pages**, extended with **Google reCAPTCHA v3** for invisible, score-based bot protection on a demo form.

---

## Overview

| Item | Value |
|------|--------|
| Framework | .NET 8 |
| UI | Razor Pages |
| Human verification | Google reCAPTCHA **v3** (no checkbox; background score) |
| Local domain | `localhost` (registered in Google reCAPTCHA Admin) |

The demo flow: the user submits a form; the browser obtains a **reCAPTCHA token** via JavaScript; the server verifies that token with Google’s **siteverify** API and enforces a **minimum score** before accepting the submission.

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- A Google account to use [Google reCAPTCHA Admin](https://www.google.com/recaptcha/admin)
- A browser for local testing (HTTPS or `http://localhost` per your launch profile)

---

## 1. Creating the project (Cursor / CLI)

The project was created from the official **ASP.NET Core Web App (Razor Pages)** template targeting **.NET 8**.

From the parent folder (e.g. `D:\code-fullstack`), the equivalent CLI commands are:

```bash
dotnet new webapp -n recapcha -f net8.0
cd recapcha
```

That scaffolded the default Razor Pages structure: `Program.cs`, `Pages/` (including `Index`, `Privacy`, `Error`), `wwwroot/`, and `appsettings.json`.

Optionally, a Visual Studio **solution** file was added so the project opens cleanly in Visual Studio:

```bash
dotnet new sln -n recapcha -f sln
dotnet sln add recapcha.csproj
```

> **Note:** On newer SDKs, `dotnet new sln` defaults to `.slnx` unless you pass **`-f sln`** for the classic `.sln` format.

---

## 2. Registering the site in Google reCAPTCHA Admin

These steps were performed in the [reCAPTCHA Admin console](https://www.google.com/recaptcha/admin).

1. **Create** a new site (label used in the console, e.g. for local development).
2. Choose **reCAPTCHA v3** (not v2 “checkbox” — v3 does not show an interactive challenge; it returns a **score**).
3. Under **Domains**, add **`localhost`** so keys work when you run the app locally (e.g. `https://localhost:7xxx`).
4. Accept the terms and submit.

After creation, Google provides **two keys**:

| Key | Purpose | Where it belongs |
|-----|---------|------------------|
| **Site key** | Public; used in HTML/JS to load the widget and call `grecaptcha.execute` | Client-side (Razor view) and may live in `appsettings.json` for server-side injection into the page |
| **Secret key** | Private; proves server-to-server calls to Google | **Never** expose to browsers or public repos; use **User Secrets** (dev) or **environment variables / secret stores** (production) |

---

## 3. Security model (important)

- The **site key** is not a secret; it appears in page source and network traffic.
- The **secret key** must stay on the server. This project stores it in **.NET User Secrets** during development (see below), not as a committed secret in plain text.

If a secret is ever exposed, **rotate** it in the Google Admin console and update your configuration.

---

## 4. Integrating Google reCAPTCHA v3 — design

### 4.1 End-to-end flow

1. **Browser** loads `https://www.google.com/recaptcha/api.js?render=<SITE_KEY>`.
2. On **form submit**, JavaScript calls `grecaptcha.execute(<SITE_KEY>, { action: '<ACTION_NAME>' })`, receives a **token**, writes it to a **hidden field**, then submits the form.
3. **Server** (`OnPostAsync`) reads the token, calls Google’s **siteverify** endpoint with the **secret key** and token (and optionally the client IP).
4. Google responds with JSON: `success`, **`score`** (0.0–1.0), **`action`**, and optional `error-codes`.
5. The app compares **score** to **`MinimumScore`** in configuration and checks that **`action`** matches the expected string used in `grecaptcha.execute`.

The **action** string is a logical name for that interaction (e.g. `demo_submit`). It should match between client and server.

---

## 5. Implementation in this repository

### 5.1 Configuration class — `Options/RecaptchaOptions.cs`

A strongly typed options class binds the `GoogleReCaptcha` section from configuration:

- `SiteKey` — public site key.
- `SecretKey` — loaded from User Secrets or environment in practice.
- `MinimumScore` — threshold for accepting a request (default in code is `0.5`; your `appsettings.json` may override, e.g. `0.99` for stricter tests).

```csharp
public sealed class RecaptchaOptions
{
    public const string SectionName = "GoogleReCaptcha";

    public string SiteKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
    public double MinimumScore { get; set; } = 0.5;
}
```

### 5.2 `appsettings.json`

The **site key** and **minimum score** are suitable for `appsettings.json` (non-secret). **Do not** commit the **secret key** here for real deployments.

Example shape:

```json
{
  "GoogleReCaptcha": {
    "SiteKey": "<YOUR_SITE_KEY>",
    "MinimumScore": 0.5
  }
}
```

### 5.3 User Secrets (development secret key)

The project file defines a stable **`UserSecretsId`** so the Secret Manager can store `GoogleReCaptcha:SecretKey` locally:

```xml
<UserSecretsId>recapcha-web-8f3a2b1c-4d5e-6f7a-8b9c-0d1e2f3a4b5c</UserSecretsId>
```

From the project directory:

```bash
dotnet user-secrets set "GoogleReCaptcha:SecretKey" "<YOUR_SECRET_KEY>"
```

At runtime, **Development** loads user secrets automatically when this ID is present.

### 5.4 Verification service — `Services/GoogleRecaptchaService.cs`

`GoogleRecaptchaService` uses **`IHttpClientFactory`**-injected `HttpClient` to **POST** `application/x-www-form-urlencoded` data to:

`https://www.google.com/recaptcha/api/siteverify`

Parameters:

- `secret` — secret key  
- `response` — token from the client  
- `remoteip` — optional; client IP  

The JSON response is deserialized (`success`, `score`, `action`, `error-codes`). The service returns a **`RecaptchaVerificationResult`** with `Success` only when:

- Google reports success,
- `score >= MinimumScore`,
- `action` matches the **expected action** passed from the page (or is empty in edge cases handled in code).

See `Services/IRecaptchaService.cs` for the interface and `RecaptchaVerificationResult`.

### 5.5 Dependency injection — `Program.cs`

```csharp
builder.Services.Configure<RecaptchaOptions>(
    builder.Configuration.GetSection(RecaptchaOptions.SectionName));
builder.Services.AddHttpClient<IRecaptchaService, GoogleRecaptchaService>();
builder.Services.AddRazorPages();
```

This registers options, a typed HTTP client for Google, and Razor Pages.

### 5.6 Razor view — `Pages/Index.cshtml`

- A **POST** form with `asp-page="/Index"` includes antiforgery support.
- A **hidden** input binds `RecaptchaToken` to the page model.
- **`@section Scripts`** loads the reCAPTCHA script with `render=<SiteKey>`.
- On **submit**, the script **prevents** the default submit, runs `grecaptcha.ready` → `grecaptcha.execute` with the same **action** constant as the server (`IndexModel.DemoAction`), writes the token to the hidden field, then calls `form.submit()` to POST once.

Key ideas:

- **One** token per submission; tokens are short-lived.
- The **action** in JS must match the server’s expected action (here: `demo_submit`).

### 5.7 Page model — `Pages/Index.cshtml.cs`

`OnPostAsync`:

1. Ensures `RecaptchaToken` was posted.
2. Calls `_recaptcha.VerifyAsync(token, remoteIp, DemoAction, ...)`.
3. On failure: logs a warning, adds a model error (including score vs minimum), returns the page.
4. On success: sets a success message (placeholder for “real” business logic).

This is where you would add database saves, emails, etc., **after** verification succeeds.

---

## 6. Differences between reCAPTCHA v2 and v3

| | v2 | v3 |
|---|----|-----|
| User experience | Often a checkbox or image challenge | **Invisible**; no puzzle for most users |
| Result | Typically pass/fail | **Score** + **action** name |
| Integration | Different JS API | `grecaptcha.execute` with `render` key |

This project uses **v3**, so you will **not** see a traditional “click all traffic lights” UI; you may see Google’s small **badge** on the page.

---

## 7. Running the application

```bash
cd recapcha
dotnet run
```

Open the URL shown in the console (see `Properties/launchSettings.json` for HTTP/HTTPS ports). Submit the **Message** form: the server should verify the token and either show success or validation errors.

---

## 8. Production checklist

- Set **`GoogleReCaptcha__SecretKey`** (environment variable) or use a cloud secret manager; **do not** rely on User Secrets on the server.
- Register **production domains** in reCAPTCHA Admin (not only `localhost`).
- Tune **`MinimumScore`** (stricter sites use higher values; too high may block legitimate users).
- Consider **rate limiting** and **logging** of failed verifications.

Environment variable example (double underscore nests configuration):

```bash
GoogleReCaptcha__SecretKey=<YOUR_SECRET_KEY>
```

---

## 9. Project structure (relevant files)

```
recapcha/
├── Program.cs                 # DI and pipeline
├── appsettings.json           # SiteKey, MinimumScore
├── recapcha.csproj            # UserSecretsId
├── Options/
│   └── RecaptchaOptions.cs
├── Services/
│   ├── IRecaptchaService.cs
│   └── GoogleRecaptchaService.cs
├── Pages/
│   ├── Index.cshtml           # Form + reCAPTCHA v3 scripts
│   └── Index.cshtml.cs        # POST verification + demo success path
└── README.md                  # This document
```

---

## 10. References

- [ASP.NET Core Razor Pages](https://learn.microsoft.com/aspnet/core/razor-pages/)
- [reCAPTCHA v3 — Google Developers](https://developers.google.com/recaptcha/docs/v3)
- [Verify the user’s response (siteverify)](https://developers.google.com/recaptcha/docs/verify)
- [Safe storage of app secrets in development (User Secrets)](https://learn.microsoft.com/aspnet/core/security/app-secrets)

---

*This README was written to record the development and integration steps for the **recapcha** demo project.*
