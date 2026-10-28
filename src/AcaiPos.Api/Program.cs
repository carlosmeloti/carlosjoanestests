using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using AcaiPos.Api.Contratos;
using AcaiPos.Api.Lotes;
using AcaiPos.Api.Persistence;
using AcaiPos.Api.Seguranca;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

var builder = WebApplication.CreateBuilder(args);

var provider = builder.Configuration["Central:Provider"] ?? "MySql";
var connection = builder.Configuration.GetConnectionString("Central");
var signingKey = builder.Configuration["Sync:ChaveAssinatura"];
var adminKey = builder.Configuration["Sync:ChaveAdmin"];
var usaSqlite = provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase);

if (!usaSqlite && string.IsNullOrWhiteSpace(connection))
    throw new InvalidOperationException("Defina ConnectionStrings:Central no ambiente.");
if (!usaSqlite && connection!.IndexOf("CharSet=", StringComparison.OrdinalIgnoreCase) < 0)
    connection += ";CharSet=utf8mb4";
if (string.IsNullOrWhiteSpace(signingKey) || signingKey.Length < 32)
    throw new InvalidOperationException("Defina Sync:ChaveAssinatura no ambiente, com pelo menos 32 caracteres.");
if (string.IsNullOrWhiteSpace(adminKey))
    throw new InvalidOperationException("Defina Sync:ChaveAdmin no ambiente.");

builder.Services.AddDbContext<CentralDbContext>(options =>
{
    if (usaSqlite)
        options.UseSqlite(connection);
    else
        options.UseMySQL(connection!);
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "acaipos-api",
            ValidateAudience = true,
            ValidAudience = "acaipos-terminal",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("tokens", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "local",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

builder.Services.AddScoped<InscricaoService>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<LoteService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "AcaiPos API",
        Version = "v1",
        Description = "Inscrição do terminal, token e lote de sincronização."
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Token obtido em POST /api/tokens."
    });
    options.OperationFilter<EsquemaSegurancaFilter>();
});

var app = builder.Build();

if (app.Environment.IsProduction())
    app.UseHttpsRedirection();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "AcaiPos API");
    options.DocumentTitle = "AcaiPos API";
});

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CentralDbContext>();
    db.Database.EnsureCreated();
}

app.MapGet("/", () => Results.Redirect("/swagger"));

app.MapPost("/api/inscricoes", async (
    InscricaoRequest request,
    [FromHeader(Name = "X-Chave-Admin")] string? chaveAdmin,
    IConfiguration configuration,
    InscricaoService service,
    CancellationToken cancellationToken) =>
{
    var esperada = configuration["Sync:ChaveAdmin"] ?? string.Empty;
    if (string.IsNullOrEmpty(chaveAdmin) || !SegredoHasher.Iguais(chaveAdmin, esperada))
        return Results.Unauthorized();

    try
    {
        var resposta = await service.InscreverAsync(request, cancellationToken);
        return Results.Ok(resposta);
    }
    catch (RecusaApiException ex)
    {
        return Results.Json(new { mensagem = ex.Message }, statusCode: ex.StatusCode);
    }
})
.WithTags("Inscrição")
.WithSummary("Inscreve a loja e o terminal e devolve o segredo uma vez.")
.Produces<InscricaoResponse>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status409Conflict);

app.MapPost("/api/tokens", async (TokenRequest request, TokenService service, CancellationToken cancellationToken) =>
{
    var token = await service.EmitirAsync(request, cancellationToken);
    return token is null ? Results.Unauthorized() : Results.Ok(token);
})
.RequireRateLimiting("tokens")
.WithTags("Token")
.WithSummary("Troca o segredo do terminal por um token curto.")
.Produces<TokenResponse>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status429TooManyRequests);

app.MapPost("/api/lotes", async (LoteRequest lote, HttpContext http, LoteService service, CancellationToken cancellationToken) =>
{
    var terminalId = Guid.Parse(http.User.FindFirst("sub")!.Value);
    var storeId = Guid.Parse(http.User.FindFirst("store_id")!.Value);
    var resultado = await service.ReceberAsync(terminalId, storeId, lote, cancellationToken);
    return resultado switch
    {
        LoteAceito aceito => Results.Ok(aceito),
        LoteRecusado recusado => Results.BadRequest(recusado),
        _ => Results.BadRequest()
    };
})
.RequireAuthorization()
.WithTags("Lote")
.WithSummary("Grava o lote da loja e do terminal do token. Repetir o mesmo id atualiza a linha.")
.Produces<LoteAceito>(StatusCodes.Status200OK)
.Produces<LoteRecusado>(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status401Unauthorized);

app.Run();

public partial class Program;

file sealed class EsquemaSegurancaFilter : IOperationFilter
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
