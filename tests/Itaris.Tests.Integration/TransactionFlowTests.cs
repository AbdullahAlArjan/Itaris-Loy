using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Itaris.Tests.Integration;

/// <summary>
/// Phase 4 — the consistency core. Covers the DoD (resolve QR → record 4.500 JOD → stamps update)
/// and doc 06's critical suite: parallel same-key idempotency replay; 20 concurrent sales on one
/// membership → correct final balance; refund caps; stamp carry-over across completion; QR nonce reuse.
/// </summary>
[Collection(ApiCollection.Name)]
public class TransactionFlowTests(ApiFixture fixture)
{
    private sealed record TokenOnly(string AccessToken, string RefreshToken, int ExpiresIn);
    private sealed record CreateMerchantResult(Guid MerchantId, string Code, Guid OwnerUserId, Guid DefaultBranchId);
    private sealed record AuthTokens(string AccessToken, string RefreshToken, int ExpiresIn, MerchantDto Merchant);
    private sealed record MerchantDto(Guid Id, string Code, string NameEn);
    private sealed record ProgramResult(Guid ProgramId, string Type, string Status, int? RuleVersion);
    private sealed record InviteResult(Guid StaffMemberId, string InviteToken);
    private sealed record SaleLoyalty(string Type, int StampsEarned, StampCard? StampCard, long PointsEarned, long NewBalance);
    private sealed record StampCard(int Filled, int Total, bool Completed, int Cycle);
    private sealed record SaleResult(Guid TransactionId, string Status, long AmountMinor, SaleLoyalty Loyalty);
    private sealed record RefundResult(Guid RefundId, string Type, long AmountMinor, ReversalResult LoyaltyReversal, string TransactionStatus);
    private sealed record ReversalResult(long PointsClawback, int StampsClawback);
    private sealed record QrResult(Guid CustomerId, string? FirstName, bool IsNewMember, QrMembership? Membership);
    private sealed record QrMembership(long PointsBalance, int StampsFilled, int CardSize, int CardCycle);
    private sealed record ErrorBody(ErrorEnvelope Error);
    private sealed record ErrorEnvelope(string Code, string Message);

    private static readonly object Device = new { platform = "web", model = (string?)null, fcmToken = (string?)null };

    private static void Bearer(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private sealed record Fixture(string CashierToken, Guid MerchantId, string MerchantCode, Guid BranchId);

    /// <summary>Merchant + active program (points or stamps) + a logged-in cashier.</summary>
    private static async Task<Fixture> SetupAsync(HttpClient client, string programType, object rules)
    {
        var ownerEmail = $"owner{Guid.NewGuid():N}@example.com";
        var admin = await (await client.PostAsJsonAsync("/v1/auth/admin/login", new
        {
            email = "admin@itaris.local", password = "dev-admin-pass-change-me", device = Device,
        })).Content.ReadFromJsonAsync<TokenOnly>();
        Bearer(client, admin!.AccessToken);
        var merchant = await (await client.PostAsJsonAsync("/v1/admin/merchants", new
        {
            nameAr = "مقهى", nameEn = "Tx Co", category = "cafe",
            owner = new { email = ownerEmail, password = "OwnerPass123!" },
        })).Content.ReadFromJsonAsync<CreateMerchantResult>();

        client.DefaultRequestHeaders.Authorization = null;
        var owner = await (await client.PostAsJsonAsync("/v1/auth/owner/login",
            new { email = ownerEmail, password = "OwnerPass123!", device = Device }))
            .Content.ReadFromJsonAsync<AuthTokens>();
        Bearer(client, owner!.AccessToken);

        var program = await (await client.PostAsJsonAsync("/v1/merchant/programs",
            new { type = programType, nameAr = "برنامج", nameEn = "Program" })).Content.ReadFromJsonAsync<ProgramResult>();
        await client.PatchAsJsonAsync($"/v1/merchant/programs/{program!.ProgramId}/rules", rules);
        await client.PostAsJsonAsync($"/v1/merchant/programs/{program.ProgramId}/activate", new { });

        var contact = $"cashier{Guid.NewGuid():N}@example.com";
        var invite = await (await client.PostAsJsonAsync("/v1/merchant/staff", new
        {
            displayName = "Omar", phoneOrEmail = contact, role = "cashier", branchId = (Guid?)null,
        })).Content.ReadFromJsonAsync<InviteResult>();
        client.DefaultRequestHeaders.Authorization = null;
        await client.PostAsJsonAsync("/v1/auth/staff/invites/accept",
            new { inviteToken = invite!.InviteToken, pin = "2468", device = Device });
        var cashier = await (await client.PostAsJsonAsync("/v1/auth/staff/login", new
        {
            merchantCode = merchant!.Code, phoneOrEmail = contact, pin = "2468", device = Device,
        })).Content.ReadFromJsonAsync<AuthTokens>();

        return new Fixture(cashier!.AccessToken, merchant.MerchantId, merchant.Code, merchant.DefaultBranchId);
    }

    private static async Task<Guid> NewCustomerAsync(HttpClient client)
    {
        var phone = "+96279" + Random.Shared.Next(1_000_000, 9_999_999);
        var challenge = await client.PostAsJsonAsync("/v1/auth/otp/request", new { phoneNumber = phone, purpose = "login" });
        var challengeId = (await challenge.Content.ReadFromJsonAsync<Dictionary<string, object>>())!["challengeId"].ToString();
        var verify = await client.PostAsJsonAsync("/v1/auth/otp/verify", new { challengeId, code = "000000", device = Device });
        var body = await verify.Content.ReadFromJsonAsync<Dictionary<string, System.Text.Json.JsonElement>>();
        return Guid.Parse(body!["customer"].GetProperty("id").GetString()!);
    }

    private static object PointsRules(decimal rate) => new
    {
        pointsPerJod = rate, rounding = "Floor", minAmountMinor = 0, welcomeBonus = 0,
        cardSize = 0, stampsPerVisit = 1, maxStampsPerVisit = 1, expiryMonths = (int?)null,
    };

    private static object StampsRules(int cardSize) => new
    {
        pointsPerJod = 0m, rounding = "Floor", minAmountMinor = 0, welcomeBonus = 0,
        cardSize, stampsPerVisit = 1, maxStampsPerVisit = 1, expiryMonths = (int?)null,
    };

    private static HttpRequestMessage Sale(Fixture f, Guid customerId, long amount, string idemKey, bool over = false)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/v1/pos/transactions")
        {
            Content = JsonContent.Create(new
            {
                customerId, branchId = f.BranchId, amountMinor = amount, currency = "JOD",
                occurredAt = (DateTimeOffset?)null, note = (string?)null, duplicateOverride = over,
            }),
        };
        req.Headers.Add("Idempotency-Key", idemKey);
        return req;
    }

