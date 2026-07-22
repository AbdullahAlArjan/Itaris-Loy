using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Itaris.Tests.Integration;

/// <summary>
/// Phase 6 — the merchant analytics overview (the 5 numbers), the audit-log read, and PDPL
/// account deletion (request + cancel within the grace period).
/// </summary>
[Collection(ApiCollection.Name)]
public class ReportingAndPrivacyTests(ApiFixture fixture)
{
    private sealed record TokenOnly(string AccessToken, string RefreshToken, int ExpiresIn);
    private sealed record CreateMerchantResult(Guid MerchantId, string Code, Guid OwnerUserId, Guid DefaultBranchId);
    private sealed record AuthTokens(string AccessToken, string RefreshToken, int ExpiresIn, MerchantDto Merchant);
    private sealed record MerchantDto(Guid Id, string Code, string NameEn);
    private sealed record ProgramResult(Guid ProgramId, string Type, string Status, int? RuleVersion);
    private sealed record InviteResult(Guid StaffMemberId, string InviteToken);
    private sealed record Overview(DateOnly From, DateOnly To, int VisitsThisWeek, double RepeatVisitRate,
        int ActiveMembers, long PointsIssued, long PointsRedeemed, int AtRiskCustomers,
        List<VisitsPoint> VisitsSeries, Guid? TopBranchId);
    private sealed record VisitsPoint(DateOnly Date, int Visits);
    private sealed record DeletionDto(string Status, DateTimeOffset RequestedAt, DateTimeOffset ExecuteAfter);

    private static readonly object Device = new { platform = "web", model = (string?)null, fcmToken = (string?)null };

