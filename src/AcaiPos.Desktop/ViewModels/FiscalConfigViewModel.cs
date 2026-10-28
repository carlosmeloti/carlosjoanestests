using System.Collections.ObjectModel;
using System.IO;
using AcaiPos.Application.Models;
using AcaiPos.Application.Services;
using AcaiPos.Domain.Common;
using Microsoft.Win32;

namespace AcaiPos.Desktop.ViewModels;

public sealed class FiscalConfigViewModel : ObservableObject
{
    private readonly NfceService _nfceService;
    private readonly Action<string>? _notify;

    private string _razaoSocial = string.Empty;
    private string _nomeFantasia = string.Empty;
    private string _cnpj = string.Empty;
    private string _inscricaoEstadual = string.Empty;
    private int _crt = 1;
    private string _logradouro = string.Empty;
    private string _numero = string.Empty;
    private string _bairro = string.Empty;
    private int _codigoMunicipio;
    private string _municipio = string.Empty;
    private string _uf = string.Empty;
    private string _cep = string.Empty;
    private string _telefone = string.Empty;
    private string _csc = string.Empty;
    private int _idTokenCsc = 1;
    private bool _homologacao = true;
    private int _serie = 1;
    private int _proximoNumero = 1;
    private string _certificadoArquivo = string.Empty;
    private string _senhaCertificado = string.Empty;
    private bool _temSenhaSalva;
    private string _justificativaContingencia = "SEFAZ indisponivel no momento da venda. Emissao em contingencia offline.";
    private bool _emitirPorPadrao;
    private string _mensagem = string.Empty;

    public FiscalConfigViewModel(NfceService nfceService, Action<string>? notify = null)
    {
        _nfceService = nfceService;
        _notify = notify;
        SaveCommand = new RelayCommand(async _ => await SalvarAsync());
        TestarSefazCommand = new RelayCommand(async _ => await TestarSefazAsync());
        EscolherCertificadoCommand = new RelayCommand(_ => EscolherCertificado());
        TransmitirCommand = new RelayCommand(async _ => await TransmitirAsync());
        RefreshCommand = new RelayCommand(async _ => await LoadAsync());
    }

    public ObservableCollection<ContingenciaPendenteDto> Pendencias { get; } = [];

    public RelayCommand SaveCommand { get; }
    public RelayCommand TestarSefazCommand { get; }
    public RelayCommand EscolherCertificadoCommand { get; }
    public RelayCommand TransmitirCommand { get; }
    public RelayCommand RefreshCommand { get; }

    public string RazaoSocial { get => _razaoSocial; set => SetProperty(ref _razaoSocial, value); }
    public string NomeFantasia { get => _nomeFantasia; set => SetProperty(ref _nomeFantasia, value); }
    public string Cnpj { get => _cnpj; set => SetProperty(ref _cnpj, value); }
    public string InscricaoEstadual { get => _inscricaoEstadual; set => SetProperty(ref _inscricaoEstadual, value); }
    public int Crt { get => _crt; set => SetProperty(ref _crt, value); }
    public string Logradouro { get => _logradouro; set => SetProperty(ref _logradouro, value); }
    public string Numero { get => _numero; set => SetProperty(ref _numero, value); }
    public string Bairro { get => _bairro; set => SetProperty(ref _bairro, value); }
    public int CodigoMunicipio { get => _codigoMunicipio; set => SetProperty(ref _codigoMunicipio, value); }
    public string Municipio { get => _municipio; set => SetProperty(ref _municipio, value); }
    public string Uf { get => _uf; set => SetProperty(ref _uf, value); }
    public string Cep { get => _cep; set => SetProperty(ref _cep, value); }
    public string Telefone { get => _telefone; set => SetProperty(ref _telefone, value); }
    public string Csc { get => _csc; set => SetProperty(ref _csc, value); }
    public int IdTokenCsc { get => _idTokenCsc; set => SetProperty(ref _idTokenCsc, value); }
    public bool Homologacao { get => _homologacao; set => SetProperty(ref _homologacao, value); }
    public int Serie { get => _serie; set => SetProperty(ref _serie, value); }
    public int ProximoNumero { get => _proximoNumero; set => SetProperty(ref _proximoNumero, value); }
    public string CertificadoArquivo { get => _certificadoArquivo; set => SetProperty(ref _certificadoArquivo, value); }
    public string SenhaCertificado { get => _senhaCertificado; set => SetProperty(ref _senhaCertificado, value); }
    public bool TemSenhaSalva { get => _temSenhaSalva; set => SetProperty(ref _temSenhaSalva, value); }
    public string JustificativaContingencia { get => _justificativaContingencia; set => SetProperty(ref _justificativaContingencia, value); }
    public bool EmitirPorPadrao { get => _emitirPorPadrao; set => SetProperty(ref _emitirPorPadrao, value); }
    public string Mensagem { get => _mensagem; set => SetProperty(ref _mensagem, value); }
    public string SenhaDica => TemSenhaSalva ? "Senha já salva. Preencha só para trocar." : "Informe a senha do certificado A1.";

