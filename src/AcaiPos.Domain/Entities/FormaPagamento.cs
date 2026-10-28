namespace AcaiPos.Domain.Entities;

/// <summary>
/// Forma de pagamento cadastrada. O botão do PDV usa a cor e os flags desta linha.
/// </summary>
public class FormaPagamento
{
    public const string DefaultButtonColor = "#2A3642";

    public int Id { get; private set; }
    public string Descricao { get; private set; } = string.Empty;
    public bool CalculaTroco { get; private set; }
    public bool IsParcela { get; private set; }
    public string CorRgbBotao { get; private set; } = DefaultButtonColor;
    public int Ordem { get; private set; }
    public bool IsEnable { get; private set; } = true;
    public string CodigoTpag { get; private set; } = "99";

    private FormaPagamento()
    {
    }
}
