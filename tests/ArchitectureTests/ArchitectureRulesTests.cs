using System.Reflection;
using NetArchTest.Rules;
using Xunit;

namespace BankFlow.ArchitectureTests;

public sealed class ArchitectureRulesTests
{
    [Fact]
    public void TransfersDomain_MustRemainIndependent()
    {
        AssertNoDependency(
            typeof(Transfers.Domain.AssemblyReference).Assembly,
            "Transfers.Application",
            "Transfers.Infrastructure",
            "Transfers.Api",
            "Accounts",
            "Microsoft.EntityFrameworkCore");
    }

    [Fact]
    public void AccountsDomain_MustRemainIndependent()
    {
        AssertNoDependency(
            typeof(Accounts.Domain.AssemblyReference).Assembly,
            "Accounts.Application",
            "Accounts.Infrastructure",
            "Accounts.Api",
            "Transfers",
            "Microsoft.EntityFrameworkCore");
    }

    [Fact]
    public void TransfersAssemblies_MustNotDependOnAccounts()
    {
        AssertNoDependency(typeof(Transfers.Domain.AssemblyReference).Assembly, "Accounts");
        AssertNoDependency(typeof(Transfers.Application.AssemblyReference).Assembly, "Accounts");
        AssertNoDependency(typeof(Transfers.Infrastructure.AssemblyReference).Assembly, "Accounts");
    }

    [Fact]
    public void AccountsAssemblies_MustNotDependOnTransfers()
    {
        AssertNoDependency(typeof(Accounts.Domain.AssemblyReference).Assembly, "Transfers");
        AssertNoDependency(typeof(Accounts.Application.AssemblyReference).Assembly, "Transfers");
        AssertNoDependency(typeof(Accounts.Infrastructure.AssemblyReference).Assembly, "Transfers");
    }

    [Fact]
    public void Contracts_MustNotDependOnServiceImplementations()
    {
        AssertNoDependency(
            typeof(BankFlow.Contracts.AssemblyReference).Assembly,
            "Transfers",
            "Accounts",
            "BankFlow.Gateway",
            "BankFlow.Identity");
    }

    private static void AssertNoDependency(Assembly assembly, params string[] forbiddenNamespaces)
    {
        var result = Types
            .InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOnAny(forbiddenNamespaces)
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            $"Tipos com dependências proibidas: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }
}