    private static void Bearer(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private sealed record Env(string OwnerToken, string CashierToken, Guid MerchantId, Guid BranchId);

    private static async Task<Env> SetupAsync(HttpClient client)
    {
        var ownerEmail = $"owner{Guid.NewGuid():N}@example.com";
        var admin = await (await client.PostAsJsonAsync("/v1/auth/admin/login", new
        { email = "admin@itaris.local", password = "dev-admin-pass-change-me", device = Device })).Content.ReadFromJsonAsync<TokenOnly>();
        Bearer(client, admin!.AccessToken);
        var merchant = await (await client.PostAsJsonAsync("/v1/admin/merchants", new
        { nameAr = "م", nameEn = "An Co", category = "cafe", owner = new { email = ownerEmail, password = "OwnerPass123!" } }))
            .Content.ReadFromJsonAsync<CreateMerchantResult>();

        client.DefaultRequestHeaders.Authorization = null;
        var owner = await (await client.PostAsJsonAsync("/v1/auth/owner/login",
            new { email = ownerEmail, password = "OwnerPass123!", device = Device })).Content.ReadFromJsonAsync<AuthTokens>();
        Bearer(client, owner!.AccessToken);

        var program = await (await client.PostAsJsonAsync("/v1/merchant/programs",
            new { type = "points", nameAr = "ن", nameEn = "P" })).Content.ReadFromJsonAsync<ProgramResult>();
        await client.PatchAsJsonAsync($"/v1/merchant/programs/{program!.ProgramId}/rules", new
        { pointsPerJod = 1m, rounding = "Floor", minAmountMinor = 0, welcomeBonus = 0, cardSize = 0, stampsPerVisit = 1, maxStampsPerVisit = 1, expiryMonths = (int?)null });
        await client.PostAsJsonAsync($"/v1/merchant/programs/{program.ProgramId}/activate", new { });

        var contact = $"cashier{Guid.NewGuid():N}@example.com";
        var invite = await (await client.PostAsJsonAsync("/v1/merchant/staff", new
        { displayName = "O", phoneOrEmail = contact, role = "cashier", branchId = (Guid?)null })).Content.ReadFromJsonAsync<InviteResult>();
        client.DefaultRequestHeaders.Authorization = null;
        await client.PostAsJsonAsync("/v1/auth/staff/invites/accept", new { inviteToken = invite!.InviteToken, pin = "2468", device = Device });
        var cashier = await (await client.PostAsJsonAsync("/v1/auth/staff/login", new
        { merchantCode = merchant!.Code, phoneOrEmail = contact, pin = "2468", device = Device })).Content.ReadFromJsonAsync<AuthTokens>();

        return new Env(owner.AccessToken, cashier!.AccessToken, merchant.MerchantId, merchant.DefaultBranchId);
    }

    private static async Task<(string Token, Guid Id)> CustomerAsync(HttpClient client)
    {
        var phone = "+96279" + Random.Shared.Next(1_000_000, 9_999_999);
        var challenge = await client.PostAsJsonAsync("/v1/auth/otp/request", new { phoneNumber = phone, purpose = "login" });
        var challengeId = (await challenge.Content.ReadFromJsonAsync<Dictionary<string, object>>())!["challengeId"].ToString();
        var verify = await client.PostAsJsonAsync("/v1/auth/otp/verify", new { challengeId, code = "000000", device = Device });
        var body = await verify.Content.ReadFromJsonAsync<Dictionary<string, System.Text.Json.JsonElement>>();
        return (body!["accessToken"].GetString()!, Guid.Parse(body["customer"].GetProperty("id").GetString()!));
    }

    private static async Task SaleAsync(HttpClient client, Env env, Guid customerId, long amount)
    {
        Bearer(client, env.CashierToken);
        var sale = new HttpRequestMessage(HttpMethod.Post, "/v1/pos/transactions")
        { Content = JsonContent.Create(new { customerId, branchId = env.BranchId, amountMinor = amount, currency = "JOD", occurredAt = (DateTimeOffset?)null, note = (string?)null, duplicateOverride = true }) };
        sale.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        (await client.SendAsync(sale)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Analytics_overview_reports_the_five_numbers()
    {
        var client = fixture.CreateClient();
        var env = await SetupAsync(client);

        // Customer A visits twice (repeat), customer B once.
        var (_, a) = await CustomerAsync(client);
        var (_, b) = await CustomerAsync(client);
        await SaleAsync(client, env, a, 5000); // 5 points
        await SaleAsync(client, env, a, 3000); // +3 points
        await SaleAsync(client, env, b, 2000); // 2 points

        Bearer(client, env.OwnerToken);
        var overview = await client.GetFromJsonAsync<Overview>("/v1/merchant/analytics/overview");
        Assert.NotNull(overview);
        Assert.Equal(2, overview!.ActiveMembers);          // A and B
        Assert.Equal(0.5, overview.RepeatVisitRate);       // 1 of 2 members is a repeat
        Assert.Equal(3, overview.VisitsThisWeek);          // 3 sales just now
        Assert.Equal(10, overview.PointsIssued);           // 5 + 3 + 2
        Assert.Equal(env.BranchId, overview.TopBranchId);
        Assert.NotEmpty(overview.VisitsSeries);
    }

    [Fact]
    public async Task Owner_can_read_the_audit_trail()
    {
        var client = fixture.CreateClient();
        var env = await SetupAsync(client);

        // Owner invites a cashier — a staff mutation the interceptor records.
        Bearer(client, env.OwnerToken);
        await client.PostAsJsonAsync("/v1/merchant/staff", new
        { displayName = "Rana", phoneOrEmail = $"r{Guid.NewGuid():N}@x.jo", role = "cashier", branchId = (Guid?)null });

        // Poll the audit log (the flush is async in the interceptor's SavedChanges, but committed before response).
        List<Dictionary<string, object>> items = [];
        for (var attempt = 0; attempt < 10 && items.Count == 0; attempt++)
        {
            var body = await client.GetFromJsonAsync<Dictionary<string, List<Dictionary<string, object>>>>("/v1/merchant/audit-logs");
            items = body!["items"];
            if (items.Count == 0)
            {
                await Task.Delay(100);
            }
        }

        Assert.NotEmpty(items);
    }

    [Fact]
    public async Task Customer_can_request_and_cancel_account_deletion()
    {
        var client = fixture.CreateClient();
        var (token, _) = await CustomerAsync(client);
        Bearer(client, token);

        var request = await client.PostAsync("/v1/customers/me/deletion-request", null);
        Assert.Equal(HttpStatusCode.OK, request.StatusCode);
        var dto = await request.Content.ReadFromJsonAsync<DeletionDto>();
        Assert.Equal("pending", dto!.Status);
        Assert.True(dto.ExecuteAfter > dto.RequestedAt);

        // Status reflects pending.
        var status = await client.GetFromJsonAsync<DeletionDto>("/v1/customers/me/deletion-request");
        Assert.Equal("pending", status!.Status);

        // Cancel within the grace period.
        var cancel = await client.DeleteAsync("/v1/customers/me/deletion-request");
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);

        var afterCancel = await client.GetFromJsonAsync<DeletionDto>("/v1/customers/me/deletion-request");
        Assert.Equal("cancelled", afterCancel!.Status);
    }

    [Fact]
    public async Task Analytics_requires_the_analytics_permission()
    {
        var client = fixture.CreateClient();
        var resp = await client.GetAsync("/v1/merchant/analytics/overview");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}
