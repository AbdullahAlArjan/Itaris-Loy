using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Itaris.Tests.Integration;

/// <summary>
/// Phase 5 — rewards & redemption. Covers the two-phase flow and doc 06's critical suite: 20 parallel
/// confirms of one code → exactly one success (the double-redemption defense), out-of-stock race,
/// and TTL expiry releasing the hold.
/// </summary>
[Collection(ApiCollection.Name)]
public class RedemptionFlowTests(ApiFixture fixture)
{
    private sealed record TokenOnly(string AccessToken, string RefreshToken, int ExpiresIn);
    private sealed record CreateMerchantResult(Guid MerchantId, string Code, Guid OwnerUserId, Guid DefaultBranchId);
    private sealed record AuthTokens(string AccessToken, string RefreshToken, int ExpiresIn, MerchantDto Merchant);
    private sealed record MerchantDto(Guid Id, string Code, string NameEn);
    private sealed record ProgramResult(Guid ProgramId, string Type, string Status, int? RuleVersion);
    private sealed record InviteResult(Guid StaffMemberId, string InviteToken);
    private sealed record RewardResult(Guid Id, string NameAr, string NameEn, string CostType, long? PointsCost, long? StockRemaining, string Status);
    private sealed record IntentResult(Guid RedemptionId, string Code, string Status, DateTimeOffset ExpiresAt, long PointsHeld, bool StampCardConsumed);
    private sealed record ConfirmResult(Guid RedemptionId, string Status, DateTimeOffset ConfirmedAt);
    private sealed record MembershipItem(Guid MembershipId, Guid MerchantId, Guid ProgramId, string ProgramType, long PointsBalance);
    private sealed record ErrorBody(ErrorEnvelope Error);
    private sealed record ErrorEnvelope(string Code, string Message);

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
        { nameAr = "م", nameEn = "Rw Co", category = "cafe", owner = new { email = ownerEmail, password = "OwnerPass123!" } }))
            .Content.ReadFromJsonAsync<CreateMerchantResult>();

        client.DefaultRequestHeaders.Authorization = null;
        var owner = await (await client.PostAsJsonAsync("/v1/auth/owner/login",
            new { email = ownerEmail, password = "OwnerPass123!", device = Device })).Content.ReadFromJsonAsync<AuthTokens>();
        Bearer(client, owner!.AccessToken);

        // Points program at 1 pt/JOD.
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

    /// <summary>A customer who has earned <paramref name="points"/> points at the merchant (via a cashier sale).</summary>
    private static async Task<(string Token, Guid CustomerId)> CustomerWithPointsAsync(HttpClient client, Env env, long points)
    {
        var phone = "+96279" + Random.Shared.Next(1_000_000, 9_999_999);
        var challenge = await client.PostAsJsonAsync("/v1/auth/otp/request", new { phoneNumber = phone, purpose = "login" });
        var challengeId = (await challenge.Content.ReadFromJsonAsync<Dictionary<string, object>>())!["challengeId"].ToString();
        var verify = await client.PostAsJsonAsync("/v1/auth/otp/verify", new { challengeId, code = "000000", device = Device });
        var body = await verify.Content.ReadFromJsonAsync<Dictionary<string, System.Text.Json.JsonElement>>();
        var token = body!["accessToken"].GetString()!;
        var customerId = Guid.Parse(body["customer"].GetProperty("id").GetString()!);

        if (points > 0)
        {
            Bearer(client, env.CashierToken);
            var sale = new HttpRequestMessage(HttpMethod.Post, "/v1/pos/transactions")
            { Content = JsonContent.Create(new { customerId, branchId = env.BranchId, amountMinor = points * 1000, currency = "JOD", occurredAt = (DateTimeOffset?)null, note = (string?)null, duplicateOverride = false }) };
            sale.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            (await client.SendAsync(sale)).EnsureSuccessStatusCode();
        }

        return (token, customerId);
    }

    private static async Task<RewardResult> CreateActiveRewardAsync(HttpClient client, Env env, long? pointsCost, long? stock)
    {
        Bearer(client, env.OwnerToken);
        var reward = await (await client.PostAsJsonAsync("/v1/merchant/rewards", new
        {
            nameAr = "قهوة", nameEn = "Free Coffee",
            descriptionAr = (string?)null, descriptionEn = (string?)null,
            costType = pointsCost is null ? "stamp_completion" : "points",
            pointsCost, stockRemaining = stock, perCustomerLimit = (int?)null,
        })).Content.ReadFromJsonAsync<RewardResult>();
        await client.PostAsync($"/v1/merchant/rewards/{reward!.Id}/activate", null);
        return reward;
    }

    private static HttpRequestMessage Intent(Guid merchantId, Guid rewardId, string key)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"/v1/customers/me/redemptions?merchantId={merchantId}")
        { Content = JsonContent.Create(new { rewardId }) };
        req.Headers.Add("Idempotency-Key", key);
        return req;
    }

    private static HttpRequestMessage Confirm(string code, Guid branchId, string key)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/v1/pos/redemptions/confirm")
        { Content = JsonContent.Create(new { redemptionCode = code, branchId }) };
        req.Headers.Add("Idempotency-Key", key);
        return req;
    }

    private async Task<long> PointsBalanceAsync(HttpClient client, string customerToken)
    {
        Bearer(client, customerToken);
        var list = await client.GetFromJsonAsync<Dictionary<string, List<MembershipItem>>>("/v1/customers/me/memberships");
        return list!["items"].Sum(m => m.PointsBalance);
    }

    [Fact]
    public async Task Full_points_redemption_deducts_the_balance()
    {
        var client = fixture.CreateClient();
        var env = await SetupAsync(client);
        var (customerToken, _) = await CustomerWithPointsAsync(client, env, 10);
        var reward = await CreateActiveRewardAsync(client, env, pointsCost: 6, stock: null);

        Bearer(client, customerToken);
        var intentResp = await client.SendAsync(Intent(env.MerchantId, reward.Id, Guid.NewGuid().ToString()));
        Assert.Equal(HttpStatusCode.OK, intentResp.StatusCode);
        var intent = await intentResp.Content.ReadFromJsonAsync<IntentResult>();
        Assert.Equal(6, intent!.PointsHeld);

        // Held at intent → balance already dropped to 4.
        Assert.Equal(4, await PointsBalanceAsync(client, customerToken));

        Bearer(client, env.CashierToken);
        var confirmResp = await client.SendAsync(Confirm(intent.Code, env.BranchId, Guid.NewGuid().ToString()));
        Assert.Equal(HttpStatusCode.OK, confirmResp.StatusCode);
        var confirm = await confirmResp.Content.ReadFromJsonAsync<ConfirmResult>();
        Assert.Equal("completed", confirm!.Status);

        // Confirm finalizes; balance stays 4.
        Assert.Equal(4, await PointsBalanceAsync(client, customerToken));
    }

    [Fact]
    public async Task Twenty_parallel_confirms_of_one_code_yield_exactly_one_success()
    {
        var client = fixture.CreateClient();
        var env = await SetupAsync(client);
        var (customerToken, _) = await CustomerWithPointsAsync(client, env, 10);
        var reward = await CreateActiveRewardAsync(client, env, pointsCost: 5, stock: null);

        Bearer(client, customerToken);
        var intent = await (await client.SendAsync(Intent(env.MerchantId, reward.Id, Guid.NewGuid().ToString())))
            .Content.ReadFromJsonAsync<IntentResult>();

        // 20 parallel confirms of the same code, each with a distinct idempotency key.
        Bearer(client, env.CashierToken);
        var tasks = Enumerable.Range(0, 20)
            .Select(_ => client.SendAsync(Confirm(intent!.Code, env.BranchId, Guid.NewGuid().ToString())))
            .ToArray();
        var responses = await Task.WhenAll(tasks);

        var okCount = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        Assert.Equal(1, okCount); // exactly one confirm wins
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.OK),
            r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode)); // rest → REDEMPTION_ALREADY_USED (409)
    }

    [Fact]
    public async Task Out_of_stock_race_lets_only_one_intent_through()
    {
        var client = fixture.CreateClient();
        var env = await SetupAsync(client);
        var reward = await CreateActiveRewardAsync(client, env, pointsCost: 5, stock: 1);

        var (tokenA, _) = await CustomerWithPointsAsync(client, env, 10);
        var (tokenB, _) = await CustomerWithPointsAsync(client, env, 10);

        // Two different customers race for the last unit.
        var clientA = fixture.CreateClient();
        var clientB = fixture.CreateClient();
        Bearer(clientA, tokenA);
        Bearer(clientB, tokenB);
        var responses = await Task.WhenAll(
            clientA.SendAsync(Intent(env.MerchantId, reward.Id, Guid.NewGuid().ToString())),
            clientB.SendAsync(Intent(env.MerchantId, reward.Id, Guid.NewGuid().ToString())));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        var loser = responses.First(r => r.StatusCode != HttpStatusCode.OK);
        var err = await loser.Content.ReadFromJsonAsync<ErrorBody>();
        Assert.Equal("REWARD_OUT_OF_STOCK", err!.Error.Code);
    }

    [Fact]
    public async Task Expiry_releases_the_hold_and_restores_points()
    {
        var client = fixture.CreateClient();
        var env = await SetupAsync(client);
        var (customerToken, _) = await CustomerWithPointsAsync(client, env, 10);
        var reward = await CreateActiveRewardAsync(client, env, pointsCost: 6, stock: 3);

        Bearer(client, customerToken);
        var intent = await (await client.SendAsync(Intent(env.MerchantId, reward.Id, Guid.NewGuid().ToString())))
            .Content.ReadFromJsonAsync<IntentResult>();
        Assert.Equal(4, await PointsBalanceAsync(client, customerToken)); // 10 - 6 held

        // Force the intent past its TTL, then run the expiry sweep.
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Itaris.Modules.Rewards.Persistence.RewardsDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE rewards.redemptions SET expires_at = now() - interval '1 minute' WHERE id = {intent!.RedemptionId}");
            var sweeper = scope.ServiceProvider.GetRequiredService<Itaris.Modules.Rewards.Features.Redeem.ExpireStaleIntentsService>();
            var released = await sweeper.SweepAsync(default);
            Assert.True(released >= 1);
        }

        // Hold released → points back to 10, stock restored.
        Assert.Equal(10, await PointsBalanceAsync(client, customerToken));

        Bearer(client, customerToken);
        var poll = await client.GetFromJsonAsync<Dictionary<string, object>>($"/v1/customers/me/redemptions/{intent!.RedemptionId}");
        Assert.Equal("expired", poll!["status"].ToString());
    }

    [Fact]
    public async Task Insufficient_points_is_rejected()
    {
        var client = fixture.CreateClient();
        var env = await SetupAsync(client);
        var (customerToken, _) = await CustomerWithPointsAsync(client, env, 2);
        var reward = await CreateActiveRewardAsync(client, env, pointsCost: 50, stock: null);

        Bearer(client, customerToken);
        var resp = await client.SendAsync(Intent(env.MerchantId, reward.Id, Guid.NewGuid().ToString()));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var err = await resp.Content.ReadFromJsonAsync<ErrorBody>();
        Assert.Equal("INSUFFICIENT_POINTS", err!.Error.Code);
    }
}
