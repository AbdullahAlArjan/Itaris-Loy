using Itaris.Infrastructure.Auth;
using Itaris.Modules.Identity.Domain;
using Itaris.Modules.Identity.Persistence;
using Itaris.Modules.Identity.PublicApi;
using Itaris.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Itaris.Modules.Identity.Features.Shared;

/// <summary>Implements <see cref="IUserDirectory"/> over identity.users.</summary>
public sealed class UserDirectory(IdentityDbContext db, ISecretHasher hasher, IClock clock) : IUserDirectory
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutWindow = TimeSpan.FromMinutes(15);

    public async Task<Guid> CreateOwnerAsync(string email, string password, CancellationToken cancellationToken)
    {
        var user = new User
        {
            UserType = UserTypes.Owner,
            Email = email,
            PasswordHash = hasher.Hash(password),
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return user.Id;
    }

    public async Task<Guid> CreateStaffUserAsync(string? phoneOrEmail, CancellationToken cancellationToken)
    {
        var isEmail = phoneOrEmail?.Contains('@') == true;
        var user = new User
        {
            UserType = UserTypes.Staff,
            Email = isEmail ? phoneOrEmail : null,
            PhoneNumber = isEmail ? null : phoneOrEmail,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return user.Id;
    }

    public async Task<Guid> EnsureCustomerByPhoneAsync(string phoneNumber, CancellationToken cancellationToken)
    {
        var existing = await db.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber, cancellationToken);
        if (existing is not null)
        {
            return existing.Id;
        }

        var user = new User { UserType = UserTypes.Customer, PhoneNumber = phoneNumber };
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return user.Id;
    }

    public Task<string?> GetPhoneAsync(Guid userId, CancellationToken cancellationToken) =>
        db.Users.Where(u => u.Id == userId).Select(u => u.PhoneNumber).FirstOrDefaultAsync(cancellationToken);

    public Task<OwnerVerifyResult> VerifyOwnerAsync(string email, string password, CancellationToken cancellationToken) =>
        VerifyByTypeAsync(email, password, UserTypes.Owner, cancellationToken);

    public Task<OwnerVerifyResult> VerifyAdminAsync(string email, string password, CancellationToken cancellationToken) =>
        VerifyByTypeAsync(email, password, UserTypes.PlatformAdmin, cancellationToken);

    public async Task<Guid> EnsureAdminAsync(string email, string password, CancellationToken cancellationToken)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var existing = await db.Users.FirstOrDefaultAsync(
            u => u.Email == normalized && u.UserType == UserTypes.PlatformAdmin, cancellationToken);
        if (existing is not null)
        {
            return existing.Id;
        }

        var admin = new User
        {
            UserType = UserTypes.PlatformAdmin,
            Email = normalized,
            PasswordHash = hasher.Hash(password),
        };
        db.Users.Add(admin);
        await db.SaveChangesAsync(cancellationToken);
        return admin.Id;
    }

    private async Task<OwnerVerifyResult> VerifyByTypeAsync(
        string email, string password, string userType, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(
            u => u.Email == email && u.UserType == userType, cancellationToken);

        if (user is null)
        {
            return new OwnerVerifyResult(OwnerVerifyStatus.Invalid, null);
        }

        if (user.LockedUntil is { } lockedUntil && lockedUntil > clock.UtcNow)
        {
            return new OwnerVerifyResult(OwnerVerifyStatus.Locked, null);
        }

        if (user.PasswordHash is null || !hasher.Verify(user.PasswordHash, password))
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= MaxFailedAttempts)
            {
                user.LockedUntil = clock.UtcNow.Add(LockoutWindow);
                user.FailedLoginAttempts = 0;
                await db.SaveChangesAsync(cancellationToken);
                return new OwnerVerifyResult(OwnerVerifyStatus.Locked, null);
            }

            await db.SaveChangesAsync(cancellationToken);
            return new OwnerVerifyResult(OwnerVerifyStatus.Invalid, null);
        }

        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        await db.SaveChangesAsync(cancellationToken);
        return new OwnerVerifyResult(OwnerVerifyStatus.Ok, user.Id);
    }
}
