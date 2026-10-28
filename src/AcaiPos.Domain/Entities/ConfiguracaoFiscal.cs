using AcaiPos.Domain.Common;

namespace AcaiPos.Domain.Entities;

public class ConfiguracaoFiscal : AuditableEntity
{
    public Guid StoreId { get; private set; }
    public string RazaoSocial { get; private set; } = string.Empty;
    public string NomeFantasia { get; private set; } = string.Empty;
    public string Cnpj { get; private set; } = string.Empty;
    public string InscricaoEstadual { get; private set; } = string.Empty;
    public int Crt { get; private set; } = 1;
    public string Logradouro { get; private set; } = string.Empty;
    public string Numero { get; private set; } = string.Empty;
    public string Bairro { get; private set; } = string.Empty;
    public int CodigoMunicipio { get; private set; }
    public string Municipio { get; private set; } = string.Empty;
    public string Uf { get; private set; } = string.Empty;
    public string Cep { get; private set; } = string.Empty;
    public string? Telefone { get; private set; }
    public string Csc { get; private set; } = string.Empty;
    public int IdTokenCsc { get; private set; }
    public bool Homologacao { get; private set; } = true;
    public int Serie { get; private set; } = 1;
    public int ProximoNumero { get; private set; } = 1;
    public string? CertificadoArquivo { get; private set; }
    public string? CertificadoSenhaProtegida { get; private set; }
    public string JustificativaContingencia { get; private set; } = "SEFAZ indisponivel no momento da venda. Emissao em contingencia offline.";
    public bool EmitirPorPadrao { get; private set; }

    private ConfiguracaoFiscal()
    {
    }

    public static ConfiguracaoFiscal Criar(Guid storeId)
    {
        if (storeId == Guid.Empty)
            throw new DomainException("Loja é obrigatória.");

        return new ConfiguracaoFiscal { StoreId = storeId };
    }

    public void Atualizar(
        string razaoSocial,
        string nomeFantasia,
        string cnpj,
        string inscricaoEstadual,
        int crt,
        string logradouro,
        string numero,
        string bairro,
        int codigoMunicipio,
        string municipio,
        string uf,
        string cep,
        string? telefone,
        string csc,
        int idTokenCsc,
        bool homologacao,
        int serie,
        int proximoNumero,
        string? certificadoArquivo,
        string justificativaContingencia,
        bool emitirPorPadrao)
    {
        if (string.IsNullOrWhiteSpace(razaoSocial))
            throw new DomainException("Razão social é obrigatória.");
        if (crt is < 1 or > 4)
            throw new DomainException("CRT deve ser 1, 2, 3 ou 4.");
        if (serie <= 0)
            throw new DomainException("Série da NFC-e deve ser maior que zero.");
        if (proximoNumero <= 0)
            throw new DomainException("Próximo número da NFC-e deve ser maior que zero.");
        if (idTokenCsc < 0)
            throw new DomainException("Id do CSC não pode ser negativo.");

        var ufLimpa = (uf ?? string.Empty).Trim().ToUpperInvariant();
        if (ufLimpa.Length != 2)
            throw new DomainException("UF deve ter 2 letras.");

        RazaoSocial = razaoSocial.Trim();
        NomeFantasia = string.IsNullOrWhiteSpace(nomeFantasia) ? RazaoSocial : nomeFantasia.Trim();
        Cnpj = Digitos(cnpj);
        InscricaoEstadual = string.IsNullOrWhiteSpace(inscricaoEstadual) ? string.Empty : inscricaoEstadual.Trim();
        Crt = crt;
        Logradouro = (logradouro ?? string.Empty).Trim();
        Numero = (numero ?? string.Empty).Trim();
        Bairro = (bairro ?? string.Empty).Trim();
        CodigoMunicipio = codigoMunicipio;
        Municipio = (municipio ?? string.Empty).Trim();
        Uf = ufLimpa;
        Cep = Digitos(cep);
        Telefone = string.IsNullOrWhiteSpace(telefone) ? null : Digitos(telefone);
        Csc = (csc ?? string.Empty).Trim();
        IdTokenCsc = idTokenCsc;
        Homologacao = homologacao;
        Serie = serie;
        ProximoNumero = proximoNumero;
        CertificadoArquivo = string.IsNullOrWhiteSpace(certificadoArquivo) ? null : certificadoArquivo.Trim();
        JustificativaContingencia = string.IsNullOrWhiteSpace(justificativaContingencia)
            ? "SEFAZ indisponivel no momento da venda. Emissao em contingencia offline."
            : justificativaContingencia.Trim();
        EmitirPorPadrao = emitirPorPadrao;
        MarkUpdated();
    }

    public void DefinirSenhaProtegida(string? senhaProtegida)
    {
        CertificadoSenhaProtegida = string.IsNullOrWhiteSpace(senhaProtegida) ? null : senhaProtegida.Trim();
        MarkUpdated();
    }

    public int ReservarNumero()
    {
        var numero = ProximoNumero <= 0 ? 1 : ProximoNumero;
        ProximoNumero = numero + 1;
        MarkUpdated();
        return numero;
    }

    public void ValidarParaEmissao()
    {
        if (Cnpj.Length != 14)
            throw new DomainException("Informe o CNPJ do emitente na configuração fiscal.");
        if (string.IsNullOrWhiteSpace(RazaoSocial) || string.IsNullOrWhiteSpace(Logradouro) || string.IsNullOrWhiteSpace(Numero) || string.IsNullOrWhiteSpace(Bairro) || string.IsNullOrWhiteSpace(Municipio))
            throw new DomainException("Complete o endereço do emitente na configuração fiscal.");
        if (CodigoMunicipio is < 1000000 or > 9999999)
            throw new DomainException("Informe o código IBGE do município com 7 dígitos.");
        if (Cep.Length != 8)
            throw new DomainException("Informe o CEP do emitente com 8 dígitos.");
        if (string.IsNullOrWhiteSpace(InscricaoEstadual))
            throw new DomainException("Informe a inscrição estadual do emitente.");
        if (string.IsNullOrWhiteSpace(Csc) || IdTokenCsc <= 0)
            throw new DomainException("Informe o CSC e o id do token na configuração fiscal.");
        if (string.IsNullOrWhiteSpace(CertificadoArquivo) || string.IsNullOrWhiteSpace(CertificadoSenhaProtegida))
            throw new DomainException("Informe o certificado A1 e a senha na configuração fiscal.");
        if (!File.Exists(CertificadoArquivo))
            throw new DomainException("O arquivo do certificado A1 não foi encontrado.");
    }

    private static string Digitos(string? texto)
        => new((texto ?? string.Empty).Where(char.IsDigit).ToArray());
}
