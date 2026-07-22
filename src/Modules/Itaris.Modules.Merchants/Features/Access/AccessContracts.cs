using Itaris.Modules.Identity.PublicApi;

namespace Itaris.Modules.Merchants.Features.Access;

// doc 05 C10 — admin creates a merchant + its owner account.
public sealed record CreateMerchantRequest(
    string NameAr, string NameEn, string Category, OwnerAccount Owner);

public sealed record OwnerAccount(string Email, string Password);

public sealed record CreateMerchantResponse(Guid MerchantId, string Code, Guid OwnerUserId, Guid DefaultBranchId);

// doc 05 A6 — owner email/password login.
public sealed record OwnerLoginRequest(string Email, string Password, DeviceRegistration Device);

public sealed record MerchantSummary(Guid Id, string Code, string NameEn);

public sealed record AuthTokensResponse(
    string AccessToken, string RefreshToken, int ExpiresIn, MerchantSummary Merchant);

// doc 05 C7 — owner/admin invites a staff member.
public sealed record InviteStaffRequest(
    string DisplayName, string PhoneOrEmail, string Role, Guid? BranchId);

public sealed record InviteStaffResponse(Guid StaffMemberId, string InviteToken);

// doc 05 A8 — invited staff accepts and sets a PIN.
public sealed record AcceptInviteRequest(string InviteToken, string Pin, DeviceRegistration Device);

// doc 05 A7 — staff PIN login.
public sealed record StaffLoginRequest(
    string MerchantCode, string PhoneOrEmail, string Pin, DeviceRegistration Device);
