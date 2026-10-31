---
name: Cursor do catálogo
overview: O produto não desce porque o PDV pede alterações posteriores a um horário 3 horas à frente. SyncStatus = 1 é o esperado e não entra nesse filtro. A correção envia o cursor em UTC e, uma vez, baixa a loja inteira.
todos:
  - id: cursor-utc
    content: Enviar CatalogoLidoEm como UTC, sem ToUniversalTime em valor Unspecified.
    status: completed
  - id: leitura-completa
    content: Zerar CatalogoLidoEm uma vez para a próxima descida trazer a loja inteira.
    status: completed
  - id: teste-desde
    content: Testar que o filtro desde exclui o produto antigo e inclui o mais recente.
    status: completed
isProject: false
---

# Cursor do catálogo

`SyncStatus = 1` é `Synced`. O Swagger grava assim de propósito em `AplicarTrilhaCentral`. A descida filtra `LojaId` e `UpdatedAt`, em [CatalogoService.ObterAsync](src/AcaiPos.Api/Services/CatalogoService.cs).

O SQLite deste PDV já tem inscrição completa na loja `B2B5BFFE-6EC0-4384-A5D7-6C0539B75F65`, nenhuma venda aberta e `CatalogoLidoEm = 2026-10-30 22:22:43`. Esse horário foi gravado em UTC sem fuso. Em [SyncHttpTransporte.ObterCatalogoAsync](src/AcaiPos.Desktop/Sync/SyncHttpTransporte.cs) o envio chama `ToUniversalTime()`. No Windows isso trata o valor como horário de Brasília e pede alterações depois de `2026-10-31 01:22Z`. O produto fica de fora. O ciclo então avança o cursor, então um reinício continua sem vê-lo.

O `LojaId` do produto no MySQL precisa ser o da inscrição. Com outro `LojaId`, a loja inteira também não o traz.

## Correção

- Formatar `desde` com `DateTimeKind.Utc`, sem converter de novo um valor que já é UTC.
- Na subida do PDV, zerar `CatalogoLidoEm` uma única vez. A loja remota já tem cadastro, a semeadura responde 409 e o ciclo baixa tudo, sem `desde`. O cursor volta a valer nos ciclos seguintes.
- Cobrir o filtro `desde` na API: um produto anterior ao cursor fica de fora; um posterior desce.
