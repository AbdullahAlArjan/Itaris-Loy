using NetArchTest.Rules;

namespace Itaris.Tests.Architecture;

/// <summary>
/// Enforces doc 04 Part 7 module boundaries from day one: modules communicate only
/// through PublicApi; nobody reaches into another module's Domain or Persistence.
/// </summary>
public class ModuleBoundaryTests
{
    private static readonly string[] ModuleNames =
    [
        "Identity", "Customers", "Merchants", "Loyalty",
        "Transactions", "Rewards", "Ops", "Reporting",
    ];

    private static readonly System.Reflection.Assembly[] ModuleAssemblies =
    [
        typeof(Itaris.Modules.Identity.PublicApi.IdentityModule).Assembly,
        typeof(Itaris.Modules.Customers.PublicApi.CustomersModule).Assembly,
        typeof(Itaris.Modules.Merchants.PublicApi.MerchantsModule).Assembly,
        typeof(Itaris.Modules.Loyalty.PublicApi.LoyaltyModule).Assembly,
        typeof(Itaris.Modules.Transactions.PublicApi.TransactionsModule).Assembly,
        typeof(Itaris.Modules.Rewards.PublicApi.RewardsModule).Assembly,
        typeof(Itaris.Modules.Ops.PublicApi.OpsModule).Assembly,
        typeof(Itaris.Modules.Reporting.PublicApi.ReportingModule).Assembly,
    ];

    public static TheoryData<string> Modules() => [.. ModuleNames];

    [Theory]
    [MemberData(nameof(Modules))]
    public void Module_does_not_depend_on_other_modules_internals(string module)
    {
        var otherModulesInternals = ModuleNames
            .Where(m => m != module)
            .SelectMany(m => new[] { $"Itaris.Modules.{m}.Domain", $"Itaris.Modules.{m}.Persistence", $"Itaris.Modules.{m}.Features" })
            .ToArray();

        var result = Types.InAssemblies(ModuleAssemblies)
            .That().ResideInNamespace($"Itaris.Modules.{module}")
            .ShouldNot().HaveDependencyOnAny(otherModulesInternals)
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"{module} reaches into another module's internals: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void Module_does_not_depend_on_the_composition_root(string module)
    {
        var result = Types.InAssemblies(ModuleAssemblies)
            .That().ResideInNamespace($"Itaris.Modules.{module}")
            .ShouldNot().HaveDependencyOn("Itaris.Api")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"{module} depends on the composition root: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void SharedKernel_depends_on_no_other_itaris_assembly()
    {
        var result = Types.InAssembly(typeof(Itaris.SharedKernel.ErrorCodes).Assembly)
            .ShouldNot().HaveDependencyOnAny(
                "Itaris.Api", "Itaris.Infrastructure", "Itaris.Modules")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"SharedKernel must stay dependency-free: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }
}