    [Fact]
    public async Task Dod_record_4500_stamps_updates_the_card()
    {
        var client = fixture.CreateClient();
        var f = await SetupAsync(client, "stamps", StampsRules(9));
        var customerId = await NewCustomerAsync(client);

        Bearer(client, f.CashierToken);
        var resp = await client.SendAsync(Sale(f, customerId, 4500, Guid.NewGuid().ToString()));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var sale = await resp.Content.ReadFromJsonAsync<SaleResult>();
        Assert.Equal("stamps", sale!.Loyalty.Type);
        Assert.Equal(1, sale.Loyalty.StampsEarned);
        Assert.Equal(1, sale.Loyalty.StampCard!.Filled);
        Assert.Equal(9, sale.Loyalty.StampCard.Total);
    }

    [Fact]
    public async Task Same_idempotency_key_replays_the_original_response()
    {
        var client = fixture.CreateClient();
        var f = await SetupAsync(client, "points", PointsRules(1m));
        var customerId = await NewCustomerAsync(client);
        Bearer(client, f.CashierToken);

        var key = Guid.NewGuid().ToString();

        // Fire the same key twice in parallel — exactly one sale is recorded; both see the same tx.
        var responses = await Task.WhenAll(
            client.SendAsync(Sale(f, customerId, 5000, key)),
            client.SendAsync(Sale(f, customerId, 5000, key)));

        var sales = new List<SaleResult>();
        foreach (var r in responses)
        {
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            sales.Add((await r.Content.ReadFromJsonAsync<SaleResult>())!);
        }
        Assert.Equal(sales[0].TransactionId, sales[1].TransactionId);

        // The balance reflects ONE sale (5 points), not two.
        var list = await client.GetAsync($"/v1/pos/transactions?branchId={f.BranchId}");
        var body = await list.Content.ReadFromJsonAsync<Dictionary<string, List<Dictionary<string, object>>>>();
        Assert.Single(body!["items"]);
    }

    [Fact]
    public async Task Same_key_different_payload_is_a_conflict()
    {
        var client = fixture.CreateClient();
        var f = await SetupAsync(client, "points", PointsRules(1m));
        var customerId = await NewCustomerAsync(client);
        Bearer(client, f.CashierToken);

        var key = Guid.NewGuid().ToString();
        var first = await client.SendAsync(Sale(f, customerId, 5000, key));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.SendAsync(Sale(f, customerId, 9999, key)); // different amount, same key
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var err = await second.Content.ReadFromJsonAsync<ErrorBody>();
        Assert.Equal("IDEMPOTENCY_CONFLICT", err!.Error.Code);
    }

