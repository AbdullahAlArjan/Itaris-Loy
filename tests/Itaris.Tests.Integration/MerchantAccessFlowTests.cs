using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Itaris.Tests.Integration;

/// <summary>
/// Phase 2 Definition of Done (doc 06): admin creates merchant → owner login → invite staff →
/// staff PIN login → a forbidden action returns 403 with the FORBIDDEN code.
/// </summary>
public class MerchantAccessFlowTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private sealed record TokenOnly(string AccessToken, string RefreshToken, int ExpiresIn);
    private sealed record CreateMerchantResult(Guid MerchantId, string Code, Guid OwnerUserId);
    private sealed record AuthTokens(string AccessToken, string RefreshToken, int ExpiresIn, MerchantDto Merchant);
    private sealed record MerchantDto(Guid Id, string Code, string NameEn);
    private sealed record InviteResult(Guid StaffMemberId, string InviteToken);
    private sealed record ErrorBody(ErrorEnvelope Error);
    private sealed record ErrorEnvelope(string Code, string Message);

    private static readonly object Device = new { platform = "android", model = "POS-1", fcmToken = (string?)null };

    private static void Bearer(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    [Fact]
    public async Task Full_access_lifecycle_ends_in_forbidden_for_cashier()
    {
        var client = fixture.CreateClient();
        var ownerEmail = $"owner{Random.Shared.Next(1_000_000)}@example.com";
        var staffContact = $"cashier{Random.Shared.Next(1_000_000)}@example.com";

        // 1. Admin login (seeded platform admin from appsettings.Development.json)
        var adminLogin = await client.PostAsJsonAsync("/v1/auth/admin/login", new
        {
            email = "admin@itaris.local",
            password = "dev-admin-pass-change-me",
            device = Device,
        });
        Assert.Equal(HttpStatusCode.OK, adminLogin.StatusCode);
        var admin = await adminLogin.Content.ReadFromJsonAsync<TokenOnly>();

        // 2. Admin creates a merchant + owner
        Bearer(client, admin!.AccessToken);
        var createResp = await client.PostAsJsonAsync("/v1/admin/merchants", new
        {
            nameAr = "مقهى تجريبي",
            nameEn = "Test Roasters",
            category = "cafe",
            owner = new { email = ownerEmail, password = "OwnerPass123!" },
        });
        Assert.Equal(HttpStatusCode.OK, createResp.StatusCode);
        var created = await createResp.Content.ReadFromJsonAsync<CreateMerchantResult>();
        Assert.NotNull(created);

        // 3. Owner login (email/password)
        client.DefaultRequestHeaders.Authorization = null;
        var ownerLogin = await client.PostAsJsonAsync("/v1/auth/owner/login", new
        {
            email = ownerEmail,
            password = "OwnerPass123!",
            device = Device,
        });
        Assert.Equal(HttpStatusCode.OK, ownerLogin.StatusCode);
        var owner = await ownerLogin.Content.ReadFromJsonAsync<AuthTokens>();
        Assert.Equal(created!.Code, owner!.Merchant.Code);

        // 4. Owner invites a cashier (owner has staff.manage)
        Bearer(client, owner.AccessToken);
        var inviteResp = await client.PostAsJsonAsync("/v1/merchant/staff", new
        {
            displayName = "Omar",
            phoneOrEmail = staffContact,
            role = "cashier",
            branchId = (Guid?)null,
        });
        Assert.Equal(HttpStatusCode.OK, inviteResp.StatusCode);
        var invite = await inviteResp.Content.ReadFromJsonAsync<InviteResult>();

        // 5. Cashier accepts the invite and sets a PIN
        client.DefaultRequestHeaders.Authorization = null;
        var acceptResp = await client.PostAsJsonAsync("/v1/auth/staff/invites/accept", new
        {
            inviteToken = invite!.InviteToken,
            pin = "1357",
            device = Device,
        });
        Assert.Equal(HttpStatusCode.OK, acceptResp.StatusCode);

        // 6. Staff PIN login
        var staffLogin = await client.PostAsJsonAsync("/v1/auth/staff/login", new
        {
            merchantCode = created.Code,
            phoneOrEmail = staffContact,
            pin = "1357",
            device = Device,
        });
        Assert.Equal(HttpStatusCode.OK, staffLogin.StatusCode);
        var staff = await staffLogin.Content.ReadFromJsonAsync<AuthTokens>();

        // 7. Forbidden: a cashier lacks staff.manage → 403 FORBIDDEN
        Bearer(client, staff!.AccessToken);
        var forbiddenResp = await client.PostAsJsonAsync("/v1/merchant/staff", new
        {
            displayName = "Rana",
            phoneOrEmail = "rana@example.com",
            role = "cashier",
            branchId = (Guid?)null,
        });
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResp.StatusCode);
        var err = await forbiddenResp.Content.ReadFromJsonAsync<ErrorBody>();
        Assert.Equal("FORBIDDEN", err!.Error.Code);
    }

    [Fact]
    public async Task Wrong_owner_password_is_invalid_credentials()
    {
        var client = fixture.CreateClient();
        var ownerEmail = $"owner{Random.Shared.Next(1_000_000)}@example.com";

        var adminLogin = await client.PostAsJsonAsync("/v1/auth/admin/login", new
        {
            email = "admin@itaris.local",
            password = "dev-admin-pass-change-me",
            device = Device,
        });
        var admin = await adminLogin.Content.ReadFromJsonAsync<TokenOnly>();
        Bearer(client, admin!.AccessToken);
        await client.PostAsJsonAsync("/v1/admin/merchants", new
        {
            nameAr = "مقهى", nameEn = "Cred Test", category = "cafe",
            owner = new { email = ownerEmail, password = "OwnerPass123!" },
        });

        client.DefaultRequestHeaders.Authorization = null;
        var badLogin = await client.PostAsJsonAsync("/v1/auth/owner/login", new
        {
            email = ownerEmail, password = "WrongPass!", device = Device,
        });
        Assert.Equal(HttpStatusCode.Unauthorized, badLogin.StatusCode);
        var err = await badLogin.Content.ReadFromJsonAsync<ErrorBody>();
        Assert.Equal("INVALID_CREDENTIALS", err!.Error.Code);
    }

    [Fact]
    public async Task Admin_endpoint_without_token_is_unauthorized()
    {
        var client = fixture.CreateClient();
        var resp = await client.PostAsJsonAsync("/v1/admin/merchants", new
        {
            nameAr = "x", nameEn = "x", category = "cafe",
            owner = new { email = "x@example.com", password = "x" },
        });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}
