using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AcaiPos.Api.Dtos;
using AcaiPos.Api.Repositories;
using AcaiPos.Api.Seguranca;
using Microsoft.IdentityModel.Tokens;

namespace AcaiPos.Api.Services;

public sealed class TokenService : ITokenService
{
    private readonly ITerminalRepository _terminais;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TokenService> _logger;

    public TokenService(
        ITerminalRepository terminais,
        IConfiguration configuration,
        ILogger<TokenService> logger)
    {
        _terminais = terminais;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<TokenResponse?> EmitirAsync(TokenRequest request, CancellationToken cancellationToken)
    {
        var terminal = await _terminais.ObterPorIdAsync(request.TerminalId, cancellationToken);
        if (terminal is null)
            return null;

        var hash = _terminais.LerSegredoHash(terminal);
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