    [Fact]
    public async Task Twenty_concurrent_sales_on_one_membership_produce_the_correct_balance()
    {
        var client = fixture.CreateClient();
        var f = await SetupAsync(client, "points", PointsRules(1m));
        var customerId = await NewCustomerAsync(client);
        Bearer(client, f.CashierToken);

        // Establish the membership first (doc 06: "on ONE membership"), then hammer it concurrently.
        await client.SendAsync(Sale(f, customerId, 1000, Guid.NewGuid().ToString()));

        // 20 distinct sales of 1.000 JOD each (distinct idempotency keys) fired concurrently.
        var tasks = Enumerable.Range(0, 20)
            .Select(_ => client.SendAsync(Sale(f, customerId, 1000, Guid.NewGuid().ToString(), over: true)))
            .ToArray();
        var responses = await Task.WhenAll(tasks);
        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));

        // Final balance must be exactly 21 points (1 warm-up + 20) — no lost updates under the row lock.
        var max = long.MinValue;
        foreach (var r in responses)
        {
            var s = await r.Content.ReadFromJsonAsync<SaleResult>();
            max = Math.Max(max, s!.Loyalty.NewBalance);
        }
        Assert.Equal(21, max);
    }

    [Fact]
    public async Task Full_refund_claws_back_points_and_partial_refunds_cap_at_remaining()
    {
        var client = fixture.CreateClient();
        var f = await SetupAsync(client, "points", PointsRules(1m));
        var customerId = await NewCustomerAsync(client);
        Bearer(client, f.CashierToken);

        var sale = await (await client.SendAsync(Sale(f, customerId, 10_000, Guid.NewGuid().ToString())))
            .Content.ReadFromJsonAsync<SaleResult>(); // 10 points

        // Partial refund of 4.000 JOD → 4 points clawed back.
        var r1 = new HttpRequestMessage(HttpMethod.Post, $"/v1/pos/transactions/{sale!.TransactionId}/refunds")
        { Content = JsonContent.Create(new { type = "partial", amountMinor = 4000, reason = "wrong item" }) };
        r1.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var refund1 = await client.SendAsync(r1);
        Assert.Equal(HttpStatusCode.OK, refund1.StatusCode);
        var body1 = await refund1.Content.ReadFromJsonAsync<RefundResult>();
        Assert.Equal(4, body1!.LoyaltyReversal.PointsClawback);
        Assert.Equal("partially_refunded", body1.TransactionStatus);

        // A second partial refund exceeding the remaining 6.000 JOD is rejected.
        var r2 = new HttpRequestMessage(HttpMethod.Post, $"/v1/pos/transactions/{sale.TransactionId}/refunds")
        { Content = JsonContent.Create(new { type = "partial", amountMinor = 7000, reason = "too much" }) };
        r2.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var refund2 = await client.SendAsync(r2);
        Assert.Equal(HttpStatusCode.Conflict, refund2.StatusCode);
        var err = await refund2.Content.ReadFromJsonAsync<ErrorBody>();
        Assert.Equal("REFUND_EXCEEDS_REMAINING", err!.Error.Code);
    }

    [Fact]
    public async Task Stamps_carry_over_when_a_sale_crosses_card_completion()
    {
        var client = fixture.CreateClient();
        var f = await SetupAsync(client, "stamps", StampsRules(3)); // small card
        var customerId = await NewCustomerAsync(client);
        Bearer(client, f.CashierToken);

        // Three sales fill the card; the third completes it (filled resets, cycle increments).
        SaleResult? third = null;
        for (var i = 0; i < 3; i++)
        {
            var r = await client.SendAsync(Sale(f, customerId, 2000, Guid.NewGuid().ToString(), over: true));
            third = await r.Content.ReadFromJsonAsync<SaleResult>();
        }

        Assert.True(third!.Loyalty.StampCard!.Completed);
        Assert.Equal(1, third.Loyalty.StampCard.Cycle);
        Assert.Equal(0, third.Loyalty.StampCard.Filled); // carried over to a fresh card
    }

    [Fact]
    public async Task Qr_resolve_works_once_then_the_nonce_is_rejected()
    {
        var client = fixture.CreateClient();
        var f = await SetupAsync(client, "points", PointsRules(1m));

        // A customer gets their QR token.
        var phone = "+96279" + Random.Shared.Next(1_000_000, 9_999_999);
        var challenge = await client.PostAsJsonAsync("/v1/auth/otp/request", new { phoneNumber = phone, purpose = "login" });
        var challengeId = (await challenge.Content.ReadFromJsonAsync<Dictionary<string, object>>())!["challengeId"].ToString();
        var verify = await client.PostAsJsonAsync("/v1/auth/otp/verify", new { challengeId, code = "000000", device = Device });
        var customerToken = (await verify.Content.ReadFromJsonAsync<TokenOnly>())!.AccessToken;
        Bearer(client, customerToken);
        var qr = await (await client.GetAsync("/v1/customers/me/qr-token"))
            .Content.ReadFromJsonAsync<Dictionary<string, System.Text.Json.JsonElement>>();
        var qrPayload = qr!["qrPayload"].GetString()!;

        // The cashier resolves it once — success.
        Bearer(client, f.CashierToken);
        var first = await client.PostAsJsonAsync("/v1/pos/customers/resolve-qr", new { qrPayload, branchId = f.BranchId });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Resolving the same QR again → QR_ALREADY_USED (single-use nonce).
        var second = await client.PostAsJsonAsync("/v1/pos/customers/resolve-qr", new { qrPayload, branchId = f.BranchId });
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        var err = await second.Content.ReadFromJsonAsync<ErrorBody>();
        Assert.Equal("QR_ALREADY_USED", err!.Error.Code);
    }
}
