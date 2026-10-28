using AcaiPos.Application.Abstractions;
using AcaiPos.Application.Models;
using AcaiPos.Domain.Common;
using AcaiPos.Domain.Entities;
using AcaiPos.Domain.Enums;

namespace AcaiPos.Application.Services;

public sealed class NfceService
{
    private static readonly string[] CsosnSuportados = ["102", "103", "300", "400"];

    private readonly IPosDbContext _db;
    private readonly ICurrentSession _session;
    private readonly SaleService _sales;
    private readonly IEmissorNfce _emissor;
    private readonly IProtecaoCertificado _protecao;
    private readonly IAuditService _audit;

    public NfceService(
        IPosDbContext db,
        ICurrentSession session,
        SaleService sales,
        IEmissorNfce emissor,
        IProtecaoCertificado protecao,
        IAuditService audit)
    {
        _db = db;
        _session = session;
        _sales = sales;
        _emissor = emissor;
        _protecao = protecao;
        _audit = audit;
    }

    public Task<bool> EmitirPorPadraoAsync(CancellationToken cancellationToken = default)
    {
        var config = ObterConfiguracao();
        return Task.FromResult(config?.EmitirPorPadrao == true);
    }

    public Task<ConfiguracaoFiscalDto?> ObterAsync(CancellationToken cancellationToken = default)
    {
        var config = ObterConfiguracao();
        return Task.FromResult(config is null ? null : Mapear(config));
    }

