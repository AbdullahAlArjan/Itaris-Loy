using Itaris.Infrastructure.Auth;
using Itaris.Modules.Transactions.PublicApi;
using Itaris.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Itaris.Modules.Reporting.PublicApi;

/// <summary>
/// Composition entry point for the Reporting module: the merchant overview — the 5 numbers the owner
/// understands (doc 01: "honest analytics for small merchants … not 40 dashboards"). Computed over
/// the Transactions analytics contract.
/// </summary>
public static class ReportingModule
{
    public static IServiceCollection AddReportingModule(this IServiceCollection services) => services;

    public static IEndpointRouteBuilder MapReportingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // doc 05 G5 — GET /merchant/analytics/overview?from=&to=
        endpoints.MapGet("/v1/merchant/analytics/overview", async (
                DateOnly? from, DateOnly? to, ITransactionAnalytics analytics, ICurrentUser user, CancellationToken ct) =>
            {
                var merchantId = user.MerchantId
                    ?? throw new ApiException(403, ErrorCodes.Forbidden, "No merchant scope on this token.");
                var toDate = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
                var fromDate = from ?? toDate.AddDays(-30);
                return Results.Ok(await analytics.GetOverviewAsync(merchantId, fromDate, toDate, ct));
            })
            .RequirePermission("analytics.view")
            .WithTags("Analytics")
            .Produces<AnalyticsOverview>()
            .WithSummary("Merchant analytics overview — the 5 numbers (doc 05 G5)");

        return endpoints;
    }
}
