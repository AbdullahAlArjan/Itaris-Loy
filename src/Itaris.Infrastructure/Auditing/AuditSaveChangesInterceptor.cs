using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Itaris.Infrastructure.Auth;
using Itaris.SharedKernel;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Itaris.Infrastructure.Auditing;

/// <summary>
/// doc 04: the audit interceptor. Captures every insert/update/delete of an <see cref="Entity"/>
/// performed while a staff or admin is authenticated, then persists ops.audit_logs rows after the
/// business save commits — synchronously within the request, before the response is sent.
///
/// Registered as a <b>singleton</b>: EF Core caches interceptor instances across scopes, so this
/// must not hold scoped state. It reads the current principal live from <see cref="IHttpContextAccessor"/>
/// and buffers pending entries in HttpContext.Items (per request). The write goes through a fresh DI
/// scope, decoupled from the intercepted context's transaction, and is best-effort: a failure is
/// logged, never propagated, since the business change has already committed (doc 01: audit is a
/// fraud control, not a gate). Customer and anonymous mutations (e.g. OTP login) are not audited.
/// </summary>
public sealed class AuditSaveChangesInterceptor(
    IHttpContextAccessor httpContextAccessor,
    IServiceScopeFactory scopeFactory,
    ILogger<AuditSaveChangesInterceptor> logger) : SaveChangesInterceptor
{
    private const string PendingItemsKey = "__itaris_audit_pending";
    private static readonly string[] AuditedAudiences = ["staff", "admin"];

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        Capture(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Capture(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        FlushAsync().GetAwaiter().GetResult();
        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        await FlushAsync();
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private void Capture(DbContext? context)
    {
        var http = httpContextAccessor.HttpContext;
        if (context is null || http is null)
        {
            return;
        }

        var principal = http.User;
        var audience = principal.FindFirstValue(ItarisClaims.Audience);
        if (audience is null || !AuditedAudiences.Contains(audience))
        {
            return;
        }

        var actorUserId = Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var uid) ? uid : (Guid?)null;
        var merchantId = Guid.TryParse(principal.FindFirstValue(ItarisClaims.MerchantId), out var mid) ? mid : (Guid?)null;

        var pending = http.Items[PendingItemsKey] as List<AuditEntry> ?? [];
        foreach (var entry in context.ChangeTracker.Entries<Entity>())
        {
            var action = entry.State switch
            {
                EntityState.Added => "insert",
                EntityState.Modified => "update",
                EntityState.Deleted => "delete",
                _ => null,
            };
            if (action is null)
            {
                continue;
            }

            pending.Add(new AuditEntry(
                MerchantId: merchantId,
                ActorUserId: actorUserId,
                ActorType: audience,
                EntityType: entry.Entity.GetType().Name,
                EntityId: entry.Entity.Id.ToString(),
                Action: action,
                AfterSummaryJson: SummarizeChangedColumns(entry)));
        }

        http.Items[PendingItemsKey] = pending;
    }

    private async Task FlushAsync()
    {
        var http = httpContextAccessor.HttpContext;
        if (http?.Items[PendingItemsKey] is not List<AuditEntry> { Count: > 0 } pending)
        {
            return;
        }

        http.Items[PendingItemsKey] = null;

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var sink = scope.ServiceProvider.GetRequiredService<IAuditSink>();
            await sink.WriteAsync(pending, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to persist {Count} audit entries", pending.Count);
        }
    }

    /// <summary>Compact list of the columns that changed (names only — never audit secret values).</summary>
    private static string SummarizeChangedColumns(EntityEntry entry)
    {
        var columns = entry.State == EntityState.Modified
            ? entry.Properties.Where(p => p.IsModified).Select(p => p.Metadata.Name)
            : entry.Properties.Select(p => p.Metadata.Name);

        var filtered = columns.Where(name =>
            !name.Contains("Hash", StringComparison.OrdinalIgnoreCase) &&
            !name.Contains("Password", StringComparison.OrdinalIgnoreCase) &&
            !name.Contains("Pin", StringComparison.OrdinalIgnoreCase));

        return System.Text.Json.JsonSerializer.Serialize(new { columns = filtered });
    }
}
