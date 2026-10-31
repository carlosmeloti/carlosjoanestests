using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace AcaiPos.Api.Swagger;

public sealed class EsquemaSegurancaFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var exigeToken = context.ApiDescription.ActionDescriptor.EndpointMetadata.OfType<IAuthorizeData>().Any();
        if (!exigeToken)
            return;

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = []
        });
    }
}
