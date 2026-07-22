using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Itaris.Tests.Integration;

/// <summary>
/// Customers module: the phone-only (shadow) flow — a cashier enrolls a customer by phone, and when
/// that person later registers with the same phone their profile is claimed. Plus profile + QR + lookup.
/// </summary>
[Collection(ApiCollection.Name)]
public class CustomerFlowTests(ApiFixture fixture)
{
    private sealed record TokenOnly(string AccessToken, string RefreshToken, int ExpiresIn);
    private sealed record CreateMerchantResult(Guid MerchantId, string Code, Guid OwnerUserId);
    private sealed record AuthTokens(string AccessToken, string RefreshToken, int ExpiresIn, MerchantDto Merchant);
    private sealed record MerchantDto(Guid Id, string Code, string NameEn);
    private sealed record InviteResult(Guid StaffMemberId, string InviteToken);
    private sealed record EnrollResult(Guid CustomerId, Guid ProfileId, bool IsNewCustomer);
    private sealed record ProfileDto(Guid Id, string? FirstName, string PhoneNumber, DateOnly? BirthDate, string? Gender, string PreferredLanguage, DateTimeOffset JoinedAt);
    private sealed record QrDto(string QrPayload, int ExpiresInSeconds);

    private static readonly object Device = new { platform = "web", model = (string?)null, fcmToken = (string?)null };

