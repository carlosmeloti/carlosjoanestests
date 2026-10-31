---
name: Tela e UpdatedAt
overview: O SQL remoto passa a marcar UpdatedAt quando o próprio comando não mexe nessa coluna. A tela do PDV relê o catálogo quando a descida grava. O UpdatedAt local continua sendo o instante que veio da central.
todos:
  - id: trigger-updatedat
    content: Trigger BEFORE UPDATE que preenche UpdatedAt só quando o comando SQL não alterou essa coluna.
    status: completed
  - id: tela-recarrega
    content: Avisar a tela após a descida, reler as entidades acompanhadas e atualizar venda e cadastro sem reiniciar.
    status: completed
  - id: teste-trigger
    content: Cobrir UPDATE de SalePrice sem UpdatedAt e a presença dessa linha no GET desde.
    status: completed
isProject: false
---

# Tela e UpdatedAt

## O UpdatedAt local permanece o da central

A descida não compara o `UpdatedAt` do PDV com o do MySQL para decidir se busca a linha. O filtro em [CatalogoService.ObterAsync](src/AcaiPos.Api/Services/CatalogoService.cs) é `UpdatedAt > CatalogoLidoEm`, e `CatalogoLidoEm` é o cursor da inscrição, gravado com o horário da leitura. Copiar o `UpdatedAt` remoto para o produto local, em [SincronizadorCatalogo.AplicarAsync](src/AcaiPos.Infrastructure/Sync/SincronizadorCatalogo.cs), é a versão dessa linha. Um preço alterado no terminal continua gravando um `UpdatedAt` novo e sobe. Trocar esse campo para o horário da aplicação faria um terminal que só recebeu o preço parecer mais novo que a central.

## Trigger no remoto

Não existe trigger hoje. O `POST` da API preenche `UpdatedAt`; um `UPDATE` do Workbench que não cita a coluna deixa o valor antigo e a descida não vê a linha.

O `POST` continua gravando o `UpdatedAt` que o cliente enviou. A trigger não assume esse papel. Ela só cobre o SQL que não mexe na coluna. Se toda gravação passasse a usar o horário de chegada no servidor, um terminal offline que editou o preço depois e sincronizou mais tarde perderia para quem chegou primeiro: o instante guardado seria o da recepção, não o da edição.

Em [CatalogoSchema.GarantirAsync](src/AcaiPos.Api/Persistence/CatalogoSchema.cs), criar trigger `BEFORE UPDATE` em `Products`, `Categories` e `FormasPagamento`. Ela só grava `UTC_TIMESTAMP(6)` quando `NEW.UpdatedAt = OLD.UpdatedAt`. Quando a API já mandou um instante, esse instante permanece. No SQLite dos testes, o mesmo critério com `strftime`.

## A tela está com o produto antigo na memória

A descida grava num `PosDbContext` separado. A tela lê o contexto único de [DependencyInjection.cs](src/AcaiPos.Infrastructure/DependencyInjection.cs). O Entity Framework devolve a instância já acompanhada, com o preço antigo. [LoadCatalogAsync](src/AcaiPos.Desktop/ViewModels/MainViewModel.cs) só roda no login e na volta para a venda, então a grade também não pede de novo.

Um aviso disparado quando a descida aplica ao menos uma linha faz a tela, no dispatcher:

- recarregar do SQLite os produtos, categorias e formas já acompanhados
- refazer a grade da venda e os botões de pagamento
- refazer as listas do cadastro sem limpar o formulário aberto; se o produto em edição for o que desceu, o preço e o estoque exibidos acompanham o valor novo

## Teste

No teste da API, um `UPDATE` de `SalePrice` sem citar `UpdatedAt` passa a aparecer num `GET /api/catalogo?desde=` anterior a essa gravação.