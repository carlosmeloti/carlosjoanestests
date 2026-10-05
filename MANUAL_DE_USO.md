# Manual de Uso — AcaiPos

**Versão do manual:** 1.0  
**Produto:** AcaiPos — PDV desktop touch-first, offline-first  
**Público:** operadores de caixa, gerentes e administradores  

---

## Sumário

1. [O que é o AcaiPos](#1-o-que-é-o-acaipos)
2. [Como iniciar o sistema](#2-como-iniciar-o-sistema)
3. [Perfis e permissões](#3-perfis-e-permissões)
4. [Login e logout](#4-login-e-logout)
5. [Barra superior](#5-barra-superior)
6. [Abertura de caixa](#6-abertura-de-caixa)
7. [Tela de venda](#7-tela-de-venda)
8. [Pagamento](#8-pagamento)
9. [Ticket e reimpressão](#9-ticket-e-reimpressão)
10. [Sangria e suprimento](#10-sangria-e-suprimento)
11. [Fechamento de caixa](#11-fechamento-de-caixa)
12. [Histórico de vendas](#12-histórico-de-vendas)
13. [Cancelamento de venda](#13-cancelamento-de-venda)
14. [Cadastro de produtos e categorias](#14-cadastro-de-produtos-e-categorias)
15. [Cadastro de usuários](#15-cadastro-de-usuários)
16. [Dados iniciais (seed)](#16-dados-iniciais-seed)
17. [Operação offline](#17-operação-offline)
18. [Boas práticas](#18-boas-práticas)
19. [Problemas comuns](#19-problemas-comuns)
20. [O que ainda não faz parte deste MVP](#20-o-que-ainda-não-faz-parte-deste-mvp)

---

## 1. O que é o AcaiPos

O AcaiPos é um **ponto de venda (PDV)** para loja física, pensado para terminal touch:

- venda rápida no balcão
- abertura e fechamento de caixa
- pagamentos (incluindo pagamento misto)
- ticket e reimpressão
- histórico e cancelamento
- cadastros de produtos, categorias e usuários
- funcionamento **local**, mesmo sem internet

Os dados ficam no computador do terminal. A sincronização com nuvem/API e a integração com iFood **não** entram neste MVP.

---

## 2. Como iniciar o sistema

### Desenvolvimento / instalação local

No diretório do projeto:

```bash
dotnet run --project src/AcaiPos.Desktop
```

### Banco de dados

Arquivo local:

`%LocalAppData%\AcaiPos\acai-pos.db`

Na primeira execução o sistema cria o banco e carrega os dados iniciais (usuários, categorias e produtos de exemplo).

---

## 3. Perfis e permissões

| Perfil | Login seed | O que pode fazer |
|--------|------------|------------------|
| **Operador** | `operador` | Vender, caixa, tickets, histórico. Cancelamento/desconto só com autorização de gerente/admin |
| **Gerente** | `gerente` | Tudo do operador + cadastros de produtos/categorias + cancelar/descontar direto |
| **Administrador** | `admin` | Tudo do gerente + cadastro de usuários |

PIN inicial de todos: **`1234`**

> Altere os PINs em produção assim que possível (tela **Usuários**, com login admin).

---

## 4. Login e logout

### Entrar

1. Informe o **usuário**
2. Informe o **PIN** (teclado numérico na tela ou teclado físico)
3. Toque em **ENTRAR**

### Sair

Na barra superior, toque em **Sair**.

A sessão encerra. O caixa aberto **não** é fechado automaticamente — feche o caixa antes de sair, se for o fim do turno.

---

## 5. Barra superior

Elementos principais:

| Elemento | Função |
|----------|--------|
| **ACAIPOS** | Identidade do sistema |
| Status do caixa | `CAIXA ABERTO` / `CAIXA FECHADO` |
| **Venda** | Volta para a tela de venda |
| **Histórico** | Consulta de vendas |
| **Cadastros** | Produtos e categorias (gerente/admin) |
| **Usuários** | Gestão de usuários (somente admin) |
| **Tickets** | Lista rápida dos tickets do dia |
| Operador / loja / terminal | Contexto da sessão |
| **OFFLINE-LOCAL** | Indica operação local |
| Relógio | Hora atual |
| **Abrir caixa** / **Fechar caixa** | Ciclo de caixa |
| **Suprimento** / **Sangria** | Movimentações (caixa aberto) |
| **Sair** | Logout |

---

## 6. Abertura de caixa

Sem caixa aberto **não é possível vender**.

1. Faça login
2. Se não houver caixa aberto, o sistema sugere a abertura
3. Informe o **fundo de troco** (valor inicial em dinheiro)
4. Confirme **ABRIR CAIXA**

Regras:

- Só pode haver **um caixa aberto** por terminal
- O valor de abertura entra no cálculo do fechamento

---

## 7. Tela de venda

É a tela principal do operador.

### 7.1 Localizar produto

Formas disponíveis:

- toque no produto
- filtro por **categoria**
- busca por nome / SKU
- **código de barras**: digite ou use o leitor no campo de busca e pressione **Enter** (ou **Buscar**)

Se o código de barras existir, o produto entra direto no carrinho.

### 7.2 Carrinho

No carrinho você pode:

- aumentar / diminuir quantidade (`+` / `−`)
- limpar o carrinho (**Limpar**)
- ver subtotal e total
- iniciar o pagamento (**FINALIZAR VENDA**)

### 7.3 Estoque

Ao concluir a venda, o estoque do produto é baixado automaticamente.  
No cancelamento, o estoque é revertido.

---

## 8. Pagamento

Ao tocar em **FINALIZAR VENDA**, abre o painel de pagamento.

### 8.1 Desconto

1. Informe o valor do **desconto**
2. Se o usuário logado for **operador**, o sistema pede **autorização** (usuário + PIN de gerente/admin)
3. O total a pagar é recalculado

### 8.2 Pagamento único (restante)

Use os botões **Pagar restante …** para quitar tudo de uma vez, por exemplo:

- Pagar restante PIX
- Pagar restante Dinheiro

### 8.3 Pagamento misto (parcelas)

Exemplo: venda de R$ 100,00

1. Em **Valor desta parcela**, informe `40,00`
2. Toque em **PIX (parcela)**
3. O restante passa a ser R$ 60,00
4. Informe `60,00` (ou use pagar restante)
5. Toque em **Dinheiro (parcela)** ou **Pagar restante Dinheiro**
6. Se for dinheiro, informe o **valor recebido** para calcular o **troco**

O sistema só conclui quando a soma das parcelas for igual ao total da venda.

### 8.4 Dinheiro e troco

- Informe o valor da parcela em dinheiro
- Informe o **valor recebido**
- O sistema mostra o troco da parcela
- Não permite valor recebido menor que a parcela

### 8.5 Formas de pagamento do MVP

- Dinheiro
- Cartão de débito
- Cartão de crédito
- PIX

### 8.6 Após concluir

A venda é gravada localmente, o estoque é atualizado e o **ticket** é exibido.

---

## 9. Ticket e reimpressão

### 9.1 Após a venda

O ticket aparece em tela com:

- loja / terminal
- número do ticket
- data/hora
- operador
- itens, quantidades e preços
- descontos
- total
- pagamentos e troco

Ações:

- **Imprimir** — abre o diálogo de impressão do Windows
- **Nova venda** — fecha o ticket e volta ao caixa

### 9.2 Reimpressão

Caminhos:

1. Barra superior → **Tickets** → lista do dia → **Ver** ou **Reimprimir**
2. **Histórico** → selecionar venda → **Ver ticket** / **Reimprimir**

Reimpressões aparecem marcadas como `*** REIMPRESSAO ***`.  
Vendas canceladas aparecem como `*** CANCELADA ***`.

---

## 10. Sangria e suprimento

Disponíveis somente com **caixa aberto**.

### Suprimento

Entrada de dinheiro no caixa (ex.: reforço de troco).

1. Toque em **Suprimento**
2. Informe valor e motivo
3. Confirme

### Sangria

Retirada de dinheiro do caixa.

1. Toque em **Sangria**
2. Informe valor e motivo
3. Confirme

O painel também mostra o histórico de movimentações do caixa atual e o resumo esperado em dinheiro.

---

## 11. Fechamento de caixa

1. Toque em **Fechar caixa**
2. Confira o resumo:
   - abertura
   - vendas em dinheiro
   - suprimentos
   - sangrias
   - valor esperado
3. Informe o **valor contado** em dinheiro
4. Confirme

O sistema calcula:

```text
Diferença = Valor contado − Valor esperado
```

Depois do fechamento, é necessário abrir um novo caixa para continuar vendendo.

---

## 12. Histórico de vendas

Menu **Histórico**.

### Filtros

- período (**De** / **Até**)
- atalhos **Hoje** e **7 dias**
- operador
- terminal
- status (todas / concluídas / canceladas)
- forma de pagamento
- busca por número do ticket

### Lista

Mostra ticket, data, operador, terminal, status, pagamentos e total.

### Detalhe

Ao selecionar uma venda, o painel lateral mostra os dados completos.  
Se estiver cancelada, mostra quem cancelou, quando e o motivo.

### Resumo do dia

Botão **Resumo do dia**:

- quantidade de vendas e cancelamentos
- bruto, descontos e líquido
- totais por forma de pagamento
- totais por operador

---

## 13. Cancelamento de venda

Somente vendas **concluídas** podem ser canceladas.

### Como cancelar

1. Abra **Histórico**
2. Localize a venda
3. Toque em **Cancelar** (lista) ou **Cancelar venda** (detalhe)
4. Informe o **motivo** (obrigatório)
5. Confirme

### Permissão

| Usuário logado | O que acontece |
|----------------|----------------|
| Gerente / Admin | Cancela direto |
| Operador | Precisa de usuário + PIN de gerente/admin |

### Efeitos do cancelamento

- a venda **não é apagada** (fica com status Cancelada)
- estoque é **revertido**
- operação é **auditada**
- quem autorizou/cancelou fica registrado

---

## 14. Cadastro de produtos e categorias

Menu **Cadastros** (gerente ou admin).

### Abas

- **Produtos**
- **Categorias**

### Produtos

É possível:

- criar / editar
- informar nome, SKU, código de barras, categoria, preço, custo, estoque, unidade
- ativar / desativar
- filtrar por busca, categoria e inativos

Produto **inativo** não aparece na tela de venda.

Alteração de estoque no cadastro gera movimentação de ajuste.

### Categorias

É possível:

- criar / editar
- ordenar
- ativar / desativar

Não é permitido desativar categoria que ainda tenha produtos ativos.

---

## 15. Cadastro de usuários

Menu **Usuários** (somente **administrador**).

É possível:

- criar usuário (nome, login, PIN, perfil, ativo)
- editar nome, perfil, status e PIN
- desativar usuário

Perfis disponíveis:

- Operador
- Gerente
- Administrador

Observações:

- o login **não** muda na edição
- deixe o PIN em branco na edição para manter o PIN atual
- não é possível desativar o usuário que está logado

---

## 16. Dados iniciais (seed)

Na primeira execução o sistema cria:

### Usuários

| Usuário | PIN | Perfil |
|---------|-----|--------|
| `operador` | `1234` | Operador |
| `gerente` | `1234` | Gerente |
| `admin` | `1234` | Administrador |

### Loja / terminal

- Loja: `LOJA-01` — Açaí Prime Centro  
- Terminal: `PDV-01` — Terminal Principal  

### Categorias e produtos de exemplo

Categorias: Bowls, Bebidas, Adicionais, Lanches  

Produtos de exemplo incluem açaís, bebidas, adicionais e lanche, já com preços e códigos de barras de demonstração.

---

## 17. Operação offline

O AcaiPos foi desenhado para **não depender de internet** nas operações do caixa:

- login local
- venda
- pagamento
- ticket
- caixa
- histórico
- cadastros

O indicador **OFFLINE-LOCAL** reforça que a operação é no terminal.

> Integrações futuras (API central, iFood, fiscal) poderão exigir conectividade pontual, mas o balcão local já funciona sem rede.

---

## 18. Boas práticas

1. Abra o caixa no início do turno com o fundo de troco correto.
2. Use sangria ao longo do dia para não acumular muito dinheiro na gaveta.
3. Confira o valor contado no fechamento antes de confirmar.
4. Prefira gerente/admin para cancelamentos e descontos sensíveis.
5. Cadastre código de barras nos produtos para acelerar a venda.
6. Troque o PIN padrão (`1234`) em ambiente real.
7. Não desligue o terminal no meio de uma venda sem concluir ou limpar o carrinho.
8. Use o histórico no fim do dia para conferir faturamento e formas de pagamento.

---

## 19. Problemas comuns

| Situação | O que verificar |
|----------|-----------------|
| Não consigo vender | Caixa está aberto? |
| Produto não aparece | Está ativo? Categoria correta? |
| Código de barras não acha | O código está cadastrado no produto? |
| Desconto bloqueado | Operador precisa de autorização de gerente/admin |
| Cancelamento bloqueado | Mesma regra de autorização |
| Botão Cadastros não aparece | Login precisa ser gerente ou admin |
| Botão Usuários não aparece | Login precisa ser admin |
| Diferença no fechamento | Confira sangrias, suprimentos e vendas em dinheiro |
| Impressão não sai | Verifique impressora no diálogo do Windows |

---

## 20. O que ainda não faz parte deste MVP

Itens previstos para fases futuras (não disponíveis nesta versão):

- sincronização com API / nuvem
- múltiplas lojas e múltiplos PDVs sincronizados
- integração **iFood**
- emissão fiscal (ex.: TecnoSpeed / NFC-e)
- programa de fidelidade, CRM, BI avançado

---

## Fluxo rápido do dia (checklist)

1. Ligar o terminal e abrir o AcaiPos  
2. Login do operador  
3. Abrir caixa com fundo de troco  
4. Vender durante o turno  
5. Fazer sangrias/suprimentos quando necessário  
6. Reimprimir tickets se preciso  
7. Consultar histórico / resumo do dia  
8. Fechar caixa com valor contado  
9. Sair do sistema  

---

## Suporte técnico (referência)

| Item | Detalhe |
|------|---------|
| Aplicação | `AcaiPos.Desktop` |
| Banco local | `%LocalAppData%\AcaiPos\acai-pos.db` |
| Stack | .NET 8 + WPF + SQLite |
| Arquitetura | Domain / Application / Infrastructure / Desktop |

---

*Manual gerado para a versão atual do MVP de balcão do AcaiPos.*
