using Itaris.Modules.Customers.Domain;
using Itaris.Modules.Customers.Persistence;
using Itaris.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Itaris.Modules.Customers.Features.Deletion;

public sealed record DeletionRequestResponse(string Status, DateTimeOffset RequestedAt, DateTimeOffset ExecuteAfter);

/// <summary>
/// doc 05 B13 — PDPL account deletion (doc 01: in the MVP). A request opens a 7-day grace window
/// the customer can cancel; a purge job executes it afterwards. doc 05 also requires a fresh OTP
/// (step-up) — noted for the auth layer; not yet enforced here.
/// Errors: ALREADY_REPORTED (a pending request exists), NOT_FOUND (nothing to cancel).
/// </summary>
public sealed class DeletionHandler(CustomersDbContext db, IClock clock)
{
    private const int GraceDays = 7;

    public async Task<Result<DeletionRequestResponse>> RequestAsync(Guid userId, CancellationToken cancellationToken)
    {
        var existing = await db.DeletionRequests
            .FirstOrDefaultAsync(d => d.UserId == userId && d.Status == DeletionStatuses.Pending, cancellationToken);
        if (existing is not null)
        {
            return Result<DeletionRequestResponse>.Success(
                new DeletionRequestResponse(existing.Status, existing.RequestedAt, existing.ExecuteAfter));
        }

        var now = clock.UtcNow;
        var request = new DeletionRequest
        {
            UserId = userId,
            RequestedAt = now,
            ExecuteAfter = now.AddDays(GraceDays),
            Status = DeletionStatuses.Pending,
        };
        db.DeletionRequests.Add(request);
        await db.SaveChangesAsync(cancellationToken);

        return Result<DeletionRequestResponse>.Success(
            new DeletionRequestResponse(request.Status, request.RequestedAt, request.ExecuteAfter));
    }

    public async Task<Result<bool>> CancelAsync(Guid userId, CancellationToken cancellationToken)
    {
        var request = await db.DeletionRequests
            .FirstOrDefaultAsync(d => d.UserId == userId && d.Status == DeletionStatuses.Pending, cancellationToken);
        if (request is null)
        {
            return Result<bool>.Failure(ErrorCodes.NotFound, "No pending deletion request.");
        }

        request.Status = DeletionStatuses.Cancelled;
        request.CancelledAt = clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }

    public async Task<Result<DeletionRequestResponse>> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var request = await db.DeletionRequests.AsNoTracking()
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.Id)
            .FirstOrDefaultAsync(cancellationToken);
        return request is null
            ? Result<DeletionRequestResponse>.Failure(ErrorCodes.NotFound, "No deletion request.")
            : Result<DeletionRequestResponse>.Success(
                new DeletionRequestResponse(request.Status, request.RequestedAt, request.ExecuteAfter));
    }
}
