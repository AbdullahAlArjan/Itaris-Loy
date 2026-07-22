using Itaris.Api.Middleware;
using Itaris.Infrastructure;
using Itaris.Infrastructure.Auditing;
using Itaris.Infrastructure.Auth;
using Itaris.Infrastructure.Observability;
using Itaris.Infrastructure.Sms;
using Itaris.Modules.Customers.PublicApi;
using Itaris.Modules.Identity.PublicApi;
using Itaris.Modules.Loyalty.PublicApi;
using Itaris.Modules.Merchants.PublicApi;
using Itaris.Modules.Ops.PublicApi;
using Itaris.Modules.Reporting.PublicApi;
using Itaris.Modules.Rewards.PublicApi;
using Itaris.Modules.Transactions.PublicApi;
using Itaris.SharedKernel;
using Microsoft.OpenApi;
using Serilog;

Log.Logger = new LoggerConfiguration().ConfigureItarisSerilog().CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ConfigureItarisSerilog());

var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("ConnectionStrings:Postgres is not configured.");

// JSON: enums as strings across the wire (doc 05 uses string values, not integers).
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

// Cross-cutting infrastructure
builder.Services.AddItarisOpenTelemetry();
builder.Services.AddItarisAuth(builder.Configuration);
builder.Services.AddItarisAuditing();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<ISmsProvider, FakeSmsProvider>();

// Modules (composition root only — doc 04 Part 7)
builder.Services.AddIdentityModule(connectionString);
builder.Services.AddCustomersModule(connectionString);
builder.Services.AddMerchantsModule(connectionString);
builder.Services.AddLoyaltyModule(connectionString);
builder.Services.AddTransactionsModule(connectionString);
builder.Services.AddRewardsModule(connectionString);
builder.Services.AddOpsModule(connectionString);
builder.Services.AddReportingModule();

// OpenAPI with the three doc 05 audiences as JWT bearer schemes
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "Itaris API";
        document.Info.Version = "v1";
        var components = document.Components ??= new OpenApiComponents();
        var securitySchemes = components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        foreach (var audience in (string[])["customer", "staff", "admin"])
        {
            securitySchemes[$"bearer-{audience}"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = $"JWT for the '{audience}' audience (doc 05 global conventions).",
            };
        }

        return Task.CompletedTask;
    });
});

var app = builder.Build();

// Dev/local convenience only: apply pending migrations on startup. Production applies
// migrations as a deploy step (doc 04 operational posture), not at boot.
if (app.Environment.IsDevelopment())
{
    await app.Services.MigrateIdentityAsync(app.Configuration);
    await app.Services.MigrateCustomersAsync();
    await app.Services.MigrateAndSeedMerchantsAsync();
    await app.Services.MigrateOpsAsync();
    await app.Services.MigrateLoyaltyAsync();
    await app.Services.MigrateTransactionsAsync();
    await app.Services.MigrateRewardsAsync();
}

app.UseMiddleware<ErrorEnvelopeMiddleware>();
app.UseMiddleware<IdempotencyMiddleware>();
app.UseSerilogRequestLogging();

app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Itaris API v1"));

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
    .WithSummary("Liveness probe");

app.MapIdentityEndpoints();
app.MapCustomersEndpoints();
app.MapMerchantsEndpoints();
app.MapLoyaltyEndpoints();
app.MapTransactionsEndpoints();
app.MapRewardsEndpoints();
app.MapOpsEndpoints();
app.MapReportingEndpoints();

app.Run();

/// <summary>Exposed for WebApplicationFactory in integration tests.</summary>
public partial class Program;
