using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AcaiPos.Application.Sync;

namespace AcaiPos.Desktop.Sync;

public sealed class SyncHttpTransporte : ISyncTransporte
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private string? _token;
    private Guid _terminalId;
    private DateTime _expiraUtc;

    public async Task<ResultadoEnvio> EnviarAsync(
        string enderecoApi,
        Guid terminalId,
        string segredo,
        CorpoLote corpo,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var token = await ObterTokenAsync(enderecoApi, terminalId, segredo, forcar: false, cancellationToken);
            if (token.Falha is not null)
                return token.Falha;

            using var primeira = await PostarLoteAsync(enderecoApi, token.Valor!, corpo, cancellationToken);
            if (primeira.StatusCode != HttpStatusCode.Unauthorized)
                return await InterpretarAsync(primeira, cancellationToken);

            var renovado = await ObterTokenAsync(enderecoApi, terminalId, segredo, forcar: true, cancellationToken);
            if (renovado.Falha is not null)
                return renovado.Falha;

            using var segunda = await PostarLoteAsync(enderecoApi, renovado.Valor!, corpo, cancellationToken);
            if (segunda.StatusCode == HttpStatusCode.Unauthorized)
                return ResultadoEnvio.Transitorio(401, "Token recusado.");

            return await InterpretarAsync(segunda, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return ResultadoEnvio.Transitorio(null, "Falha de rede.");
        }
    }

    private async Task<(string? Valor, ResultadoEnvio? Falha)> ObterTokenAsync(
        string enderecoApi,
        Guid terminalId,
        string segredo,
        bool forcar,
        CancellationToken cancellationToken)
    {
        if (!forcar && _token is not null && _terminalId == terminalId && DateTime.UtcNow < _expiraUtc)
            return (_token, null);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{enderecoApi}/api/tokens");
        request.Content = Json(new { terminalId, segredo });
        using var resposta = await _http.SendAsync(request, cancellationToken);
        if (!resposta.IsSuccessStatusCode)
        {
            _token = null;
            return (null, ResultadoEnvio.Transitorio((int)resposta.StatusCode, "Falha ao obter token."));
        }

        var corpo = await JsonSerializer.DeserializeAsync<TokenResposta>(
            await resposta.Content.ReadAsStreamAsync(cancellationToken),
            JsonLote.Opcoes,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(corpo?.AccessToken))
        {
            _token = null;
            return (null, ResultadoEnvio.Transitorio((int)resposta.StatusCode, "Falha ao obter token."));
        }

        _token = corpo.AccessToken;
        _terminalId = terminalId;
        _expiraUtc = DateTime.UtcNow.AddSeconds(Math.Max(0, corpo.ExpiresIn - 30));
        return (_token, null);
    }

    private async Task<HttpResponseMessage> PostarLoteAsync(
        string enderecoApi,
        string token,
        CorpoLote corpo,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{enderecoApi}/api/lotes");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = Json(corpo);
        return await _http.SendAsync(request, cancellationToken);
    }

    private static async Task<ResultadoEnvio> InterpretarAsync(HttpResponseMessage resposta, CancellationToken cancellationToken)
    {
        if (resposta.StatusCode == HttpStatusCode.OK)
            return ResultadoEnvio.Aceito();

        if (resposta.StatusCode == HttpStatusCode.BadRequest)
            return ResultadoEnvio.Recusado(400, await LerMensagemAsync(resposta, cancellationToken));

        var status = (int)resposta.StatusCode;
        var mensagem = status >= 500 ? "Falha de rede." : "Falha ao enviar o lote.";
        return ResultadoEnvio.Transitorio(status, mensagem);
    }

    private static async Task<string> LerMensagemAsync(HttpResponseMessage resposta, CancellationToken cancellationToken)
    {
        try
        {
            var json = await resposta.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(json))
                return "Lote recusado.";

            using var documento = JsonDocument.Parse(json);
            if (documento.RootElement.TryGetProperty("mensagem", out var mensagem)
                && mensagem.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(mensagem.GetString()))
                return mensagem.GetString()!;
        }
        catch (JsonException)
        {
        }

        return "Lote recusado.";
    }

    private static StringContent Json<T>(T corpo)
        => new(JsonSerializer.Serialize(corpo, JsonLote.Opcoes), Encoding.UTF8, "application/json");

    public async Task<ResultadoSemeadura> SemearCatalogoAsync(
        string enderecoApi,
        Guid terminalId,
        string segredo,
        SemeaduraRemota corpo,
        CancellationToken cancellationToken = default)
    {
        using var resposta = await EnviarJsonAsync(enderecoApi, terminalId, segredo, "/api/catalogo/semeadura", corpo, cancellationToken);
        if (resposta is null)
            return new ResultadoSemeadura(TipoSemeadura.Falha, "Falha de rede.");
        if (resposta.StatusCode == HttpStatusCode.OK)
            return new ResultadoSemeadura(TipoSemeadura.Aceita, null);
        if (resposta.StatusCode == HttpStatusCode.Conflict)
            return new ResultadoSemeadura(TipoSemeadura.JaExiste, "Loja já possui cadastro.");
        return new ResultadoSemeadura(TipoSemeadura.Falha, await LerMensagemAsync(resposta, cancellationToken));
    }

    public async Task<CatalogoRecebido?> ObterCatalogoAsync(
        string enderecoApi,
        Guid terminalId,
        string segredo,
        DateTime? desde,
        CancellationToken cancellationToken = default)
    {
        var token = await ObterTokenAsync(enderecoApi, terminalId, segredo, forcar: false, cancellationToken);
        if (token.Falha is not null)
            return null;

        var caminho = desde is null
            ? "/api/catalogo"
            : "/api/catalogo?desde=" + Uri.EscapeDataString(FormatarDesde(desde.Value));
        using var request = new HttpRequestMessage(HttpMethod.Get, enderecoApi + caminho);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Valor);
        using var resposta = await _http.SendAsync(request, cancellationToken);
        if (resposta.StatusCode == HttpStatusCode.Unauthorized)
        {
            token = await ObterTokenAsync(enderecoApi, terminalId, segredo, forcar: true, cancellationToken);
            if (token.Falha is not null)
                return null;
            using var renovado = new HttpRequestMessage(HttpMethod.Get, enderecoApi + caminho);
            renovado.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Valor);
            using var segunda = await _http.SendAsync(renovado, cancellationToken);
            if (!segunda.IsSuccessStatusCode)
                return null;
            return await JsonSerializer.DeserializeAsync<CatalogoRecebido>(
                await segunda.Content.ReadAsStreamAsync(cancellationToken),
                JsonLote.Opcoes,
                cancellationToken);
        }

        if (!resposta.IsSuccessStatusCode)
            return null;

        return await JsonSerializer.DeserializeAsync<CatalogoRecebido>(
            await resposta.Content.ReadAsStreamAsync(cancellationToken),
            JsonLote.Opcoes,
            cancellationToken);
    }

    public async Task<ResultadoPreco> EnviarPrecoAsync(
        string enderecoApi,
        Guid terminalId,
        string segredo,
        PrecoRemoto preco,
        CancellationToken cancellationToken = default)
    {
        using var resposta = await EnviarJsonAsync(enderecoApi, terminalId, segredo, "/api/catalogo/precos", preco, cancellationToken);
        if (resposta is null)
            return new ResultadoPreco(false, null, null, "Falha de rede.");
        if (resposta.StatusCode == HttpStatusCode.OK)
        {
            var corpo = await JsonSerializer.DeserializeAsync<PrecoRemoto>(
                await resposta.Content.ReadAsStreamAsync(cancellationToken),
                JsonLote.Opcoes,
                cancellationToken);
            return new ResultadoPreco(true, corpo, 200, null);
        }

        var mensagem = await LerMensagemAsync(resposta, cancellationToken);
        var definitiva = resposta.StatusCode == HttpStatusCode.BadRequest;
        return new ResultadoPreco(false, null, (int)resposta.StatusCode, definitiva ? mensagem : "Falha de rede.");
    }

    private async Task<HttpResponseMessage?> EnviarJsonAsync<T>(
        string enderecoApi,
        Guid terminalId,
        string segredo,
        string caminho,
        T corpo,
        CancellationToken cancellationToken)
    {
        var token = await ObterTokenAsync(enderecoApi, terminalId, segredo, forcar: false, cancellationToken);
        if (token.Falha is not null)
            return null;

        using var request = new HttpRequestMessage(HttpMethod.Post, enderecoApi + caminho);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Valor);
        request.Content = Json(corpo);
        var resposta = await _http.SendAsync(request, cancellationToken);
        if (resposta.StatusCode != HttpStatusCode.Unauthorized)
            return resposta;

        resposta.Dispose();
        token = await ObterTokenAsync(enderecoApi, terminalId, segredo, forcar: true, cancellationToken);
        if (token.Falha is not null)
            return null;

        using var renovado = new HttpRequestMessage(HttpMethod.Post, enderecoApi + caminho);
        renovado.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Valor);
        renovado.Content = Json(corpo);
        return await _http.SendAsync(renovado, cancellationToken);
    }

    private static string FormatarDesde(DateTime valor)
    {
        var utc = valor.Kind == DateTimeKind.Local
            ? valor.ToUniversalTime()
            : DateTime.SpecifyKind(valor, DateTimeKind.Utc);
        return utc.ToString("o");
    }

    private sealed record TokenResposta(string? AccessToken, int ExpiresIn);
}
