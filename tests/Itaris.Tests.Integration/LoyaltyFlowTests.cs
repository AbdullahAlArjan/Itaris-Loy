using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Itaris.Tests.Integration;

/// <summary>
/// Phase 3 Definition of Done (doc 06): create program → activate → join customer → preview
/// 4.500 JOD returns the correct value in both points and stamps modes.
/// </summary>
[Collection(ApiCollection.Name)]
public class LoyaltyFlowTests(ApiFixture fixture)
{
    private sealed record TokenOnly(string AccessToken, string RefreshToken, int ExpiresIn);
    private sealed record CreateMerchantResult(Guid MerchantId, string Code, Guid OwnerUserId);
    private sealed record AuthTokens(string AccessToken, string RefreshToken, int ExpiresIn, MerchantDto Merchant);
    private sealed record MerchantDto(Guid Id, string Code, string NameEn);
    private sealed record ProgramResult(Guid ProgramId, string Type, string Status, int? RuleVersion);
    private sealed record PreviewResult(long PointsEarned, int StampsEarned);
    private sealed record MembershipResult(Guid MembershipId, Guid MerchantId, Guid ProgramId, string ProgramType, long PointsBalance);
    private sealed record ErrorBody(ErrorEnvelope Error);
    private sealed record ErrorEnvelope(string Code, string Message);

    private static readonly object Device = new { platform = "web", model = (string?)null, fcmToken = (string?)null };

