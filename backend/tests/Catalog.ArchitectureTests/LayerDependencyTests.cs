namespace Catalog.ArchitectureTests;

/// <summary>
/// §0.1. The dependency direction is a design decision; without a test it is a suggestion.
/// These fail the build the moment somebody adds a reference that points the wrong way — which
/// is the only time anyone would find out otherwise.
/// </summary>
public sealed class LayerDependencyTests
{
    private static readonly Assembly Domain = typeof(Product).Assembly;
    private static readonly Assembly Application = typeof(IProductRepository).Assembly;
    private static readonly Assembly Infrastructure = typeof(AppDbContext).Assembly;
    private static readonly Assembly Api = typeof(ProductsController).Assembly;

    [Fact]
    public void Domain_should_not_depend_on_any_other_layer()
    {
        var result = Types.InAssembly(Domain)
            .ShouldNot()
            .HaveDependencyOnAny("Catalog.Application", "Catalog.Infrastructure", "Catalog.Api")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Because(result));
    }

    [Fact]
    public void Domain_should_not_depend_on_EntityFrameworkCore()
    {
        var result = Types.InAssembly(Domain)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Npgsql")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Because(result));
    }

    [Fact]
    public void Application_should_not_depend_on_Infrastructure_or_Api()
    {
        var result = Types.InAssembly(Application)
            .ShouldNot()
            .HaveDependencyOnAny("Catalog.Infrastructure", "Catalog.Api")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Because(result));
    }

    [Fact]
    public void Application_should_not_depend_on_EntityFrameworkCore()
    {
        var result = Types.InAssembly(Application)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Npgsql")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Because(result));
    }

    [Fact]
    public void Application_should_not_depend_on_AspNetCore()
    {
        var result = Types.InAssembly(Application)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.AspNetCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Because(result));
    }

    [Fact]
    public void Infrastructure_should_not_depend_on_Api()
    {
        var result = Types.InAssembly(Infrastructure)
            .ShouldNot()
            .HaveDependencyOn("Catalog.Api")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Because(result));
    }

    [Fact]
    public void Handlers_should_be_sealed()
    {
        var result = Types.InAssembly(Application)
            .That().HaveNameEndingWith("Handler")
            .Should().BeSealed()
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Because(result));
    }

    [Fact]
    public void Entities_should_have_no_public_setters()
    {
        var entityTypes = Domain.GetTypes()
            .Where(t => t.Namespace == "Catalog.Domain.Entities" && t is { IsClass: true, IsAbstract: false });

        var offenders = entityTypes
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Where(p => p.SetMethod is { IsPublic: true })
            .Select(p => $"{p.DeclaringType!.Name}.{p.Name}")
            .ToArray();

        offenders.Should().BeEmpty(
            "entity state must only change through behaviour methods, but these setters are public: {0}",
            string.Join(", ", offenders));
    }

    [Fact]
    public void Api_is_the_only_layer_that_references_HttpContext()
    {
        var result = Types.InAssemblies([Domain, Application, Infrastructure])
            .ShouldNot()
            .HaveDependencyOn("Microsoft.AspNetCore.Http")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Because(result));
    }

    private static string Because(TestResult result) =>
        "the dependency direction in §0.1 must hold, but these types violate it: " +
        string.Join(", ", result.FailingTypeNames ?? []);

    private static readonly Type _ = typeof(Program);
}
