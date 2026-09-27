using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Govern.Api.Extensions;

/// <summary>
/// Customises the OpenAPI document (served only in Development) so Swagger UI can call protected endpoints.
/// </summary>
public static class OpenApiExtensions
{
    private const string BearerScheme = JwtBearerDefaults.AuthenticationScheme;

    /// <param name="options">The OpenAPI document options.</param>
    extension(OpenApiOptions options)
    {
        /// <summary>
        /// Declares the JWT bearer scheme and requires it on every operation that does not allow anonymous access,
        /// so Swagger UI's Authorize button sends <c>Authorization: Bearer &lt;access token&gt;</c>.
        /// </summary>
        /// <returns>The options, for chaining.</returns>
        public OpenApiOptions AddBearerSecurity()
        {
            options.AddDocumentTransformer(static (document, _, _) =>
            {
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes[BearerScheme] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "The access token from POST /api/users/sign-in or /api/users/refresh.",
                };
                return Task.CompletedTask;
            });

            options.AddOperationTransformer(static (operation, context, _) =>
            {
                if (!context.Description.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any())
                {
                    operation.Security ??= [];
                    operation.Security.Add(new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(BearerScheme, context.Document)] = [] });
                }

                return Task.CompletedTask;
            });

            return options;
        }
    }
}
