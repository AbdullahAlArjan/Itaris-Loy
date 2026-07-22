using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Itaris.SharedKernel;
using Microsoft.AspNetCore.Http;

namespace Itaris.Infrastructure.Auth;

/// <summary>
/// <see cref="ICurrentUser"/> projected from the JWT claims on the current request.
/// Anonymous requests yield a null Id and empty collections.
/// </summary>
public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public Guid? Id =>
        Guid.TryParse(Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : null;

    public string? Audience => Principal?.FindFirstValue(ItarisClaims.Audience);

    public Guid? MerchantId =>
        Guid.TryParse(Principal?.FindFirstValue(ItarisClaims.MerchantId), out var id) ? id : null;

    public Guid? StaffId =>
        Guid.TryParse(Principal?.FindFirstValue(ItarisClaims.StaffId), out var id) ? id : null;

    public IReadOnlyList<Guid> BranchIds =>
        Principal?.FindAll(ItarisClaims.BranchId)
            .Select(c => Guid.TryParse(c.Value, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .ToArray() ?? [];

    public IReadOnlyList<string> Permissions =>
        Principal?.FindAll(ItarisClaims.Permission).Select(c => c.Value).ToArray() ?? [];
}
