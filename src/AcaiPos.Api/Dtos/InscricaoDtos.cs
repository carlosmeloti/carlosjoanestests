namespace AcaiPos.Api.Dtos;

public sealed record InscricaoRequest(string CodigoLoja, string NomeLoja, string CodigoTerminal, string NomeTerminal);

public sealed record InscricaoResponse(Guid LojaId, Guid TerminalId, string Segredo);
