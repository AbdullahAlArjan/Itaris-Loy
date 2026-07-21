using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Itaris.Tests.Integration;

/// <summary>
/// Verifies the audit interceptor writes rows for staff/admin mutations, and that permission
/// gating returns 403 vs 200 per role (doc 06 Phase 2: permission-matrix + audit tests).
/// </summary>
[Collection(ApiCollection.Name)]
public class AuditAndAuthorizationTests(ApiFixture fixture)
{
    private sealed record TokenOnly(string AccessToken, string RefreshToken, int ExpiresIn);
    private sealed record CreateMerchantResult(Guid MerchantId, string Code, Guid OwnerUserId);
    private sealed record AuthTokens(string AccessToken, string RefreshToken, int ExpiresIn, MerchantDto Merchant);
    private sealed record MerchantDto(Guid Id, string Code, string NameEn);
    private sealed record InviteResult(Guid StaffMemberId, string InviteToken);

    private static readonly object Device = new { platform = "web", model = (string?)null, fcmToken = (string?)null };

    private static void Bearer(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private static async Task<string> AdminTokenAsync(HttpClient client)
    {
        var resp = await client.PostAsJsonAsync("/v1/auth/admin/login", new
        {
            email = "admin@itaris.local", password = "dev-admin-pass-change-me", device = Device,
        });
        var body = await resp.Content.ReadFromJsonAsync<TokenOnly>();
        return body!.AccessToken;
    }

    private static async Task<(CreateMerchantResult merchant, string ownerEmail)> CreateMerchantAsync(HttpClient client)
    {
        var ownerEmail = $"owner{Guid.NewGuid():N}@example.com";
        Bearer(client, await AdminTokenAsync(client));
        var resp = await client.PostAsJsonAsync("/v1/admin/merchants", new
        {
            nameAr = "مقهى", nameEn = "Audit Test Co", category = "cafe",
            owner = new { email = ownerEmail, password = "OwnerPass123!" },
        });
        client.DefaultRequestHeaders.Authorization = null;
        return ((await resp.Content.ReadFromJsonAsync<CreateMerchantResult>())!, ownerEmail);
    }

    private static async Task<AuthTokens> OwnerLoginAsync(HttpClient client, string email)
    {
        var resp = await client.PostAsJsonAsync("/v1/auth/owner/login",
            new { email, password = "OwnerPass123!", device = Device });
        return (await resp.Content.ReadFromJsonAsync<AuthTokens>())!;
    }

    [Fact]
    public async Task Creating_a_merchant_writes_an_audit_row_for_the_admin()
    {
        var client = fixture.CreateClient();
        var (merchant, _) = await CreateMerchantAsync(client);

        // The admin is platform-level, so the row's merchant_id is null (doc 04: null=platform);
        // the created Merchant entity is identified by entity_type/entity_id, and actor_type=admin.
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Itaris.Modules.Ops.Persistence.OpsDbContext>();
        var row = await db.AuditLogs.FirstOrDefaultAsync(a =>
            a.EntityType == "Merchant" &&
            a.EntityId == merchant.MerchantId.ToString() &&
            a.Action == "insert");

        Assert.NotNull(row);
        Assert.Equal("admin", row.ActorType);
        Assert.Null(row.MerchantId);
    }

    [Fact]
    public async Task Owner_can_invite_but_cashier_cannot()
    {
        var client = fixture.CreateClient();
        var (merchant, ownerEmail) = await CreateMerchantAsync(client);
        var owner = await OwnerLoginAsync(client, ownerEmail);

        // Owner (has staff.manage) → 200
        Bearer(client, owner.AccessToken);
        var cashierContact = $"cashier{Guid.NewGuid():N}@example.com";
        var inviteResp = await client.PostAsJsonAsync("/v1/merchant/staff", new
        {
            displayName = "Omar", phoneOrEmail = cashierContact, role = "cashier", branchId = (Guid?)null,
        });
        Assert.Equal(HttpStatusCode.OK, inviteResp.StatusCode);
        var invite = await inviteResp.Content.ReadFromJsonAsync<InviteResult>();

        // Cashier activates, logs in, and is denied the same action → 403
        client.DefaultRequestHeaders.Authorization = null;
        await client.PostAsJsonAsync("/v1/auth/staff/invites/accept",
            new { inviteToken = invite!.InviteToken, pin = "2468", device = Device });
        var staffLogin = await client.PostAsJsonAsync("/v1/auth/staff/login", new
        {
            merchantCode = merchant.Code, phoneOrEmail = cashierContact, pin = "2468", device = Device,
        });
        var staff = await staffLogin.Content.ReadFromJsonAsync<AuthTokens>();

        Bearer(client, staff!.AccessToken);
        var denied = await client.PostAsJsonAsync("/v1/merchant/staff", new
        {
            displayName = "Rana", phoneOrEmail = "rana@example.com", role = "cashier", branchId = (Guid?)null,
        });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task Owner_token_missing_returns_401_on_gated_endpoint()
    {
        var client = fixture.CreateClient();
        var resp = await client.PostAsJsonAsync("/v1/merchant/staff", new
        {
            displayName = "x", phoneOrEmail = "x@example.com", role = "cashier", branchId = (Guid?)null,
        });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}
