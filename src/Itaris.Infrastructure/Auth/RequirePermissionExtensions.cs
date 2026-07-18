using Microsoft.AspNetCore.Builder;

namespace Itaris.Infrastructure.Auth;

public static class RequirePermissionExtensions
{
    /// <summary>Requires the caller's token to carry the given permission string (doc 01 Part 3).</summary>
    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, string permission)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.RequireAuthorization($"{PermissionRequirement.PolicyPrefix}{permission}");
        return builder;
    }
}
