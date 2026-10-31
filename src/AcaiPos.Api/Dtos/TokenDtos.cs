namespace AcaiPos.Api.Dtos;

public sealed record TokenRequest(Guid TerminalId, string Segredo);

public sealed record TokenResponse(string AccessToken, int ExpiresIn);
