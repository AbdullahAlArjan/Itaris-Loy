using System.Net;
using System.Net.Http.Json;

namespace Itaris.Tests.Integration;

/// <summary>
/// Customer auth core (doc 05 A1–A4): request OTP → verify → rotate refresh → reuse detection.
/// Runs against a real PostgreSQL container; the Development dev-bypass OTP code is used.
/// </summary>
[Collection(ApiCollection.Name)]
public class AuthFlowTests(ApiFixture fixture)
{
    private sealed record VerifyResponse(string AccessToken, string RefreshToken, int ExpiresIn, bool IsNewUser, CustomerDto Customer);
    private sealed record CustomerDto(Guid Id, string? FirstName, string PhoneNumber);
    private sealed record RefreshResponse(string AccessToken, string RefreshToken, int ExpiresIn);
    private sealed record ErrorBody(ErrorEnvelope Error);
    private sealed record ErrorEnvelope(string Code, string Message);

    private static async Task<Guid> RequestChallenge(HttpClient client, string phone)
    {
        var response = await client.PostAsJsonAsync("/v1/auth/otp/request",
            new { phoneNumber = phone, purpose = "login" });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        return Guid.Parse(body!["challengeId"].ToString()!);
    }

    [Fact]
    public async Task Full_customer_auth_lifecycle()
    {
        var client = fixture.CreateClient();
        var phone = "+96279" + Random.Shared.Next(1_000_000, 9_999_999);

        // A1 → A2: first verify creates the customer (isNewUser = true)
        var challengeId = await RequestChallenge(client, phone);
        var verifyResp = await client.PostAsJsonAsync("/v1/auth/otp/verify", new
        {
            challengeId,
            code = "000000",
            device = new { platform = "ios", model = "iPhone 15", fcmToken = (string?)null },
        });
        Assert.Equal(HttpStatusCode.OK, verifyResp.StatusCode);
        var verify = await verifyResp.Content.ReadFromJsonAsync<VerifyResponse>();
        Assert.NotNull(verify);
        Assert.True(verify.IsNewUser);
        Assert.Equal(phone, verify.Customer.PhoneNumber);
        Assert.False(string.IsNullOrEmpty(verify.AccessToken));

        // A3: rotate the refresh token
        var refreshResp = await client.PostAsJsonAsync("/v1/auth/token/refresh",
            new { refreshToken = verify.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, refreshResp.StatusCode);
        var refreshed = await refreshResp.Content.ReadFromJsonAsync<RefreshResponse>();
        Assert.NotNull(refreshed);
        Assert.NotEqual(verify.RefreshToken, refreshed.RefreshToken);

        // A3 reuse detection: presenting the now-consumed original token revokes the family
        var reuseResp = await client.PostAsJsonAsync("/v1/auth/token/refresh",
            new { refreshToken = verify.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, reuseResp.StatusCode);
        var reuseErr = await reuseResp.Content.ReadFromJsonAsync<ErrorBody>();
        Assert.Equal("TOKEN_REUSE_DETECTED", reuseErr!.Error.Code);

        // The rotated (newer) token is now also revoked because the whole family was killed
        var afterRevokeResp = await client.PostAsJsonAsync("/v1/auth/token/refresh",
            new { refreshToken = refreshed.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, afterRevokeResp.StatusCode);
    }

    [Fact]
    public async Task Second_login_for_same_phone_is_not_new_user()
    {
        var client = fixture.CreateClient();
        var phone = "+96279" + Random.Shared.Next(1_000_000, 9_999_999);

        var firstChallenge = await RequestChallenge(client, phone);
        await client.PostAsJsonAsync("/v1/auth/otp/verify", new
        {
            challengeId = firstChallenge,
            code = "000000",
            device = new { platform = "android", model = (string?)null, fcmToken = (string?)null },
        });

        var secondChallenge = await RequestChallenge(client, phone);
        var secondVerify = await client.PostAsJsonAsync("/v1/auth/otp/verify", new
        {
            challengeId = secondChallenge,
            code = "000000",
            device = new { platform = "android", model = (string?)null, fcmToken = (string?)null },
        });
        var body = await secondVerify.Content.ReadFromJsonAsync<VerifyResponse>();
        Assert.False(body!.IsNewUser);
    }

    [Fact]
    public async Task Wrong_code_is_rejected_with_otp_invalid()
    {
        var client = fixture.CreateClient();
        var phone = "+96279" + Random.Shared.Next(1_000_000, 9_999_999);
        var challengeId = await RequestChallenge(client, phone);

        var resp = await client.PostAsJsonAsync("/v1/auth/otp/verify", new
        {
            challengeId,
            code = "999999",
            device = new { platform = "ios", model = (string?)null, fcmToken = (string?)null },
        });

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        var err = await resp.Content.ReadFromJsonAsync<ErrorBody>();
        Assert.Equal("OTP_INVALID", err!.Error.Code);
    }
}
