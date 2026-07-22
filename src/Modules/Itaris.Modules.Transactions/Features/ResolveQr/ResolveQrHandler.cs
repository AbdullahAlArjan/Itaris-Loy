using Itaris.Infrastructure.Auth;
using Itaris.Modules.Customers.PublicApi;
using Itaris.Modules.Loyalty.PublicApi;
using Itaris.Modules.Transactions.Persistence;
using Itaris.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Itaris.Modules.Transactions.Features.ResolveQr;

/// <summary>doc 05 D1 body: { qrPayload, branchId }.</summary>
public sealed record ResolveQrRequest(string QrPayload, Guid BranchId);

public sealed record MembershipSummary(long PointsBalance, int StampsFilled, int CardSize, int CardCycle);

public sealed record ResolveQrResponse(
    Guid CustomerId, string? FirstName, bool IsNewMember, MembershipSummary? Membership);

/// <summary>
/// doc 05 D1 — cashier scans a customer's QR. Validates the signed QR token and enforces SINGLE USE
/// of its nonce (jti): the first resolve consumes it; a second resolve of the same QR → QR_ALREADY_USED
/// (doc 06 test: QR nonce reuse rejected). The nonce is stored in idempotency_records (insert-first)
/// so no extra table is needed. Errors: QR_EXPIRED, QR_INVALID, QR_ALREADY_USED.
/// </summary>
public sealed class ResolveQrHandler(
    TransactionsDbContext db,
    ITokenService tokens,
    ICustomerDirectory customers,
    ILoyaltyTransactionParticipant loyalty,
    IClock clock)
{
    public async Task<Result<ResolveQrResponse>> HandleAsync(
        Guid merchantId, ResolveQrRequest request, CancellationToken cancellationToken)
    {
        var validation = tokens.ValidateQrToken(request.QrPayload);
        switch (validation.Status)
        {
            case QrValidationStatus.Expired:
                return Fail(ErrorCodes.QrExpired, "This QR code has expired. Ask the customer to refresh it.");
            case QrValidationStatus.Invalid:
                return Fail(ErrorCodes.QrInvalid, "This QR code is not valid.");
        }

        if (!await TryConsumeNonceAsync(validation.Nonce, validation.ExpiresAt, cancellationToken))
        {
            return Fail(ErrorCodes.QrAlreadyUsed, "This QR code was already used. Ask the customer to refresh it.");
        }

        var summary = await customers.GetSummaryAsync(validation.CustomerId, cancellationToken);
        var snapshot = await loyalty.GetMembershipSnapshotAsync(merchantId, validation.CustomerId, cancellationToken);

        MembershipSummary? membership = snapshot is null
            ? null
            : new MembershipSummary(snapshot.NewPointsBalance, snapshot.StampsFilled, snapshot.CardSize, snapshot.CardCycle);

        return Result<ResolveQrResponse>.Success(new ResolveQrResponse(
            validation.CustomerId, summary?.FirstName, IsNewMember: snapshot is null, membership));
    }

    private async Task<bool> TryConsumeNonceAsync(string nonce, DateTimeOffset expiresAt, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var inserted = await db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO transactions.idempotency_records (key, request_hash, locked_at, expires_at)
            VALUES ({"qr-nonce:" + nonce}, {"qr"}, {now}, {expiresAt})
            ON CONFLICT (key) DO NOTHING", ct);
        return inserted == 1;
    }

    private static Result<ResolveQrResponse> Fail(string code, string message) =>
        Result<ResolveQrResponse>.Failure(code, message);
}