    private static void Bearer(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private static async Task<string> OwnerForNewMerchantAsync(HttpClient client)
    {
        var ownerEmail = $"owner{Guid.NewGuid():N}@example.com";
        var adminLogin = await client.PostAsJsonAsync("/v1/auth/admin/login", new
        {
            email = "admin@itaris.local", password = "dev-admin-pass-change-me", device = Device,
        });
        var admin = await adminLogin.Content.ReadFromJsonAsync<TokenOnly>();
        Bearer(client, admin!.AccessToken);
        await client.PostAsJsonAsync("/v1/admin/merchants", new
        {
            nameAr = "مقهى", nameEn = "Loyalty Co", category = "cafe",
            owner = new { email = ownerEmail, password = "OwnerPass123!" },
        });

        client.DefaultRequestHeaders.Authorization = null;
        var ownerLogin = await client.PostAsJsonAsync("/v1/auth/owner/login",
            new { email = ownerEmail, password = "OwnerPass123!", device = Device });
        var owner = await ownerLogin.Content.ReadFromJsonAsync<AuthTokens>();
        Bearer(client, owner!.AccessToken);
        return owner.AccessToken;
    }

    private static async Task<ProgramResult> CreateActiveProgramAsync(
        HttpClient client, string type, object rules)
    {
        var create = await client.PostAsJsonAsync("/v1/merchant/programs",
            new { type, nameAr = "برنامج", nameEn = "Program" });
        var program = await create.Content.ReadFromJsonAsync<ProgramResult>();

        await client.PatchAsJsonAsync($"/v1/merchant/programs/{program!.ProgramId}/rules", rules);
        var activate = await client.PostAsJsonAsync($"/v1/merchant/programs/{program.ProgramId}/activate", new { });
        activate.EnsureSuccessStatusCode();
        return (await activate.Content.ReadFromJsonAsync<ProgramResult>())!;
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

    [Fact]
    public async Task Points_program_previews_4500_fils_as_4_points_and_customer_can_join()
    {
        var client = fixture.CreateClient();
        await OwnerForNewMerchantAsync(client);

        var program = await CreateActiveProgramAsync(client, "points", PointsRules(1m));
        Assert.Equal("active", program.Status);

        // E4 preview: 4.500 JOD at 1 point/JOD floored → 4 points
        var previewResp = await client.PostAsJsonAsync("/v1/loyalty/preview",
            new { programId = program.ProgramId, amountMinor = 4500 });
        Assert.Equal(HttpStatusCode.OK, previewResp.StatusCode);
        var preview = await previewResp.Content.ReadFromJsonAsync<PreviewResult>();
        Assert.Equal(4, preview!.PointsEarned);
        Assert.Equal(0, preview.StampsEarned);
    }

    [Fact]
    public async Task Stamps_program_previews_4500_fils_as_1_stamp()
    {
        var client = fixture.CreateClient();
        await OwnerForNewMerchantAsync(client);

        var program = await CreateActiveProgramAsync(client, "stamps", StampsRules(9));

        var previewResp = await client.PostAsJsonAsync("/v1/loyalty/preview",
            new { programId = program.ProgramId, amountMinor = 4500 });
        var preview = await previewResp.Content.ReadFromJsonAsync<PreviewResult>();
        Assert.Equal(0, preview!.PointsEarned);
        Assert.Equal(1, preview.StampsEarned);
    }

    [Fact]
    public async Task Customer_can_join_the_active_program_and_see_the_membership()
    {
        var client = fixture.CreateClient();

        // Owner sets up an active points program and we capture the merchant id from owner login.
        var ownerEmail = $"owner{Guid.NewGuid():N}@example.com";
        var adminLogin = await client.PostAsJsonAsync("/v1/auth/admin/login", new
        {
            email = "admin@itaris.local", password = "dev-admin-pass-change-me", device = Device,
        });
        var admin = await adminLogin.Content.ReadFromJsonAsync<TokenOnly>();
        Bearer(client, admin!.AccessToken);
        var createMerchant = await client.PostAsJsonAsync("/v1/admin/merchants", new
        {
            nameAr = "مقهى", nameEn = "Join Co", category = "cafe",
            owner = new { email = ownerEmail, password = "OwnerPass123!" },
        });
        var merchant = await createMerchant.Content.ReadFromJsonAsync<CreateMerchantResult>();

        client.DefaultRequestHeaders.Authorization = null;
        var ownerLogin = await client.PostAsJsonAsync("/v1/auth/owner/login",
            new { email = ownerEmail, password = "OwnerPass123!", device = Device });
        var owner = await ownerLogin.Content.ReadFromJsonAsync<AuthTokens>();
        Bearer(client, owner!.AccessToken);
        await CreateActiveProgramAsync(client, "points", PointsRules(2m));

        // Customer logs in via OTP (dev code) and joins the merchant.
        client.DefaultRequestHeaders.Authorization = null;
        var phone = "+96279" + Random.Shared.Next(1_000_000, 9_999_999);
        var challenge = await client.PostAsJsonAsync("/v1/auth/otp/request", new { phoneNumber = phone, purpose = "login" });
        var challengeId = (await challenge.Content.ReadFromJsonAsync<Dictionary<string, object>>())!["challengeId"].ToString();
        var verify = await client.PostAsJsonAsync("/v1/auth/otp/verify", new
        {
            challengeId, code = "000000", device = Device,
        });
        var customerToken = (await verify.Content.ReadFromJsonAsync<TokenOnly>())!.AccessToken;

        Bearer(client, customerToken);
        var joinResp = await client.PostAsJsonAsync("/v1/customers/me/memberships", new { merchantId = merchant!.MerchantId });
        Assert.Equal(HttpStatusCode.OK, joinResp.StatusCode);
        var membership = await joinResp.Content.ReadFromJsonAsync<MembershipResult>();
        Assert.Equal(merchant.MerchantId, membership!.MerchantId);
        Assert.Equal("points", membership.ProgramType);

        // Re-join is idempotent, and the membership shows up in the list.
        var rejoin = await client.PostAsJsonAsync("/v1/customers/me/memberships", new { merchantId = merchant.MerchantId });
        Assert.Equal(HttpStatusCode.OK, rejoin.StatusCode);

        var list = await client.GetAsync("/v1/customers/me/memberships");
        var body = await list.Content.ReadFromJsonAsync<Dictionary<string, List<MembershipResult>>>();
        Assert.Single(body!["items"]);
    }

    [Fact]
    public async Task Second_active_program_is_rejected_with_program_limit()
    {
        var client = fixture.CreateClient();
        await OwnerForNewMerchantAsync(client);
        await CreateActiveProgramAsync(client, "points", PointsRules(1m));

        // A second program can be created and given rules, but activating it must fail.
        var create = await client.PostAsJsonAsync("/v1/merchant/programs",
            new { type = "stamps", nameAr = "ثاني", nameEn = "Second" });
        var second = await create.Content.ReadFromJsonAsync<ProgramResult>();
        await client.PatchAsJsonAsync($"/v1/merchant/programs/{second!.ProgramId}/rules", StampsRules(9));

        var activate = await client.PostAsJsonAsync($"/v1/merchant/programs/{second.ProgramId}/activate", new { });
        Assert.Equal(HttpStatusCode.Conflict, activate.StatusCode);
        var err = await activate.Content.ReadFromJsonAsync<ErrorBody>();
        Assert.Equal("PROGRAM_LIMIT", err!.Error.Code);
    }

    [Fact]
    public async Task Joining_a_merchant_with_no_active_program_returns_program_inactive()
    {
        var client = fixture.CreateClient();

        // Create a merchant but never activate a program.
        var adminLogin = await client.PostAsJsonAsync("/v1/auth/admin/login", new
        {
            email = "admin@itaris.local", password = "dev-admin-pass-change-me", device = Device,
        });
        var admin = await adminLogin.Content.ReadFromJsonAsync<TokenOnly>();
        Bearer(client, admin!.AccessToken);
        var createMerchant = await client.PostAsJsonAsync("/v1/admin/merchants", new
        {
            nameAr = "مقهى", nameEn = "No Program Co", category = "cafe",
            owner = new { email = $"owner{Guid.NewGuid():N}@example.com", password = "OwnerPass123!" },
        });
        var merchant = await createMerchant.Content.ReadFromJsonAsync<CreateMerchantResult>();
        client.DefaultRequestHeaders.Authorization = null;

        var phone = "+96279" + Random.Shared.Next(1_000_000, 9_999_999);
        var challenge = await client.PostAsJsonAsync("/v1/auth/otp/request", new { phoneNumber = phone, purpose = "login" });
        var challengeId = (await challenge.Content.ReadFromJsonAsync<Dictionary<string, object>>())!["challengeId"].ToString();
        var verify = await client.PostAsJsonAsync("/v1/auth/otp/verify", new { challengeId, code = "000000", device = Device });
        var customerToken = (await verify.Content.ReadFromJsonAsync<TokenOnly>())!.AccessToken;

        Bearer(client, customerToken);
        var joinResp = await client.PostAsJsonAsync("/v1/customers/me/memberships", new { merchantId = merchant!.MerchantId });
        Assert.Equal(HttpStatusCode.BadRequest, joinResp.StatusCode);
        var err = await joinResp.Content.ReadFromJsonAsync<ErrorBody>();
        Assert.Equal("PROGRAM_INACTIVE", err!.Error.Code);
    }
}