    private static void Bearer(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    /// <summary>Owner + a cashier who can identify customers, returns the cashier's staff token.</summary>
    private static async Task<string> CashierTokenAsync(HttpClient client)
    {
        var ownerEmail = $"owner{Guid.NewGuid():N}@example.com";
        var adminLogin = await client.PostAsJsonAsync("/v1/auth/admin/login", new
        {
            email = "admin@itaris.local", password = "dev-admin-pass-change-me", device = Device,
        });
        var admin = await adminLogin.Content.ReadFromJsonAsync<TokenOnly>();
        Bearer(client, admin!.AccessToken);
        var created = await (await client.PostAsJsonAsync("/v1/admin/merchants", new
        {
            nameAr = "مقهى", nameEn = "Cust Co", category = "cafe",
            owner = new { email = ownerEmail, password = "OwnerPass123!" },
        })).Content.ReadFromJsonAsync<CreateMerchantResult>();

        client.DefaultRequestHeaders.Authorization = null;
        var owner = await (await client.PostAsJsonAsync("/v1/auth/owner/login",
            new { email = ownerEmail, password = "OwnerPass123!", device = Device }))
            .Content.ReadFromJsonAsync<AuthTokens>();

        Bearer(client, owner!.AccessToken);
        var contact = $"cashier{Guid.NewGuid():N}@example.com";
        var invite = await (await client.PostAsJsonAsync("/v1/merchant/staff", new
        {
            displayName = "Omar", phoneOrEmail = contact, role = "cashier", branchId = (Guid?)null,
        })).Content.ReadFromJsonAsync<InviteResult>();

        client.DefaultRequestHeaders.Authorization = null;
        await client.PostAsJsonAsync("/v1/auth/staff/invites/accept",
            new { inviteToken = invite!.InviteToken, pin = "2468", device = Device });
        var staff = await (await client.PostAsJsonAsync("/v1/auth/staff/login", new
        {
            merchantCode = created!.Code, phoneOrEmail = contact, pin = "2468", device = Device,
        })).Content.ReadFromJsonAsync<AuthTokens>();
        return staff!.AccessToken;
    }

    [Fact]
    public async Task Cashier_enrolls_a_phone_only_customer_who_later_claims_it_on_registration()
    {
        var client = fixture.CreateClient();
        var phone = "+96279" + Random.Shared.Next(1_000_000, 9_999_999);

        // Cashier enrolls the phone (customer never installed the app).
        Bearer(client, await CashierTokenAsync(client));
        var enrollResp = await client.PostAsJsonAsync("/v1/pos/customers/enroll", new { phoneNumber = phone });
        Assert.Equal(HttpStatusCode.OK, enrollResp.StatusCode);
        var enroll = await enrollResp.Content.ReadFromJsonAsync<EnrollResult>();
        Assert.True(enroll!.IsNewCustomer);

        // Enrolling the same phone again is idempotent (same customer).
        var again = await (await client.PostAsJsonAsync("/v1/pos/customers/enroll", new { phoneNumber = phone }))
            .Content.ReadFromJsonAsync<EnrollResult>();
        Assert.False(again!.IsNewCustomer);
        Assert.Equal(enroll.CustomerId, again.CustomerId);

        // The same person later registers via OTP with that phone → same identity user.
        client.DefaultRequestHeaders.Authorization = null;
        var challenge = await client.PostAsJsonAsync("/v1/auth/otp/request", new { phoneNumber = phone, purpose = "login" });
        var challengeId = (await challenge.Content.ReadFromJsonAsync<Dictionary<string, object>>())!["challengeId"].ToString();
        var verify = await client.PostAsJsonAsync("/v1/auth/otp/verify", new { challengeId, code = "000000", device = Device });
        var customer = await verify.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.False((bool)((System.Text.Json.JsonElement)customer!["isNewUser"]).GetBoolean());
        var customerToken = ((System.Text.Json.JsonElement)customer["accessToken"]).GetString()!;

        // Fetching the profile claims the shadow (is_shadow → false) and returns the same phone.
        Bearer(client, customerToken);
        var profileResp = await client.GetAsync("/v1/customers/me");
        Assert.Equal(HttpStatusCode.OK, profileResp.StatusCode);
        var profile = await profileResp.Content.ReadFromJsonAsync<ProfileDto>();
        Assert.Equal(phone, profile!.PhoneNumber);
    }

    [Fact]
    public async Task Customer_can_update_profile_and_get_a_qr_token()
    {
        var client = fixture.CreateClient();
        var phone = "+96279" + Random.Shared.Next(1_000_000, 9_999_999);
        var challenge = await client.PostAsJsonAsync("/v1/auth/otp/request", new { phoneNumber = phone, purpose = "login" });
        var challengeId = (await challenge.Content.ReadFromJsonAsync<Dictionary<string, object>>())!["challengeId"].ToString();
        var verify = await client.PostAsJsonAsync("/v1/auth/otp/verify", new { challengeId, code = "000000", device = Device });
        var token = (await verify.Content.ReadFromJsonAsync<TokenOnly>())!.AccessToken;
        Bearer(client, token);

        var update = await client.PatchAsJsonAsync("/v1/customers/me", new { firstName = "Layla", preferredLanguage = "ar" });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var profile = await update.Content.ReadFromJsonAsync<ProfileDto>();
        Assert.Equal("Layla", profile!.FirstName);

        var qr = await client.GetAsync("/v1/customers/me/qr-token");
        var body = await qr.Content.ReadFromJsonAsync<QrDto>();
        Assert.Equal(60, body!.ExpiresInSeconds);
        Assert.False(string.IsNullOrEmpty(body.QrPayload));
    }

    [Fact]
    public async Task Cashier_lookup_returns_masked_matches()
    {
        var client = fixture.CreateClient();
        var phone = "+96279" + Random.Shared.Next(1_000_000, 9_999_999);
        var cashier = await CashierTokenAsync(client);

        Bearer(client, cashier);
        await client.PostAsJsonAsync("/v1/pos/customers/enroll", new { phoneNumber = phone });

        var last7 = phone[^7..];
        var lookup = await client.GetAsync($"/v1/pos/customers/lookup?phone={last7}");
        Assert.Equal(HttpStatusCode.OK, lookup.StatusCode);
        var body = await lookup.Content.ReadFromJsonAsync<Dictionary<string, List<Dictionary<string, object>>>>();
        var matches = body!["matches"];
        Assert.NotEmpty(matches);
        var masked = ((System.Text.Json.JsonElement)matches[0]["phoneMasked"]).GetString()!;
        Assert.Contains("•", masked);
        Assert.DoesNotContain(phone[3..9], masked); // middle digits are hidden
    }

    [Fact]
    public async Task Enroll_requires_the_identify_permission()
    {
        var client = fixture.CreateClient();
        var resp = await client.PostAsJsonAsync("/v1/pos/customers/enroll", new { phoneNumber = "+962790000001" });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}
