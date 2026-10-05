# AcaiPos

PDV desktop offline-first (WPF + .NET 8 + SQLite), alinhado à especificação funcional do projeto.

## Stack

- **AcaiPos.Domain** — entidades e regras de negócio
- **AcaiPos.Application** — casos de uso (auth, catálogo, caixa, venda)
- **AcaiPos.Infrastructure** — EF Core + SQLite, hash de PIN, seed
- **AcaiPos.Desktop** — UI WPF touch-first

## Como rodar

```bash
dotnet run --project src/AcaiPos.Desktop
```

Banco local: `%LocalAppData%\AcaiPos\acai-pos.db`

### Login seed

| Usuário   | PIN  | Perfil         |
|-----------|------|----------------|
| operador  | 1234 | Operador       |
| gerente   | 1234 | Gerente        |
| admin     | 1234 | Administrador  |

## Fluxo MVP atual

1. Login com PIN
2. Abrir caixa (fundo de troco)
3. Montar venda (categorias + produtos + busca/código de barras)
4. Desconto (com autorização se necessário)
5. Pagamento único ou misto (PIX/cartão/dinheiro + troco)
6. Ticket + reimpressão
7. Sangria / suprimento
8. Fechar caixa
9. Histórico + resumo do dia
10. Cancelamento com permissão
11. Cadastros (produtos/categorias) e usuários (admin)
