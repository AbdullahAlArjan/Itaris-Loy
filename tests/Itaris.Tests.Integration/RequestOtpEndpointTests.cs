using System.Net;
using System.Net.Http.Json;

namespace Itaris.Tests.Integration;

/// <summary>
/// Phase 1 walking-skeleton verification (doc 06): HTTP → validation → handler →
/// EF Core → real PostgreSQL, response shaped per doc 05 A1.
/// </summary>
public class RequestOtpEndpointTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private sealed record OtpResponse(Guid ChallengeId, int ExpiresInSeconds, int ResendAfterSeconds);
    private sealed record ErrorBody(ErrorEnvelope Error);
    private sealed record ErrorEnvelope(string Code, string Message);

    [Fact]
    public async Task Valid_request_creates_challenge_with_contract_shape()
    {
        var client = fixture.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/v1/auth/otp/request",
            new { phoneNumber = "+962790000001", purpose = "login" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<OtpResponse>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body.ChallengeId);
        Assert.Equal(300, body.ExpiresInSeconds);
        Assert.Equal(45, body.ResendAfterSeconds);
    }

    [Fact]
    public async Task Non_jordanian_phone_returns_invalid_phone_envelope()
    {
        var client = fixture.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/v1/auth/otp/request",
            new { phoneNumber = "+15551234567", purpose = "login" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorBody>();
        Assert.NotNull(body);
        Assert.Equal("INVALID_PHONE", body.Error.Code);
    }

    [Fact]
    public async Task Health_endpoint_is_healthy()
    {
        var client = fixture.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