    public async Task LoadAsync()
    {
        var config = await _nfceService.ObterAsync();
        if (config is not null)
            Aplicar(config);

        Pendencias.Clear();
        foreach (var pendencia in await _nfceService.ListarPendenciasAsync())
            Pendencias.Add(pendencia);
    }

    private async Task SalvarAsync()
    {
        try
        {
            var salva = await _nfceService.SalvarAsync(new SalvarConfiguracaoFiscalRequest(
                RazaoSocial,
                NomeFantasia,
                Cnpj,
                InscricaoEstadual,
                Crt,
                Logradouro,
                Numero,
                Bairro,
                CodigoMunicipio,
                Municipio,
                Uf,
                Cep,
                string.IsNullOrWhiteSpace(Telefone) ? null : Telefone,
                Csc,
                IdTokenCsc,
                Homologacao,
                Serie,
                ProximoNumero,
                string.IsNullOrWhiteSpace(CertificadoArquivo) ? null : CertificadoArquivo,
                string.IsNullOrWhiteSpace(SenhaCertificado) ? null : SenhaCertificado,
                JustificativaContingencia,
                EmitirPorPadrao));
            SenhaCertificado = string.Empty;
            Aplicar(salva);
            Mensagem = "Configuração fiscal salva.";
            _notify?.Invoke(Mensagem);
        }
        catch (DomainException ex)
        {
            Mensagem = ex.Message;
        }
        catch (Exception ex)
        {
            Mensagem = ex.Message;
        }
    }

    private async Task TestarSefazAsync()
    {
        try
        {
            var status = await _nfceService.ConsultarStatusAsync();
            Mensagem = status.SemComunicacao
                ? $"Sem comunicação com a SEFAZ. {status.Motivo}"
                : $"SEFAZ {status.Status}: {status.Motivo}";
            _notify?.Invoke(Mensagem);
        }
        catch (DomainException ex)
        {
            Mensagem = ex.Message;
        }
        catch (Exception ex)
        {
            Mensagem = ex.Message;
        }
    }

    private async Task TransmitirAsync()
    {
        try
        {
            var resultado = await _nfceService.TransmitirPendentesAsync();
            Mensagem = resultado.Mensagem;
            _notify?.Invoke(Mensagem);
            await LoadAsync();
        }
        catch (DomainException ex)
        {
            Mensagem = ex.Message;
        }
        catch (Exception ex)
        {
            Mensagem = ex.Message;
        }
    }

    private void EscolherCertificado()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Certificado A1 (*.pfx)|*.pfx",
            CheckFileExists = true
        };
        if (!string.IsNullOrWhiteSpace(CertificadoArquivo))
        {
            var pasta = Path.GetDirectoryName(CertificadoArquivo);
            if (!string.IsNullOrWhiteSpace(pasta) && Directory.Exists(pasta))
                dialog.InitialDirectory = pasta;
        }

        if (dialog.ShowDialog() == true)
            CertificadoArquivo = dialog.FileName;
    }

    private void Aplicar(ConfiguracaoFiscalDto config)
    {
        RazaoSocial = config.RazaoSocial;
        NomeFantasia = config.NomeFantasia;
        Cnpj = config.Cnpj;
        InscricaoEstadual = config.InscricaoEstadual;
        Crt = config.Crt;
        Logradouro = config.Logradouro;
        Numero = config.Numero;
        Bairro = config.Bairro;
        CodigoMunicipio = config.CodigoMunicipio;
        Municipio = config.Municipio;
        Uf = config.Uf;
        Cep = config.Cep;
        Telefone = config.Telefone ?? string.Empty;
        Csc = config.Csc;
        IdTokenCsc = config.IdTokenCsc;
        Homologacao = config.Homologacao;
        Serie = config.Serie;
        ProximoNumero = config.ProximoNumero;
        CertificadoArquivo = config.CertificadoArquivo ?? string.Empty;
        TemSenhaSalva = config.TemSenhaCertificado;
        JustificativaContingencia = config.JustificativaContingencia;
        EmitirPorPadrao = config.EmitirPorPadrao;
        RaisePropertyChanged(nameof(SenhaDica));
    }
}
