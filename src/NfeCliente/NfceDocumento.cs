using System.Globalization;
using System.Xml;
using Unimake.Business.DFe.Servicos;
using Unimake.Business.DFe.Utility;
using Unimake.Business.DFe.Xml.NFe;

namespace NfeCliente;

public static class NfceDocumento
{
    public const string TextoHomologacao = "NOTA FISCAL EMITIDA EM AMBIENTE DE HOMOLOGACAO - SEM VALOR FISCAL";
    public const string NomeDestinatarioHomologacao = "NF-E EMITIDA EM AMBIENTE DE HOMOLOGACAO - SEM VALOR FISCAL";

    private static readonly string[] CsosnSemCredito = ["102", "103", "300", "400"];

    public static NFe Montar(PedidoNfce pedido)
    {
        ArgumentNullException.ThrowIfNull(pedido);
        if (pedido.Itens.Count == 0)
            throw new ArgumentException("A NFC-e precisa de ao menos um item.");
        if (pedido.Pagamentos.Count == 0)
            throw new ArgumentException("A NFC-e precisa de ao menos um pagamento.");

        var uf = ClienteNfe.UfParaEnum(pedido.Credenciais.Uf);
        var cnpj = SomenteDigitos(pedido.Emitente.Cnpj);
        if (cnpj.Length != 14)
            throw new ArgumentException("CNPJ do emitente deve ter 14 dígitos.");

        var contingencia = pedido.Contingencia;
        var tipoEmissao = contingencia ? TipoEmissao.ContingenciaOffLine : TipoEmissao.Normal;
        var codigo = XMLUtility.GerarCodigoNumerico(pedido.Numero, 8);
        var cNf = codigo.ToString("00000000", CultureInfo.InvariantCulture);
        if (cNf == "00000000")
            cNf = "00000001";

        var emissao = pedido.EmitidoEm;
        var chave = XMLUtility.MontarChaveDFe(
            uf,
            emissao.DateTime,
            cnpj,
            ModeloDFe.NFCe,
            pedido.Serie,
            pedido.Numero,
            tipoEmissao,
            cNf);

        if (chave.Length != 44)
            throw new InvalidOperationException("A chave da NFC-e não ficou com 44 dígitos.");

        var ide = new Ide
        {
            CUF = uf,
            CNF = cNf,
            NatOp = "VENDA AO CONSUMIDOR",
            Mod = ModeloDFe.NFCe,
            Serie = pedido.Serie,
            NNF = pedido.Numero,
            DhEmi = emissao,
            TpNF = TipoOperacao.Saida,
            IdDest = DestinoOperacao.OperacaoInterna,
            CMunFG = pedido.Emitente.CodigoMunicipio,
            TpImp = FormatoImpressaoDANFE.NFCe,
            TpEmis = tipoEmissao,
            CDV = int.Parse(chave[^1].ToString(), CultureInfo.InvariantCulture),
            TpAmb = pedido.Credenciais.Homologacao ? TipoAmbiente.Homologacao : TipoAmbiente.Producao,
            FinNFe = FinalidadeNFe.Normal,
            IndFinal = SimNao.Sim,
            IndPres = IndicadorPresenca.OperacaoPresencial,
            ProcEmi = ProcessoEmissao.AplicativoContribuinte,
            VerProc = "AcaiPos"
        };
        ide.CUFField = (int)uf;

        if (contingencia)
        {
            var justificativa = string.IsNullOrWhiteSpace(pedido.JustificativaContingencia)
                ? "SEFAZ indisponivel no momento da venda. Emissao em contingencia offline."
                : pedido.JustificativaContingencia.Trim();
            if (justificativa.Length < 15)
                justificativa = justificativa.PadRight(15, '.');
            if (justificativa.Length > 256)
                justificativa = justificativa[..256];

            ide.DhCont = emissao;
            ide.XJust = justificativa;
        }

        var emitente = pedido.Emitente;
        var tributos = DestacaReforma(emissao)
            ? pedido.Itens.Select(item => CalcularIbsCbs(emissao, item)).ToList()
            : null;

        var inf = new InfNFe
        {
            Versao = "4.00",
            Id = "NFe" + chave,
            Ide = ide,
            Emit = new Emit
            {
                CNPJ = cnpj,
                XNome = LimparTexto(emitente.RazaoSocial, 60),
                XFant = LimparTexto(string.IsNullOrWhiteSpace(emitente.NomeFantasia) ? emitente.RazaoSocial : emitente.NomeFantasia, 60),
                IE = LimparIe(emitente.InscricaoEstadual),
                CRT = CrtDe(emitente.Crt),
                EnderEmit = new EnderEmit
                {
                    XLgr = LimparTexto(emitente.Logradouro, 60),
                    Nro = LimparTexto(emitente.Numero, 60),
                    XBairro = LimparTexto(emitente.Bairro, 60),
                    CMun = emitente.CodigoMunicipio,
                    XMun = LimparTexto(emitente.Municipio, 60),
                    UF = uf,
                    CEP = SomenteDigitos(emitente.Cep),
                    Fone = TelefoneOuNulo(emitente.Telefone)
                }
            },
            Det = MontarItens(pedido, tributos),
            Total = MontarTotal(pedido, tributos),
            Transp = new Transp { ModFrete = ModalidadeFrete.SemOcorrenciaTransporte },
            Pag = MontarPagamentos(pedido)
        };

        var cpf = SomenteDigitos(pedido.CpfConsumidor);
        if (cpf.Length == 11)
        {
            inf.Dest = new Dest
            {
                CPF = cpf,
                IndIEDest = IndicadorIEDestinatario.NaoContribuinte,
                XNome = pedido.Credenciais.Homologacao ? NomeDestinatarioHomologacao : null
            };
        }

        return new NFe { InfNFeField = inf };
    }

