using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AcaiPos.Api.Contratos;
using AcaiPos.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace AcaiPos.Api.Seguranca;

public sealed class TokenService
{
    private readonly CentralDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TokenService> _logger;

    public TokenService(CentralDbContext db, IConfiguration configuration, ILogger<TokenService> logger)
    {
        _db = db;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<TokenResponse?> EmitirAsync(TokenRequest request, CancellationToken cancellationToken)
    {
        var terminal = await _db.Terminals.FirstOrDefaultAsync(item => item.Id == request.TerminalId, cancellationToken);
        if (terminal is null)
            return null;

        var hash = _db.Entry(terminal).Property<string?>("SegredoHash").CurrentValue;
        if (string.IsNullOrEmpty(request.Segredo) || !SegredoHasher.Confere(request.Segredo, hash))
            return null;

        var minutos = int.TryParse(_configuration["Sync:TokenMinutos"], out var configurado) && configurado > 0
            ? configurado
            : 15;
        var chave = _configuration["Sync:ChaveAssinatura"]
            ?? throw new InvalidOperationException("Sync:ChaveAssinatura não configurada.");
        var expires = DateTime.UtcNow.AddMinutes(minutos);
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(chave)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "acaipos-api",
            audience: "acaipos-terminal",
            claims:
            [
                new Claim("sub", terminal.Id.ToString()),
                new Claim("store_id", terminal.StoreId.ToString())
            ],
            expires: expires,
            signingCredentials: credentials);

        _logger.LogInformation("Token emitido para o terminal {TerminalId}.", terminal.Id);
        return new TokenResponse(new JwtSecurityTokenHandler().WriteToken(token), minutos * 60);
    }
}
