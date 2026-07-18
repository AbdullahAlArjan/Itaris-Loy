using Itaris.Modules.Identity.PublicApi;

namespace Itaris.Modules.Identity.Features.AdminLogin;

/// <summary>doc 05 — platform admin login (internal). Body { email, password }.</summary>
public sealed record AdminLoginRequest(string Email, string Password, DeviceRegistration Device);

public sealed record AdminLoginResponse(string AccessToken, string RefreshToken, int ExpiresIn);