    public async Task<ConfiguracaoFiscalDto> SalvarAsync(
        SalvarConfiguracaoFiscalRequest request,
        CancellationToken cancellationToken = default)
    {
        var store = _session.CurrentStore ?? throw new DomainException("Nenhuma loja selecionada.");
        var config = _db.ConfiguracoesFiscais.FirstOrDefault(item => item.StoreId == store.Id && item.DeletedAt == null);
        if (config is null)
        {
            config = ConfiguracaoFiscal.Criar(store.Id);
            _db.Add(config);
        }

        config.Atualizar(
            request.RazaoSocial,
            request.NomeFantasia,
            request.Cnpj,
            request.InscricaoEstadual,
            request.Crt,
            request.Logradouro,
            request.Numero,
            request.Bairro,
            request.CodigoMunicipio,
            request.Municipio,
            request.Uf,
            request.Cep,
            request.Telefone,
            request.Csc,
            request.IdTokenCsc,
            request.Homologacao,
            request.Serie,
            request.ProximoNumero,
            request.CertificadoArquivo,
            request.JustificativaContingencia,
            request.EmitirPorPadrao);

        if (!string.IsNullOrWhiteSpace(request.SenhaCertificado))
            config.DefinirSenhaProtegida(_protecao.Proteger(request.SenhaCertificado));

        await _audit.LogAsync("FISCAL_CONFIG_SAVE", "ConfiguracaoFiscal", config.Id, config.Cnpj, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return Mapear(config);
    }

    public async Task<ResultadoEmissaoNfce> ConsultarStatusAsync(CancellationToken cancellationToken = default)
    {
        var config = ObterObrigatoria();
        config.ValidarParaEmissao();
        return await _emissor.ConsultarStatusAsync(Credenciais(config), cancellationToken);
    }

    public Task<IReadOnlyList<ContingenciaPendenteDto>> ListarPendenciasAsync(CancellationToken cancellationToken = default)
    {
        var itens = _db.DocumentosFiscais
            .Where(item => item.Status == StatusDocumentoFiscal.Contingencia && item.Protocolo == null && item.DeletedAt == null)
            .OrderBy(item => item.EmitidoEm)
            .ToList()
            .Select(item => new ContingenciaPendenteDto(item.Id, item.Serie, item.Numero, item.Chave, item.EmitidoEm, item.Motivo))
            .ToList();

        return Task.FromResult<IReadOnlyList<ContingenciaPendenteDto>>(itens);
    }

    public async Task<TransmissaoContingenciaDto> TransmitirPendentesAsync(CancellationToken cancellationToken = default)
    {
        var config = ObterConfiguracao();
        if (config is null)
            return new TransmissaoContingenciaDto(0, 0, 0, "Configuração fiscal ainda não foi salva.");

        config.ValidarParaEmissao();
        var credenciais = Credenciais(config);
        var pendentes = _db.DocumentosFiscais
            .Where(item => item.Status == StatusDocumentoFiscal.Contingencia && item.Protocolo == null && item.DeletedAt == null && item.Xml != null)
            .OrderBy(item => item.EmitidoEm)
            .ToList();

        var autorizadas = 0;
        var rejeitadas = 0;
        var restantes = 0;
        foreach (var documento in pendentes)
        {
            ResultadoEmissaoNfce resultado;
            try
            {
                resultado = await _emissor.TransmitirAsync(credenciais, documento.Xml!, cancellationToken);
            }
            catch (Exception ex) when (ex is not DomainException)
            {
                documento.RegistrarFalhaTransmissao(null, ex.Message);
                restantes++;
                break;
            }

            switch (AplicarTransmissao(documento, resultado))
            {
                case DesfechoTransmissao.Pendente:
                    restantes++;
                    break;
                case DesfechoTransmissao.Autorizada:
                    autorizadas++;
                    continue;
                default:
                    rejeitadas++;
                    continue;
            }

            break;
        }

        restantes = pendentes.Count - autorizadas - rejeitadas;

        await _db.SaveChangesAsync(cancellationToken);
        var mensagem = autorizadas == 0 && rejeitadas == 0 && restantes == 0
            ? "Nenhuma NFC-e em contingência pendente."
            : $"{autorizadas} autorizada(s), {rejeitadas} rejeitada(s), {restantes} ainda pendente(s).";
        return new TransmissaoContingenciaDto(autorizadas, restantes, rejeitadas, mensagem);
    }

    public async Task<string> TransmitirDocumentoAsync(Guid saleId, CancellationToken cancellationToken = default)
    {
        var documento = ObterDocumentoDaVenda(saleId)
            ?? throw new DomainException("Esta venda não tem NFC-e.");
        if (documento.Status != StatusDocumentoFiscal.Contingencia || documento.Protocolo is not null)
            throw new DomainException("Somente NFC-e em contingência, ainda sem protocolo, pode ser enviada.");
        if (string.IsNullOrWhiteSpace(documento.Xml))
            throw new DomainException("A NFC-e em contingência não tem XML para enviar.");

        var venda = _db.Sales.FirstOrDefault(item => item.Id == saleId)
            ?? throw new DomainException("Venda não encontrada.");
        if (venda.Status != SaleStatus.Completed)
            throw new DomainException("Somente a NFC-e de uma venda concluída pode ser enviada.");

        var config = ObterObrigatoria();
        config.ValidarParaEmissao();
        ResultadoEmissaoNfce resultado;
        try
        {
            resultado = await _emissor.TransmitirAsync(Credenciais(config), documento.Xml, cancellationToken);
        }
        catch (Exception ex) when (ex is not DomainException)
        {
            documento.RegistrarFalhaTransmissao(null, ex.Message);
            await _db.SaveChangesAsync(cancellationToken);
            throw new DomainException($"Falha ao enviar a NFC-e: {ex.Message}");
        }

        var desfecho = AplicarTransmissao(documento, resultado);
        await _audit.LogAsync(
            desfecho == DesfechoTransmissao.Autorizada ? "NFCE_AUTORIZADA" : "NFCE_TRANSMISSAO",
            "DocumentoFiscal",
            documento.Id,
            documento.Chave,
            cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return desfecho switch
        {
            DesfechoTransmissao.Autorizada => "NFC-e autorizada.",
            DesfechoTransmissao.Pendente => string.IsNullOrWhiteSpace(resultado.Motivo)
                ? "Sem comunicação com a SEFAZ. A NFC-e continua em contingência."
                : resultado.Motivo,
            _ => $"NFC-e rejeitada ({resultado.Status}): {TextoMotivo(resultado.Motivo)}"
        };
    }

    public Task<XmlNfceExportado?> ObterXmlAsync(Guid saleId, CancellationToken cancellationToken = default)
    {
        var documento = ObterDocumentoDaVenda(saleId);
        if (documento is null || string.IsNullOrWhiteSpace(documento.Xml))
            return Task.FromResult<XmlNfceExportado?>(null);

        var nome = string.IsNullOrWhiteSpace(documento.Chave)
            ? $"{documento.Serie}-{documento.Numero}-nfce.xml"
            : $"{documento.Chave}-nfce.xml";
        return Task.FromResult<XmlNfceExportado?>(new XmlNfceExportado(nome, documento.Xml));
    }

    public Task<XmlNfceExportado?> ObterXmlDocumentoAsync(Guid documentoId, CancellationToken cancellationToken = default)
    {
        var documento = _db.DocumentosFiscais.FirstOrDefault(item => item.Id == documentoId && item.DeletedAt == null);
        if (documento is null || string.IsNullOrWhiteSpace(documento.Xml))
            return Task.FromResult<XmlNfceExportado?>(null);

        var nome = string.IsNullOrWhiteSpace(documento.Chave)
            ? $"{documento.Serie}-{documento.Numero}-nfce.xml"
            : $"{documento.Chave}-nfce.xml";
        return Task.FromResult<XmlNfceExportado?>(new XmlNfceExportado(nome, documento.Xml));
    }

    public Task<NfceGestaoPaginaDto> ListarAsync(NfceGestaoConsulta consulta, CancellationToken cancellationToken = default)
    {
        var (query, vazio) = FiltrarGestao(consulta);
        if (vazio is not null)
            return Task.FromResult(new NfceGestaoPaginaDto([], 0, 1, consulta.Tamanho, 1, vazio));

        var tamanho = consulta.Tamanho < 1 ? 20 : Math.Min(consulta.Tamanho, 100);
        var total = query.Count();
        var paginas = total == 0 ? 1 : (int)Math.Ceiling(total / (double)tamanho);
        var pagina = Math.Clamp(consulta.Pagina < 1 ? 1 : consulta.Pagina, 1, paginas);
        var documentos = query
            .OrderByDescending(item => item.EmitidoEm)
            .Skip((pagina - 1) * tamanho)
            .Take(tamanho)
            .ToList();

        var mensagem = total == 0
            ? "Nenhuma NFC-e encontrada."
            : $"Página {pagina} de {paginas}. {total} NFC-e(s).";
        return Task.FromResult(new NfceGestaoPaginaDto(MapearGestao(documentos), total, pagina, tamanho, paginas, mensagem));
    }

    public Task<NfceRelatorio> MontarRelatorioAsync(NfceGestaoConsulta consulta, CancellationToken cancellationToken = default)
    {
        var store = _session.CurrentStore;
        var (query, vazio) = FiltrarGestao(consulta);
        var periodo = PeriodoRelatorio(consulta);
        var escopo = vazio ?? EscopoRelatorio(consulta);

        if (vazio is not null)
        {
            return Task.FromResult(new NfceRelatorio(
                store?.Code ?? string.Empty,
                store?.Name ?? string.Empty,
                _session.CurrentTerminal?.Code ?? string.Empty,
                _session.CurrentUser?.Name ?? string.Empty,
                escopo,
                periodo,
                0, 0m, 0, 0m, 0, 0m, 0, 0, false, [], DateTime.Now));
        }

        var total = query.Count();
        var itens = MapearGestao(query.OrderBy(item => item.EmitidoEm).Take(500).ToList());
        decimal Soma(StatusDocumentoFiscal status) => itens.Where(item => item.Status == status).Sum(item => item.Total ?? 0m);
        int Conta(StatusDocumentoFiscal status) => itens.Count(item => item.Status == status);

        return Task.FromResult(new NfceRelatorio(
            store?.Code ?? string.Empty,
            store?.Name ?? string.Empty,
            _session.CurrentTerminal?.Code ?? string.Empty,
            _session.CurrentUser?.Name ?? string.Empty,
            escopo,
            periodo,
            Conta(StatusDocumentoFiscal.Autorizada),
            Soma(StatusDocumentoFiscal.Autorizada),
            Conta(StatusDocumentoFiscal.Contingencia),
            Soma(StatusDocumentoFiscal.Contingencia),
            Conta(StatusDocumentoFiscal.Cancelada),
            Soma(StatusDocumentoFiscal.Cancelada),
            Conta(StatusDocumentoFiscal.Rejeitada),
            total,
            total > itens.Count,
            itens.Select(item => new NfceRelatorioLinha(item.EmitidoEm, item.Numero, item.StatusLabel, item.TicketNumber, item.Total)).ToList(),
            DateTime.Now));
    }

    private static string PeriodoRelatorio(NfceGestaoConsulta consulta)
    {
        if (consulta.FromUtc is not DateTime from || consulta.ToUtcExclusive is not DateTime to)
            return "Período não informado";

        var inicio = from.Kind == DateTimeKind.Utc ? from.ToLocalTime() : from;
        var fim = to.Kind == DateTimeKind.Utc ? to.ToLocalTime() : to;
        return $"{inicio:dd/MM/yyyy} a {fim.AddTicks(-1):dd/MM/yyyy}";
    }

    private static string EscopoRelatorio(NfceGestaoConsulta consulta)
    {
        var partes = new List<string> { consulta.SomenteCaixaAberto ? "Caixa em operação" : "Todos os caixas" };
        if (consulta.Status is StatusDocumentoFiscal status)
            partes.Add(RotuloStatus(status));
        if (!string.IsNullOrWhiteSpace(consulta.Ticket))
            partes.Add($"Venda {consulta.Ticket.Trim()}");
        return string.Join(" · ", partes);
    }

    public async Task CancelarVendaAsync(CancelSaleRequest request, CancellationToken cancellationToken = default)
    {
        _sales.EnsureCanCancel(request.AuthorizerUsername, request.AuthorizerPin);

        var venda = _db.Sales.FirstOrDefault(item => item.Id == request.SaleId)
            ?? throw new DomainException("Venda não encontrada.");
        if (venda.Status != SaleStatus.Completed)
            throw new DomainException("Somente vendas concluídas podem ser canceladas.");

        var documento = ObterDocumentoDaVenda(request.SaleId);
        if (documento is null
            || documento.Status is StatusDocumentoFiscal.Rejeitada or StatusDocumentoFiscal.Cancelada)
        {
            await _sales.CancelSaleAsync(request, cancellationToken);
            return;
        }

        if (documento.Status == StatusDocumentoFiscal.Contingencia)
        {
            var motivo = request.Reason?.Trim() ?? string.Empty;
            if (motivo.Length == 0)
                throw new DomainException("Motivo do cancelamento é obrigatório.");

            documento.CancelarSemTransmissao(motivo);
            await _audit.LogAsync("NFCE_CANCELADA_LOCAL", "DocumentoFiscal", documento.Id, documento.Chave, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            await _sales.CancelSaleAsync(request, cancellationToken);
            return;
        }

        if (documento.Status != StatusDocumentoFiscal.Autorizada)
        {
            await _sales.CancelSaleAsync(request, cancellationToken);
            return;
        }

        var justificativa = request.Reason?.Trim() ?? string.Empty;
        if (justificativa.Length is < 15 or > 255)
            throw new DomainException("O motivo do cancelamento da NFC-e precisa ter entre 15 e 255 caracteres.");
        if (string.IsNullOrWhiteSpace(documento.Chave) || string.IsNullOrWhiteSpace(documento.Protocolo))
            throw new DomainException("A NFC-e autorizada não tem chave ou protocolo para cancelar na SEFAZ.");

        var config = ObterObrigatoria();
        config.ValidarParaEmissao();
        var cnpj = new string(config.Cnpj.Where(char.IsDigit).ToArray());
        ResultadoCancelamentoNfce resultado;
        try
        {
            resultado = await _emissor.CancelarAsync(
                new CancelamentoNfce(Credenciais(config), cnpj, documento.Chave, documento.Protocolo, justificativa),
                cancellationToken);
        }
        catch (Exception ex) when (ex is not DomainException)
        {
            throw new DomainException($"Falha ao cancelar a NFC-e na SEFAZ: {ex.Message}");
        }

        if (resultado.SemComunicacao)
            throw new DomainException("Sem comunicação com a SEFAZ. A venda não foi cancelada.");

        if (!resultado.Cancelada)
            throw new DomainException($"NFC-e não cancelada ({resultado.Status}): {TextoMotivo(resultado.Motivo)}");

        documento.Cancelar(resultado.Protocolo, resultado.Status, resultado.Motivo, resultado.XmlEvento);
        await _audit.LogAsync("NFCE_CANCELADA", "DocumentoFiscal", documento.Id, documento.Chave, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await _sales.CancelSaleAsync(request, cancellationToken);
    }

    public async Task<SaleSummaryDto> ConcluirVendaAsync(
        IReadOnlyList<CartItemDto> cartItems,
        IReadOnlyList<PaymentDraftDto> payments,
        decimal discountAmount,
        string? discountAuthorizerUsername,
        string? discountAuthorizerPin,
        decimal surchargeAmount,
        string? cpfConsumidor,
        IProgress<string>? progresso = null,
        CancellationToken cancellationToken = default)
    {
        if (discountAmount > 0)
            _sales.ValidateDiscountAuthorization(discountAuthorizerUsername, discountAuthorizerPin);

        progresso?.Report("Validando os dados da NFC-e");
        var cpf = NormalizarCpf(cpfConsumidor);
        var config = ObterObrigatoria();
        config.ValidarParaEmissao();
        ValidarItens(cartItems);
        ValidarPagamentos(payments);

        var sale = await _sales.PrepararVendaAsync(cartItems, payments, discountAmount, surchargeAmount, cancellationToken);
        if (cpf is not null)
            sale.DefinirCpfConsumidor(cpf);
        _sales.GarantirEstoque(sale);

        var credenciais = Credenciais(config);
        progresso?.Report("Consultando a disponibilidade da SEFAZ");
        var status = await _emissor.ConsultarStatusAsync(credenciais, cancellationToken);

        var numero = config.ReservarNumero();
        await _db.SaveChangesAsync(cancellationToken);

        var pedido = MontarPedido(sale, config, credenciais, numero, cpf);
        var semServico = status.SemComunicacao || !status.ServicoOperando;
        ResultadoEmissaoNfce resultado;
        try
        {
            progresso?.Report(semServico
                ? "SEFAZ indisponível. Emitindo em contingência."
                : "Enviando a NFC-e para a SEFAZ");
            resultado = semServico
                ? await _emissor.EmitirContingenciaAsync(pedido, cancellationToken)
                : await _emissor.EmitirAsync(pedido, cancellationToken);

            if (resultado.SemComunicacao)
            {
                progresso?.Report("Sem resposta da SEFAZ. Emitindo em contingência.");
                resultado = await _emissor.EmitirContingenciaAsync(pedido, cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not DomainException)
        {
            await RegistrarRejeicaoAsync(config.Serie, numero, "ERRO", ex.Message, null, null, cancellationToken);
            throw new DomainException($"Falha ao emitir a NFC-e: {ex.Message}");
        }

        var emitidaEmContingencia = resultado.Contingencia && !resultado.Autorizada;
        if (!resultado.Autorizada && !emitidaEmContingencia)
        {
            await RegistrarRejeicaoAsync(config.Serie, numero, resultado.Status, resultado.Motivo, resultado.Chave, resultado.Xml, cancellationToken);
            var detalhe = string.IsNullOrWhiteSpace(resultado.Motivo) ? "sem motivo informado" : resultado.Motivo;
            throw new DomainException($"NFC-e rejeitada ({resultado.Status}): {detalhe}");
        }

        if (emitidaEmContingencia && string.IsNullOrWhiteSpace(resultado.QrCode))
        {
            await RegistrarRejeicaoAsync(config.Serie, numero, "QR", "QR Code de contingência não foi gerado.", resultado.Chave, resultado.Xml, cancellationToken);
            throw new DomainException("Não foi possível gerar o QR Code da contingência.");
        }

        progresso?.Report(emitidaEmContingencia
            ? "Gravando a venda emitida em contingência"
            : "Gravando a venda e a NFC-e autorizada");
        var documento = DocumentoFiscal.Criar(
            config.Serie,
            numero,
            emitidaEmContingencia ? 9 : 1,
            emitidaEmContingencia ? StatusDocumentoFiscal.Contingencia : StatusDocumentoFiscal.Autorizada,
            DateTime.UtcNow,
            resultado.Chave,
            resultado.Protocolo,
            resultado.Status,
            resultado.Motivo,
            resultado.QrCode,
            resultado.UrlChave,
            resultado.Xml);

        try
        {
            await _audit.LogAsync(
                emitidaEmContingencia ? "NFCE_CONTINGENCIA" : "NFCE_AUTORIZADA",
                "DocumentoFiscal",
                documento.Id,
                resultado.Chave,
                cancellationToken);
            return await _sales.PersistirVendaAsync(sale, documento, cancellationToken);
        }
        catch (Exception ex)
        {
            var mensagem = await GravarDocumentoSemVendaAsync(
                documento,
                resultado,
                emitidaEmContingencia,
                ex,
                cancellationToken);
            throw new DomainException(mensagem);
        }
    }

    private async Task<string> GravarDocumentoSemVendaAsync(
        DocumentoFiscal documento,
        ResultadoEmissaoNfce resultado,
        bool emitidaEmContingencia,
        Exception falha,
        CancellationToken cancellationToken)
    {
        var registrado = false;
        try
        {
            _db.DescartarInclusoesPendentes();
            documento.DesvincularVenda();
            _db.Add(documento);
            await _audit.LogAsync(
                emitidaEmContingencia ? "NFCE_CONTINGENCIA_SEM_VENDA" : "NFCE_AUTORIZADA_SEM_VENDA",
                "DocumentoFiscal",
                documento.Id,
                resultado.Chave,
                cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            registrado = true;
        }
        catch
        {
            // A chave segue na mensagem ao operador.
        }

        var protocolo = string.IsNullOrWhiteSpace(resultado.Protocolo) ? "sem protocolo" : resultado.Protocolo;
        var aviso = resultado.Autorizada
            ? $"NFC-e autorizada (chave {resultado.Chave}, protocolo {protocolo}). A venda local não foi concluída. "
            : $"NFC-e em contingência (chave {resultado.Chave}). A venda local não foi concluída. ";
        aviso += registrado
            ? "O documento fiscal foi registrado. "
            : "O documento fiscal não pôde ser registrado. ";

        if (falha is DomainException domain)
            return aviso + domain.Message;

        return aviso + "A gravação local falhou. " + falha.Message;
    }

    private (IQueryable<DocumentoFiscal> Query, string? Vazio) FiltrarGestao(NfceGestaoConsulta consulta)
    {
        IQueryable<DocumentoFiscal> query = _db.DocumentosFiscais.Where(item => item.DeletedAt == null);

        if (consulta.SomenteCaixaAberto)
        {
            if (!_session.HasOpenCashRegister || _session.OpenCashRegister is null)
                return (query, "Nenhum caixa em operação.");

            var caixaId = _session.OpenCashRegister.Id;
            var vendasDoCaixa = _db.Sales
                .Where(sale => sale.CashRegisterId == caixaId && sale.DeletedAt == null)
                .Select(sale => sale.Id)
                .ToList();
            if (vendasDoCaixa.Count == 0)
                return (query.Where(item => item.Id == Guid.Empty), null);

            query = query.Where(item => item.SaleId != null && vendasDoCaixa.Contains(item.SaleId.Value));
        }

        if (consulta.FromUtc is DateTime from)
            query = query.Where(item => item.EmitidoEm >= from);
        if (consulta.ToUtcExclusive is DateTime to)
            query = query.Where(item => item.EmitidoEm < to);
        if (consulta.Status is StatusDocumentoFiscal status)
            query = query.Where(item => item.Status == status);

        if (!string.IsNullOrWhiteSpace(consulta.Ticket))
        {
            var term = consulta.Ticket.Trim().ToLower();
            var vendas = _db.Sales
                .Where(sale => sale.DeletedAt == null && sale.TicketNumber.ToLower().Contains(term))
                .Select(sale => sale.Id)
                .ToList();
            if (vendas.Count == 0)
                return (query.Where(item => item.Id == Guid.Empty), null);

            query = query.Where(item => item.SaleId != null && vendas.Contains(item.SaleId.Value));
        }

        return (query, null);
    }

    private List<NfceGestaoItemDto> MapearGestao(List<DocumentoFiscal> documentos)
    {
        var saleIds = documentos.Where(item => item.SaleId.HasValue).Select(item => item.SaleId!.Value).Distinct().ToList();
        var sales = saleIds.Count == 0
            ? new Dictionary<Guid, Sale>()
            : _db.Sales.Where(sale => saleIds.Contains(sale.Id)).ToDictionary(sale => sale.Id);

        return documentos.Select(documento =>
        {
            Sale? sale = null;
            if (documento.SaleId is Guid saleId)
                sales.TryGetValue(saleId, out sale);

            var vendaConcluida = sale?.Status == SaleStatus.Completed;
            var temXml = !string.IsNullOrWhiteSpace(documento.Xml);
            var temChave = !string.IsNullOrWhiteSpace(documento.Chave);
            var temProtocolo = !string.IsNullOrWhiteSpace(documento.Protocolo);
            var emitido = documento.EmitidoEm.Kind == DateTimeKind.Utc
                ? documento.EmitidoEm.ToLocalTime()
                : documento.EmitidoEm;

            return new NfceGestaoItemDto(
                documento.Id,
                documento.SaleId,
                $"{documento.Serie}/{documento.Numero}",
                emitido,
                RotuloStatus(documento.Status),
                documento.Status,
                sale?.TicketNumber ?? "—",
                sale?.Total,
                documento.Chave,
                documento.Protocolo,
                documento.CStat,
                documento.Motivo,
                documento.Status == StatusDocumentoFiscal.Contingencia && !temProtocolo && temXml && vendaConcluida,
                temXml,
                vendaConcluida && documento.Status is StatusDocumentoFiscal.Autorizada or StatusDocumentoFiscal.Contingencia or StatusDocumentoFiscal.Cancelada,
                documento.Status == StatusDocumentoFiscal.Autorizada && vendaConcluida && temChave && temProtocolo,
                documento.Status == StatusDocumentoFiscal.Contingencia && vendaConcluida);
        }).ToList();
    }

    private static string RotuloStatus(StatusDocumentoFiscal status) => status switch
    {
        StatusDocumentoFiscal.Autorizada => "Autorizada",
        StatusDocumentoFiscal.Contingencia => "Contingência",
        StatusDocumentoFiscal.Rejeitada => "Rejeitada",
        StatusDocumentoFiscal.Cancelada => "Cancelada",
        _ => status.ToString()
    };

    private DocumentoFiscal? ObterDocumentoDaVenda(Guid saleId)
        => _db.DocumentosFiscais
            .Where(item => item.SaleId == saleId && item.DeletedAt == null)
            .OrderByDescending(item => item.EmitidoEm)
            .FirstOrDefault();

    private static DesfechoTransmissao AplicarTransmissao(DocumentoFiscal documento, ResultadoEmissaoNfce resultado)
    {
        if (resultado.SemComunicacao)
        {
            documento.RegistrarFalhaTransmissao(resultado.Status, resultado.Motivo);
            return DesfechoTransmissao.Pendente;
        }

        if (resultado.Autorizada || (resultado.Status == "204" && !string.IsNullOrWhiteSpace(resultado.Protocolo)))
        {
            documento.ConfirmarTransmissao(resultado.Protocolo, resultado.Status, resultado.Motivo, resultado.Xml);
            return DesfechoTransmissao.Autorizada;
        }

        documento.MarcarRejeitada(resultado.Status, resultado.Motivo);
        return DesfechoTransmissao.Rejeitada;
    }

    private static string TextoMotivo(string? motivo)
        => string.IsNullOrWhiteSpace(motivo) ? "sem motivo informado" : motivo;

    private async Task RegistrarRejeicaoAsync(
        int serie,
        int numero,
        string status,
        string motivo,
        string? chave,
        string? xml,
        CancellationToken cancellationToken)
    {
        var documento = DocumentoFiscal.Criar(
            serie,
            numero,
            1,
            StatusDocumentoFiscal.Rejeitada,
            DateTime.UtcNow,
            chave,
            null,
            status,
            motivo,
            null,
            null,
            xml);
        _db.Add(documento);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private PedidoEmissaoNfce MontarPedido(
        Sale sale,
        ConfiguracaoFiscal config,
        CredenciaisEmissao credenciais,
        int numero,
        string? cpf)
    {
        var brutos = sale.Items.Select(item => decimal.Round(item.Quantity * item.UnitPrice, 2)).ToList();
        var rateio = Ratear(brutos, sale.DiscountAmount, sale.SurchargeAmount);
        var itens = new List<ItemEmissao>();
        var indice = 0;
        foreach (var item in sale.Items)
        {
            var produto = _db.Products.First(p => p.Id == item.ProductId);
            var (desconto, acrescimo) = rateio[indice++];
            itens.Add(new ItemEmissao(
                item.ProductSku,
                item.ProductName,
                produto.Barcode,
                produto.Ncm,
                produto.Cest,
                produto.Cfop,
                produto.Origem,
                produto.Csosn,
                produto.PisCst,
                produto.CofinsCst,
                produto.IbsCbsCst,
                produto.CClassTrib,
                produto.UnitOfMeasure,
                item.Quantity,
                item.UnitPrice,
                desconto,
                acrescimo));
        }

        var pagamentos = sale.Payments.Select(pagamento =>
        {
            var forma = _db.FormasPagamento.First(item => item.Id == pagamento.FormaPagamentoId);
            var codigo = string.IsNullOrWhiteSpace(forma.CodigoTpag) ? "99" : forma.CodigoTpag.Trim();
            return new PagamentoEmissao(codigo, pagamento.Amount, pagamento.Descricao);
        }).ToList();

        return new PedidoEmissaoNfce(
            new EmitenteEmissao(
                config.RazaoSocial,
                config.NomeFantasia,
                config.Cnpj,
                config.InscricaoEstadual,
                config.Crt,
                config.Logradouro,
                config.Numero,
                config.Bairro,
                config.CodigoMunicipio,
                config.Municipio,
                config.Cep,
                config.Telefone),
            credenciais,
            config.Serie,
            numero,
            DateTimeOffset.Now,
            cpf,
            itens,
            pagamentos,
            sale.ChangeAmount,
            config.JustificativaContingencia);
    }

    private void ValidarItens(IReadOnlyList<CartItemDto> cartItems)
    {
        foreach (var item in cartItems)
        {
            var produto = _db.Products.FirstOrDefault(p => p.Id == item.ProductId)
                ?? throw new DomainException($"Produto {item.ProductName} não encontrado.");
            if (produto.Ncm.Length != 8)
                throw new DomainException($"Informe o NCM do produto {produto.Name} antes de emitir a NFC-e.");
            if (!CsosnSuportados.Contains(produto.Csosn))
                throw new DomainException($"CSOSN {produto.Csosn} de {produto.Name} não é suportado. Use 102, 103, 300 ou 400.");
            try
            {
                _emissor.ValidarIbsCbs(DateTimeOffset.Now, produto.IbsCbsCst, produto.CClassTrib);
            }
            catch (Exception ex) when (ex is not DomainException)
            {
                throw new DomainException($"{produto.Name}: {ex.Message}");
            }
        }
    }

    private void ValidarPagamentos(IReadOnlyList<PaymentDraftDto> payments)
    {
        foreach (var pagamento in payments)
        {
            var forma = _db.FormasPagamento.FirstOrDefault(item => item.Id == pagamento.FormaPagamentoId && item.IsEnable)
                ?? throw new DomainException("Forma de pagamento indisponível.");
            var codigo = new string((forma.CodigoTpag ?? string.Empty).Where(char.IsDigit).ToArray());
            if (codigo.Length is < 1 or > 2)
                throw new DomainException($"A forma {forma.Descricao} não tem código SEFAZ (tPag).");
        }
    }

    private CredenciaisEmissao Credenciais(ConfiguracaoFiscal config)
    {
        var senha = _protecao.Revelar(config.CertificadoSenhaProtegida!);
        return new CredenciaisEmissao(
            config.Uf,
            config.Homologacao,
            config.CertificadoArquivo!,
            senha,
            config.Csc,
            config.IdTokenCsc);
    }

    private ConfiguracaoFiscal ObterObrigatoria()
        => ObterConfiguracao() ?? throw new DomainException("Salve a configuração fiscal antes de emitir a NFC-e.");

    private ConfiguracaoFiscal? ObterConfiguracao()
    {
        var storeId = _session.CurrentStore?.Id;
        var query = _db.ConfiguracoesFiscais.Where(item => item.DeletedAt == null);
        if (storeId.HasValue)
            query = query.Where(item => item.StoreId == storeId.Value);

        return query.FirstOrDefault();
    }

    private static ConfiguracaoFiscalDto Mapear(ConfiguracaoFiscal config) => new(
        config.Id,
        config.StoreId,
        config.RazaoSocial,
        config.NomeFantasia,
        config.Cnpj,
        config.InscricaoEstadual,
        config.Crt,
        config.Logradouro,
        config.Numero,
        config.Bairro,
        config.CodigoMunicipio,
        config.Municipio,
        config.Uf,
        config.Cep,
        config.Telefone,
        config.Csc,
        config.IdTokenCsc,
        config.Homologacao,
        config.Serie,
        config.ProximoNumero,
        config.CertificadoArquivo,
        !string.IsNullOrWhiteSpace(config.CertificadoSenhaProtegida),
        config.JustificativaContingencia,
        config.EmitirPorPadrao);

    private static string? NormalizarCpf(string? cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf))
            return null;

        var digitos = new string(cpf.Where(char.IsDigit).ToArray());
        if (digitos.Length != 11 || digitos.Distinct().Count() == 1 || !CpfValido(digitos))
            throw new DomainException("CPF do consumidor inválido.");

        return digitos;
    }

    private static bool CpfValido(string cpf)
    {
        var primeiro = Digito(cpf, 9, 10);
        var segundo = Digito(cpf, 10, 11);
        return cpf[9] == (char)('0' + primeiro) && cpf[10] == (char)('0' + segundo);
    }

    private static int Digito(string cpf, int tamanho, int pesoInicial)
    {
        var soma = 0;
        for (var i = 0; i < tamanho; i++)
            soma += (cpf[i] - '0') * (pesoInicial - i);

        var resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }

    private static IReadOnlyList<(decimal Desconto, decimal Acrescimo)> Ratear(
        IReadOnlyList<decimal> brutos,
        decimal desconto,
        decimal acrescimo)
    {
        var resultado = new List<(decimal, decimal)>();
        var baseTotal = brutos.Sum();
        decimal descontoUsado = 0;
        decimal acrescimoUsado = 0;
        for (var i = 0; i < brutos.Count; i++)
        {
            if (i == brutos.Count - 1)
            {
                resultado.Add((decimal.Round(desconto - descontoUsado, 2), decimal.Round(acrescimo - acrescimoUsado, 2)));
                break;
            }

            var parteDesconto = baseTotal == 0 ? 0 : decimal.Round(desconto * brutos[i] / baseTotal, 2);
            var parteAcrescimo = baseTotal == 0 ? 0 : decimal.Round(acrescimo * brutos[i] / baseTotal, 2);
            descontoUsado += parteDesconto;
            acrescimoUsado += parteAcrescimo;
            resultado.Add((parteDesconto, parteAcrescimo));
        }

        return resultado;
    }

    private enum DesfechoTransmissao
    {
        Autorizada,
        Rejeitada,
        Pendente
    }
}
