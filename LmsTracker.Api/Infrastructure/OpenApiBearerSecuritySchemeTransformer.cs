using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Models;

namespace LmsTracker.Api.Infrastructure;

/// <summary>
/// Adds the Bearer security scheme to the generated OpenAPI document and marks every operation
/// as requiring it, except the two that genuinely don't (health check, login itself) - so the
/// Scalar UI shows the right lock icons and its "try it" panel can actually authenticate against
/// the real JWT bearer auth wired up in Program.cs, instead of just describing the endpoints
/// with no way to exercise the protected ones.
/// </summary>
public sealed class OpenApiBearerSecuritySchemeTransformer(IAuthenticationSchemeProvider authenticationSchemeProvider) : IOpenApiDocumentTransformer
{
    private static readonly HashSet<string> OpenPaths = ["/api/health", "/api/auth/login"];

    public async Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        var schemes = await authenticationSchemeProvider.GetAllSchemesAsync();
        if (schemes.All(scheme => scheme.Name != "Bearer"))
        {
            return;
        }

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Paste the token returned by POST /api/auth/login (without the \"Bearer \" prefix).",
        };

        var securityRequirement = new OpenApiSecurityRequirement
        {
            [new OpenApiSecurityScheme { Reference = new OpenApiReference { Id = "Bearer", Type = ReferenceType.SecurityScheme } }] = [],
        };

        foreach (var (path, pathItem) in document.Paths)
        {
            if (OpenPaths.Contains(path))
            {
                continue;
            }

            foreach (var operation in pathItem.Operations.Values)
            {
                operation.Security.Add(securityRequirement);
            }
        }
    }
}
