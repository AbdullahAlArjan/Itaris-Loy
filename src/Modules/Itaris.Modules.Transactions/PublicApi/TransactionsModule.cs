using Itaris.Infrastructure.Auth;
using Itaris.Infrastructure.Http;
using Itaris.Modules.Transactions.Features.Idempotency;
using Itaris.Modules.Transactions.Features.Reads;
using Itaris.Modules.Transactions.Features.RecordSale;
using Itaris.Modules.Transactions.Features.Refunds;
using Itaris.Modules.Transactions.Features.ResolveQr;
using Itaris.Modules.Transactions.Persistence;
using Itaris.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Itaris.Modules.Transactions.PublicApi;

/// <summary>
/// Composition entry point for the Transactions module: the consistency core. Records sales,
/// writes the immutable points ledger, refunds with compensating entries, resolves QR, and enforces
/// end-to-end idempotency — all with atomic cross-module writes under a membership row lock.
/// </summary>
public static class TransactionsModule
{
    public static IServiceCollection AddTransactionsModule(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<TransactionsDbContext>(options =>
            options.UseNpgsql(connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", TransactionsDbContext.Schema)));

        services.AddScoped<IdempotencyService>();
        services.AddScoped<IdempotencyEndpointFilter>();
        services.AddScoped<RecordSaleHandler>();
        services.AddScoped<RefundHandler>();
        services.AddScoped<ResolveQrHandler>();
        services.AddScoped<TransactionReadsHandler>();

        return services;
    }

    /// <summary>Applies pending transactions-schema migrations. Dev/test startup convenience.</summary>
    public static async Task MigrateTransactionsAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<TransactionsDbContext>().Database.MigrateAsync();
    }

    public static IEndpointRouteBuilder MapTransactionsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var pos = endpoints.MapGroup("/v1/pos").WithTags("POS — transactions");

        // doc 05 D4 [IDEM] — record a sale
        pos.MapPost("/transactions", async (
                RecordSaleRequest request, RecordSaleHandler handler, ICurrentUser user, CancellationToken ct) =>
            {
                var result = await handler.HandleAsync(RequireMerchant(user), RequireStaff(user), request, ct);
                return result.OrThrow();
            })
            .RequirePermission("transactions.record")
            .AddEndpointFilter<IdempotencyEndpointFilter>()
            .Produces<RecordSaleResponse>()
            .WithSummary("Record a sale (doc 05 D4)");

        // doc 05 D7 [IDEM] — refund
        pos.MapPost("/transactions/{transactionId:guid}/refunds", async (
                Guid transactionId, RefundRequest request, RefundHandler handler,
                ICurrentUser user, CancellationToken ct) =>
            {
                var canApprove = user.Permissions.Contains("refunds.approve");
                var result = await handler.HandleAsync(
                    RequireMerchant(user), RequireStaff(user), canApprove, transactionId, request, ct);
                return result.OrThrow();
            })
            .RequirePermission("refunds.create")
            .AddEndpointFilter<IdempotencyEndpointFilter>()
            .Produces<RefundResponse>()
            .WithSummary("Refund a transaction (doc 05 D7)");

        // doc 05 D1 — resolve a customer QR (single-use nonce)
        pos.MapPost("/customers/resolve-qr", async (
                ResolveQrRequest request, ResolveQrHandler handler, ICurrentUser user, CancellationToken ct) =>
            {
                var result = await handler.HandleAsync(RequireMerchant(user), request, ct);
                return Results.Ok(result.OrThrow());
            })
            .RequirePermission("customers.identify")
            .Produces<ResolveQrResponse>()
            .WithSummary("Resolve a customer QR (doc 05 D1)");

        // doc 05 D5 — list transactions
        pos.MapGet("/transactions", async (
                Guid? branchId, int? limit, TransactionReadsHandler handler, ICurrentUser user, CancellationToken ct) =>
                Results.Ok(await handler.ListAsync(RequireMerchant(user), branchId, limit ?? 50, ct)))
            .RequirePermission("transactions.record")
            .Produces<TransactionListResponse>()
            .WithSummary("List transactions (doc 05 D5)");

        // doc 05 D6 — transaction detail
        pos.MapGet("/transactions/{transactionId:guid}", async (
                Guid transactionId, TransactionReadsHandler handler, ICurrentUser user, CancellationToken ct) =>
            {
                var result = await handler.GetDetailAsync(RequireMerchant(user), transactionId, ct);
                return Results.Ok(result.OrThrow());
            })
            .RequirePermission("transactions.record")
            .Produces<TransactionDto>()
            .WithSummary("Transaction detail (doc 05 D6)");

        // doc 05 B6 — customer's own membership ledger
        endpoints.MapGet("/v1/customers/me/memberships/{membershipId:guid}/ledger", async (
                Guid membershipId, TransactionReadsHandler handler, ICurrentUser user, CancellationToken ct) =>
            {
                var result = await handler.GetCustomerLedgerAsync(RequireCustomer(user), membershipId, ct);
                return Results.Ok(result.OrThrow());
            })
            .RequireAuthorization()
            .WithTags("Memberships")
            .Produces<LedgerResponse>()
            .WithSummary("My membership ledger (doc 05 B6)");

        return endpoints;
    }

    private static Guid RequireMerchant(ICurrentUser user) =>
        user.MerchantId ?? throw new ApiException(403, ErrorCodes.Forbidden, "No merchant scope on this token.");

    private static Guid RequireStaff(ICurrentUser user) =>
        user.StaffId ?? throw new ApiException(403, ErrorCodes.Forbidden, "No staff identity on this token.");

    private static Guid RequireCustomer(ICurrentUser user) =>
        user.Id ?? throw new ApiException(401, ErrorCodes.Unauthorized, "Authentication required.");
}
