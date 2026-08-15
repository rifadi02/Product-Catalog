namespace Catalog.Api.Extensions;

public sealed class ApiVersionParameterFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var versionParameter = operation.Parameters?
            .FirstOrDefault(p => string.Equals(p.Name, "version", StringComparison.OrdinalIgnoreCase));

        if (versionParameter is null) return;

        versionParameter.Required = true;
        versionParameter.Schema.Default = new OpenApiString("1.0");
        versionParameter.Description = "API version. This build serves 1.0.";
    }
}
