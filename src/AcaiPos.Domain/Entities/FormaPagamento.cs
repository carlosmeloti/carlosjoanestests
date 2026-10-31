using AcaiPos.Domain.Common;

namespace AcaiPos.Domain.Entities;

/// <summary>
/// Forma de pagamento cadastrada. O botão do PDV usa a cor e os flags desta linha.
/// </summary>
public class FormaPagamento : Entity
{
    public const string DefaultButtonColor = "#2A3642";

    public static readonly Guid IdPix = Guid.Parse("00000000-0000-4000-8000-000000000001");
    public static readonly Guid IdDebito = Guid.Parse("00000000-0000-4000-8000-000000000002");
    public static readonly Guid IdCredito = Guid.Parse("00000000-0000-4000-8000-000000000003");
    public static readonly Guid IdDinheiro = Guid.Parse("00000000-0000-4000-8000-000000000004");

    public string Descricao { get; private set; } = string.Empty;
    public bool CalculaTroco { get; private set; }
    public bool IsParcela { get; private set; }
    public string CorRgbBotao { get; private set; } = DefaultButtonColor;
    public int Ordem { get; private set; }
    public bool IsEnable { get; private set; } = true;
    public string CodigoTpag { get; private set; } = "99";
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; private set; }

    private FormaPagamento()
    {
    }

    public static Guid IdDeInteiro(int id) => id switch
    {
        1 => IdPix,
        2 => IdDebito,
        3 => IdCredito,
        4 => IdDinheiro,
        _ => Guid.Parse($"00000000-0000-4000-8000-{id:000000000000}")
    };

    public static FormaPagamento Criar(
        Guid id,
        string descricao,
        bool calculaTroco,
        bool isParcela,
        string corRgbBotao,
        int ordem,
        bool isEnable,
        string codigoTpag)
    {
        var forma = new FormaPagamento();
        forma.DefinirId(id);
        forma.Atualizar(descricao, calculaTroco, isParcela, corRgbBotao, ordem, isEnable, codigoTpag);
        return forma;
    }

    public void Atualizar(
        string descricao,
        bool calculaTroco,
        bool isParcela,
        string corRgbBotao,
        int ordem,
        bool isEnable,
        string codigoTpag)
    {
        if (string.IsNullOrWhiteSpace(descricao))
            throw new DomainException("Descrição da forma de pagamento é obrigatória.");

        Descricao = descricao.Trim();
        CalculaTroco = calculaTroco;
        IsParcela = isParcela;
        CorRgbBotao = string.IsNullOrWhiteSpace(corRgbBotao) ? DefaultButtonColor : corRgbBotao.Trim();
        Ordem = ordem;
        IsEnable = isEnable;
        CodigoTpag = string.IsNullOrWhiteSpace(codigoTpag) ? "99" : codigoTpag.Trim();
        UpdatedAt = DateTime.UtcNow;
        DeletedAt = null;
    }

    public void AplicarCentral(
        string descricao,
        bool calculaTroco,
        bool isParcela,
        string corRgbBotao,
        int ordem,
        bool isEnable,
        string codigoTpag,
        DateTime updatedAt,
        DateTime? deletedAt)
    {
        Descricao = descricao.Trim();
        CalculaTroco = calculaTroco;
        IsParcela = isParcela;
        CorRgbBotao = string.IsNullOrWhiteSpace(corRgbBotao) ? DefaultButtonColor : corRgbBotao.Trim();
        Ordem = ordem;
        IsEnable = isEnable && deletedAt is null;
        CodigoTpag = string.IsNullOrWhiteSpace(codigoTpag) ? "99" : codigoTpag.Trim();
        UpdatedAt = updatedAt;
        DeletedAt = deletedAt;
    }
}