    public static XmlDocument GerarXml(PedidoNfce pedido)
    {
        var nota = Montar(pedido);
        var lote = new EnviNFe
        {
            Versao = "4.00",
            IdLote = "1",
            IndSinc = SimNao.Sim,
            NFe = [nota]
        };

        XmlDocument gerado;
        var cultura = CultureInfo.CurrentCulture;
        var culturaUi = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            gerado = lote.GerarXML() ?? throw new InvalidOperationException("Não foi possível serializar a NFC-e.");
        }
        finally
        {
            CultureInfo.CurrentCulture = cultura;
            CultureInfo.CurrentUICulture = culturaUi;
        }

        var nos = gerado.GetElementsByTagName("NFe");
        if (nos.Count == 0)
            throw new InvalidOperationException("O XML gerado não contém a tag NFe.");

        var documento = new XmlDocument { PreserveWhitespace = true };
        documento.AppendChild(documento.ImportNode(nos[0]!, true));
        return documento;
    }

    private static List<Det> MontarItens(PedidoNfce pedido, IReadOnlyList<TributoIbsCbs>? tributos)
    {
        var itens = new List<Det>();
        for (var i = 0; i < pedido.Itens.Count; i++)
        {
            var item = pedido.Itens[i];
            var csosn = SomenteDigitos(item.Csosn);
            if (!CsosnSemCredito.Contains(csosn))
                throw new ArgumentException($"CSOSN {item.Csosn} não é suportado. Use 102, 103, 300 ou 400.");

            var bruto = decimal.Round(item.Quantidade * item.ValorUnitario, 2, MidpointRounding.AwayFromZero);
            var descricao = i == 0 && pedido.Credenciais.Homologacao
                ? TextoHomologacao
                : LimparTexto(item.Descricao, 120);
            var ean = string.IsNullOrWhiteSpace(item.CodigoBarras) ? "SEM GTIN" : item.CodigoBarras.Trim();
            var cest = SomenteDigitos(item.Cest);

            var prod = new Prod
            {
                CProd = LimparTexto(item.Codigo, 60),
                CEAN = ean,
                XProd = descricao,
                NCM = SomenteDigitos(item.Ncm),
                CFOP = SomenteDigitos(item.Cfop),
                UCom = LimparTexto(item.Unidade, 6),
                QCom = item.Quantidade,
                VUnCom = item.ValorUnitario,
                CEANTrib = ean,
                UTrib = LimparTexto(item.Unidade, 6),
                QTrib = item.Quantidade,
                VUnTrib = item.ValorUnitario,
                IndTot = SimNao.Sim
            };
            Definir(valor => prod.VProd = valor, texto => prod.VProdField = texto, bruto);
            if (item.Desconto > 0)
                Definir(valor => prod.VDesc = valor, texto => prod.VDescField = texto, item.Desconto);
            if (item.Acrescimo > 0)
                Definir(valor => prod.VOutro = valor, texto => prod.VOutroField = texto, item.Acrescimo);
            if (cest.Length == 7)
                prod.CEST = cest;

            var imposto = new Imposto
            {
                ICMS = new ICMS
                {
                    ICMSSN102 = new ICMSSN102
                    {
                        Orig = OrigemDe(item.Origem),
                        CSOSN = csosn
                    }
                },
                PIS = MontarPis(item.PisCst),
                COFINS = MontarCofins(item.CofinsCst)
            };
            var det = new Det
            {
                NItem = i + 1,
                Prod = prod,
                Imposto = imposto
            };
            if (tributos is not null)
            {
                var tributo = tributos[i];
                imposto.IBSCBS = MontarGrupoIbsCbs(tributo);
                Definir(valor => det.VItem = valor, texto => det.VItemField = texto, tributo.Base + tributo.ValorIbs + tributo.ValorCbs);
            }

            itens.Add(det);
        }

        return itens;
    }

    private static ICMSTot MontarTotais(PedidoNfce pedido)
    {
        var bruto = pedido.Itens.Sum(item => decimal.Round(item.Quantidade * item.ValorUnitario, 2, MidpointRounding.AwayFromZero));
        var desconto = pedido.Itens.Sum(item => decimal.Round(item.Desconto, 2, MidpointRounding.AwayFromZero));
        var acrescimo = pedido.Itens.Sum(item => decimal.Round(item.Acrescimo, 2, MidpointRounding.AwayFromZero));
        var total = ValorNota(pedido);

        var totais = new ICMSTot();
        Definir(valor => totais.VBC = valor, texto => totais.VBCField = texto, 0);
        Definir(valor => totais.VICMS = valor, texto => totais.VICMSField = texto, 0);
        Definir(valor => totais.VICMSDeson = valor, texto => totais.VICMSDesonField = texto, 0);
        Definir(valor => totais.VFCP = valor, texto => totais.VFCPField = texto, 0);
        Definir(valor => totais.VBCST = valor, texto => totais.VBCSTField = texto, 0);
        Definir(valor => totais.VST = valor, texto => totais.VSTField = texto, 0);
        Definir(valor => totais.VFCPST = valor, texto => totais.VFCPSTField = texto, 0);
        Definir(valor => totais.VFCPSTRet = valor, texto => totais.VFCPSTRetField = texto, 0);
        Definir(valor => totais.VProd = valor, texto => totais.VProdField = texto, bruto);
        Definir(valor => totais.VFrete = valor, texto => totais.VFreteField = texto, 0);
        Definir(valor => totais.VSeg = valor, texto => totais.VSegField = texto, 0);
        Definir(valor => totais.VDesc = valor, texto => totais.VDescField = texto, desconto);
        Definir(valor => totais.VII = valor, texto => totais.VIIField = texto, 0);
        Definir(valor => totais.VIPI = valor, texto => totais.VIPIField = texto, 0);
        Definir(valor => totais.VIPIDevol = valor, texto => totais.VIPIDevolField = texto, 0);
        Definir(valor => totais.VPIS = valor, texto => totais.VPISField = texto, 0);
        Definir(valor => totais.VCOFINS = valor, texto => totais.VCOFINSField = texto, 0);
        Definir(valor => totais.VOutro = valor, texto => totais.VOutroField = texto, acrescimo);
        Definir(valor => totais.VNF = valor, texto => totais.VNFField = texto, total);
        return totais;
    }

    private static Total MontarTotal(PedidoNfce pedido, IReadOnlyList<TributoIbsCbs>? tributos)
    {
        var total = new Total { ICMSTot = MontarTotais(pedido) };
        if (tributos is null)
            return total;

        total.IBSCBSTot = MontarIbsCbsTot(tributos);
        var comReforma = ValorNota(pedido) + tributos.Sum(item => item.ValorIbs) + tributos.Sum(item => item.ValorCbs);
        Definir(valor => total.VNFTot = valor, texto => total.VNFTotField = texto, comReforma);
        return total;
    }

    private const string CstIbsCbsIntegral = "000";
    private const string CClassTribIntegral = "000001";

    /// <summary>
    /// Alíquota da CBS, em percentual, para 2027 e 2028.
    /// Permanece nula até a resolução do Senado (art. 347 da LC 214/2025). Não usar estimativa.
    /// </summary>
    private static readonly decimal? AliquotaCbs2027 = null;

    public static void ValidarIbsCbs(DateTimeOffset emissao, string? ibsCbsCst, string? cClassTrib)
    {
        if (!DestacaReforma(emissao))
            return;

        _ = ResolverIbsCbs(emissao, ibsCbsCst, cClassTrib);
    }

    private static bool DestacaReforma(DateTimeOffset emissao) => emissao.Year >= 2026;

    private static decimal? AliquotaCbs(int ano) => ano switch
    {
        2026 => 0.90m,
        2027 or 2028 => AliquotaCbs2027,
        _ => null
    };

    private static (decimal Uf, decimal Municipio)? AliquotasIbs(int ano) => ano switch
    {
        2026 => (0.10m, 0m),
        2027 or 2028 => (0.05m, 0.05m),
        _ => null
    };

    private readonly record struct TributoIbsCbs(
        string Cst,
        string CClassTrib,
        decimal Base,
        decimal AliquotaUf,
        decimal ValorUf,
        decimal AliquotaMunicipio,
        decimal ValorMunicipio,
        decimal ValorIbs,
        decimal AliquotaCbs,
        decimal ValorCbs);

    private static TributoIbsCbs CalcularIbsCbs(DateTimeOffset emissao, ItemPedidoNfce item)
    {
        var (cst, classe, aliquotaUf, aliquotaMunicipio, aliquotaCbs) = ResolverIbsCbs(emissao, item.IbsCbsCst, item.CClassTrib);
        var bruto = decimal.Round(item.Quantidade * item.ValorUnitario, 2, MidpointRounding.AwayFromZero);
        var desconto = decimal.Round(item.Desconto, 2, MidpointRounding.AwayFromZero);
        var acrescimo = decimal.Round(item.Acrescimo, 2, MidpointRounding.AwayFromZero);
        var baseCalculo = bruto - desconto + acrescimo;
        if (baseCalculo < 0)
            throw new ArgumentException("A base de cálculo do IBS/CBS não pode ser negativa.");

        var valorUf = ValorTributo(baseCalculo, aliquotaUf);
        var valorMunicipio = ValorTributo(baseCalculo, aliquotaMunicipio);
        var valorCbs = ValorTributo(baseCalculo, aliquotaCbs);
        return new TributoIbsCbs(
            cst,
            classe,
            baseCalculo,
            aliquotaUf,
            valorUf,
            aliquotaMunicipio,
            valorMunicipio,
            valorUf + valorMunicipio,
            aliquotaCbs,
            valorCbs);
    }

    private static (string Cst, string Classe, decimal Uf, decimal Municipio, decimal Cbs) ResolverIbsCbs(
        DateTimeOffset emissao,
        string? ibsCbsCst,
        string? cClassTrib)
    {
        var cst = ExigirCodigo(ibsCbsCst, 3, "CST do IBS/CBS");
        var classe = ExigirCodigo(cClassTrib, 6, "cClassTrib");
        if (cst != CstIbsCbsIntegral || classe != CClassTribIntegral)
            throw new ArgumentException($"CST {cst} e cClassTrib {classe} do IBS/CBS não são suportados. Use 000 e 000001.");

        var cbs = AliquotaCbs(emissao.Year)
            ?? throw new InvalidOperationException($"A alíquota da CBS para {emissao.Year} ainda não foi fixada. A emissão com IBS e CBS fica bloqueada até a publicação do percentual oficial.");
        var ibs = AliquotasIbs(emissao.Year)
            ?? throw new InvalidOperationException($"A alíquota do IBS para {emissao.Year} ainda não foi fixada. A emissão com IBS e CBS fica bloqueada até a publicação do percentual oficial.");
        return (cst, classe, ibs.Uf, ibs.Municipio, cbs);
    }

    private static IBSCBS MontarGrupoIbsCbs(TributoIbsCbs tributo)
    {
        var ibsUf = new GIBSUF();
        DefinirAliquota(valor => ibsUf.PIBSUF = valor, texto => ibsUf.PIBSUFField = texto, tributo.AliquotaUf);
        Definir(valor => ibsUf.VIBSUF = valor, texto => ibsUf.VIBSUFField = texto, tributo.ValorUf);

        var ibsMun = new GIBSMun();
        DefinirAliquota(valor => ibsMun.PIBSMun = valor, texto => ibsMun.PIBSMunField = texto, tributo.AliquotaMunicipio);
        Definir(valor => ibsMun.VIBSMun = valor, texto => ibsMun.VIBSMunField = texto, tributo.ValorMunicipio);

        var cbs = new GCBS();
        DefinirAliquota(valor => cbs.PCBS = valor, texto => cbs.PCBSField = texto, tributo.AliquotaCbs);
        Definir(valor => cbs.VCBS = valor, texto => cbs.VCBSField = texto, tributo.ValorCbs);

        var grupo = new GIBSCBS
        {
            GIBSUF = ibsUf,
            GIBSMun = ibsMun,
            GCBS = cbs
        };
        Definir(valor => grupo.VBC = valor, texto => grupo.VBCField = texto, tributo.Base);
        Definir(valor => grupo.VIBS = valor, texto => grupo.VIBSField = texto, tributo.ValorIbs);

        return new IBSCBS
        {
            CST = tributo.Cst,
            CClassTrib = tributo.CClassTrib,
            GIBSCBS = grupo
        };
    }

    private static IBSCBSTot MontarIbsCbsTot(IReadOnlyList<TributoIbsCbs> itens)
    {
        var uf = new GIBSUFTot();
        Definir(valor => uf.VDif = valor, texto => uf.VDifField = texto, 0);
        Definir(valor => uf.VDevTrib = valor, texto => uf.VDevTribField = texto, 0);
        Definir(valor => uf.VIBSUF = valor, texto => uf.VIBSUFField = texto, itens.Sum(item => item.ValorUf));

        var municipio = new GIBSMunTot();
        Definir(valor => municipio.VDif = valor, texto => municipio.VDifField = texto, 0);
        Definir(valor => municipio.VDevTrib = valor, texto => municipio.VDevTribField = texto, 0);
        Definir(valor => municipio.VIBSMun = valor, texto => municipio.VIBSMunField = texto, itens.Sum(item => item.ValorMunicipio));

        var ibs = new GIBSTot { GIBSUF = uf, GIBSMun = municipio };
        Definir(valor => ibs.VIBS = valor, texto => ibs.VIBSField = texto, itens.Sum(item => item.ValorIbs));
        Definir(valor => ibs.VCredPres = valor, texto => ibs.VCredPresField = texto, 0);
        Definir(valor => ibs.VCredPresCondSus = valor, texto => ibs.VCredPresCondSusField = texto, 0);

        var cbs = new GCBSTot();
        Definir(valor => cbs.VDif = valor, texto => cbs.VDifField = texto, 0);
        Definir(valor => cbs.VDevTrib = valor, texto => cbs.VDevTribField = texto, 0);
        Definir(valor => cbs.VCBS = valor, texto => cbs.VCBSField = texto, itens.Sum(item => item.ValorCbs));
        Definir(valor => cbs.VCredPres = valor, texto => cbs.VCredPresField = texto, 0);
        Definir(valor => cbs.VCredPresCondSus = valor, texto => cbs.VCredPresCondSusField = texto, 0);

        var total = new IBSCBSTot { GIBS = ibs, GCBS = cbs };
        Definir(valor => total.VBCIBSCBS = valor, texto => total.VBCIBSCBSField = texto, itens.Sum(item => item.Base));
        return total;
    }

    private static decimal ValorNota(PedidoNfce pedido)
    {
        var bruto = pedido.Itens.Sum(item => decimal.Round(item.Quantidade * item.ValorUnitario, 2, MidpointRounding.AwayFromZero));
        var desconto = pedido.Itens.Sum(item => decimal.Round(item.Desconto, 2, MidpointRounding.AwayFromZero));
        var acrescimo = pedido.Itens.Sum(item => decimal.Round(item.Acrescimo, 2, MidpointRounding.AwayFromZero));
        return decimal.Round(bruto - desconto + acrescimo, 2, MidpointRounding.AwayFromZero);
    }

    private static decimal ValorTributo(decimal baseCalculo, decimal aliquota)
        => decimal.Round(baseCalculo * aliquota / 100m, 2, MidpointRounding.AwayFromZero);

    private static string ExigirCodigo(string? texto, int tamanho, string nome)
    {
        var digitos = SomenteDigitos(texto);
        if (digitos.Length != tamanho)
            throw new ArgumentException($"{nome} deve ter {tamanho} dígitos.");

        return digitos;
    }

    private static Pag MontarPagamentos(PedidoNfce pedido)
    {
        var pagamentos = new Pag
        {
            DetPag = pedido.Pagamentos.Select(pagamento =>
            {
                var meio = MeioDe(pagamento.CodigoTpag);
                var detalhe = new DetPag
                {
                    IndPag = IndicadorPagamento.PagamentoVista,
                    TPag = meio,
                    XPag = meio == MeioPagamento.Outros ? LimparTexto(pagamento.Descricao, 60) : null
                };
                Definir(valor => detalhe.VPag = valor, texto => detalhe.VPagField = texto, pagamento.Valor);
                if (ExigeGrupoCartao(SomenteDigitos(pagamento.CodigoTpag)))
                {
                    detalhe.Card = new Card
                    {
                        TpIntegra = TipoIntegracaoPagamento.PagamentoNaoIntegrado
                    };
                }

                return detalhe;
            }).ToList()
        };

        if (pedido.Troco > 0)
            Definir(valor => pagamentos.VTroco = valor, texto => pagamentos.VTrocoField = texto, pedido.Troco);

        return pagamentos;
    }

    private static PIS MontarPis(string cst)
    {
        cst = NormalizarCst(cst);
        if (cst is "04" or "05" or "06" or "07" or "08" or "09")
            return new PIS { PISNT = new PISNT { CST = cst } };

        var outras = new PISOutr { CST = cst };
        DefinirOpcional(valor => outras.VBC = valor, texto => outras.VBCField = texto, 0);
        DefinirOpcional(valor => outras.PPIS = valor, texto => outras.PPISField = texto, 0);
        DefinirOpcional(valor => outras.VPIS = valor, texto => outras.VPISField = texto, 0);
        return new PIS { PISOutr = outras };
    }

    private static COFINS MontarCofins(string cst)
    {
        cst = NormalizarCst(cst);
        if (cst is "04" or "05" or "06" or "07" or "08" or "09")
            return new COFINS { COFINSNT = new COFINSNT { CST = cst } };

        var outras = new COFINSOutr { CST = cst };
        DefinirOpcional(valor => outras.VBC = valor, texto => outras.VBCField = texto, 0);
        DefinirOpcional(valor => outras.PCOFINS = valor, texto => outras.PCOFINSField = texto, 0);
        DefinirOpcional(valor => outras.VCOFINS = valor, texto => outras.VCOFINSField = texto, 0);
        return new COFINS { COFINSOutr = outras };
    }

    private static void Definir(Action<double> numero, Action<string> campo, decimal valor)
    {
        var arredondado = decimal.Round(valor, 2, MidpointRounding.AwayFromZero);
        numero((double)arredondado);
        campo(arredondado.ToString("0.00", CultureInfo.InvariantCulture));
    }

    private static void DefinirAliquota(Action<double> numero, Action<string> campo, decimal percentual)
    {
        var arredondado = decimal.Round(percentual, 4, MidpointRounding.AwayFromZero);
        numero((double)arredondado);
        campo(arredondado.ToString("0.0000", CultureInfo.InvariantCulture));
    }

    private static void DefinirOpcional(Action<double?> numero, Action<string> campo, decimal valor)
    {
        var arredondado = decimal.Round(valor, 2, MidpointRounding.AwayFromZero);
        numero((double)arredondado);
        campo(arredondado.ToString("0.00", CultureInfo.InvariantCulture));
    }

    private static CRT CrtDe(int crt) => crt switch
    {
        1 => CRT.SimplesNacional,
        2 => CRT.SimplesNacionalExcessoSublimite,
        3 => CRT.RegimeNormal,
        4 => CRT.SimplesNacionalMEI,
        _ => throw new ArgumentException("CRT inválido. Use 1, 2, 3 ou 4.")
    };

    private static OrigemMercadoria OrigemDe(int origem)
    {
        if (Enum.IsDefined(typeof(OrigemMercadoria), origem))
            return (OrigemMercadoria)origem;

        throw new ArgumentException("Origem da mercadoria inválida.");
    }

    private static bool ExigeGrupoCartao(string codigo)
    {
        if (codigo.Length == 1)
            codigo = "0" + codigo;

        return codigo is "03" or "04" or "10" or "11" or "12" or "13" or "15" or "16" or "17" or "18" or "19";
    }

    private static MeioPagamento MeioDe(string codigo)
    {
        var digitos = SomenteDigitos(codigo);
        if (int.TryParse(digitos, NumberStyles.None, CultureInfo.InvariantCulture, out var valor)
            && Enum.IsDefined(typeof(MeioPagamento), valor))
        {
            return (MeioPagamento)valor;
        }

        return MeioPagamento.Outros;
    }

    private static string NormalizarCst(string? cst)
    {
        var digitos = SomenteDigitos(cst);
        if (digitos.Length == 1)
            digitos = "0" + digitos;
        if (digitos.Length != 2)
            throw new ArgumentException("CST de PIS/COFINS deve ter 2 dígitos.");

        return digitos;
    }

    private static string LimparIe(string? inscricao)
    {
        if (string.IsNullOrWhiteSpace(inscricao))
            return "ISENTO";

        var texto = inscricao.Trim();
        if (texto.Equals("ISENTO", StringComparison.OrdinalIgnoreCase))
            return "ISENTO";

        var digitos = SomenteDigitos(texto);
        return digitos.Length == 0 ? "ISENTO" : digitos;
    }

    private static string? TelefoneOuNulo(string? telefone)
    {
        var digitos = SomenteDigitos(telefone);
        return digitos.Length is >= 6 and <= 14 ? digitos : null;
    }

    internal static string SomenteDigitos(string? texto)
        => new((texto ?? string.Empty).Where(char.IsDigit).ToArray());

    private static string LimparTexto(string? texto, int limite)
    {
        var limpo = (texto ?? string.Empty).Trim();
        if (limpo.Length == 0)
            limpo = "NAO INFORMADO";
        return limpo.Length <= limite ? limpo : limpo[..limite];
    }
}
