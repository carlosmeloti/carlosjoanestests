using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using AcaiPos.Api.Persistence;
using AcaiPos.Api.Repositories;
using AcaiPos.Api.Services;
using AcaiPos.Api.Swagger;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

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

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
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

builder.Services.AddScoped<ILojaRepository, LojaRepository>();
builder.Services.AddScoped<ITerminalRepository, TerminalRepository>();
builder.Services.AddScoped<ILoteRepository, LoteRepository>();
builder.Services.AddScoped<IUnitOfWork, CentralUnitOfWork>();
builder.Services.AddScoped<IInscricaoService, InscricaoService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<ILoteService, LoteService>();
builder.Services.AddScoped<ICatalogoService, CatalogoService>();
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
    CatalogoSchema.GarantirAsync(db).GetAwaiter().GetResult();
}

app.MapGet("/", () => Results.Redirect("/swagger"));
app.MapControllers();

app.Run();

public partial class Program;
