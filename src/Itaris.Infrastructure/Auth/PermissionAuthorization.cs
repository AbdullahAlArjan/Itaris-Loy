using Microsoft.AspNetCore.Authorization;

namespace Itaris.Infrastructure.Auth;

/// <summary>
/// Permission-string authorization (doc 01 Part 3: check permissions, never role names).
/// A policy named "perm:{permission}" is satisfied when the token carries that permission claim.
/// </summary>
public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;

    public const string PolicyPrefix = "perm:";
}

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var hasPermission = context.User
            .FindAll(ItarisClaims.Permission)
            .Any(c => c.Value == requirement.Permission);

        if (hasPermission)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
