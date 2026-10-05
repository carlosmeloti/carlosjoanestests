# Especificação Funcional — Sistema de PDV (POS)

**Versão:** 1.0  
**Status:** Documento-base para desenvolvimento  
**Objetivo:** Servir como especificação principal para implementação assistida por IA (Cursor), mantendo o projeto simples no MVP, mas preparado para evolução para arquitetura Edge/Offline-First, múltiplos PDVs e integrações externas.

---

# 1. Visão do produto

O sistema será um **Point of Sale (PDV/POS)** enxuto, rápido e altamente orientado à operação por toque (touch screen).

O objetivo principal é permitir que um operador:

1. entre rapidamente no sistema;
2. abra seu caixa;
3. localize produtos de forma rápida;
4. monte uma venda;
5. receba o pagamento;
6. emita/imprima o ticket;
7. consulte movimentações do caixa;
8. encerre o caixa.

O sistema deverá ser concebido desde o início como **Offline-First**:

- a operação do PDV não deve depender continuamente da internet;
- os dados operacionais devem existir localmente;
- quando houver conectividade, os dados poderão ser sincronizados com uma API central;
- a arquitetura deverá permitir futuramente múltiplos PDVs e lojas;
- integrações externas, como iFood e TecnoSpeed, não devem contaminar o núcleo da operação do PDV.

---

# 2. Objetivos

## 2.1 Objetivos do MVP

O MVP deverá permitir:

- cadastro de produtos;
- cadastro de categorias;
- cadastro de operadores/usuários;
- abertura de caixa;
- realização de vendas;
- inclusão e remoção de itens;
- alteração de quantidade;
- aplicação de formas de pagamento;
- cálculo de troco;
- emissão de ticket;
- movimentações de caixa;
- fechamento de caixa;
- consulta básica do histórico de vendas;
- funcionamento local mesmo sem internet.

## 2.2 Objetivos arquiteturais

Mesmo sendo um MVP local, o sistema deverá ser preparado para:

- sincronização futura;
- múltiplos terminais;
- múltiplas lojas;
- banco central/remoto;
- API REST;
- integração com iFood;
- integração fiscal por meio da TecnoSpeed;
- observabilidade;
- autenticação/autorização;
- processamento assíncrono;
- implantação em infraestrutura de nuvem/VPS;
- evolução para recursos Azure relacionados ao estudo do AZ-204.

---

# 3. Princípios do produto

## 3.1 Simplicidade

A operação de venda deve exigir o menor número possível de interações.

## 3.2 Touch First

A interface deve ser projetada para terminal touch:

- botões grandes;
- áreas de toque confortáveis;
- textos legíveis;
- poucos campos;
- feedback visual imediato;
- evitar menus complexos durante a venda.

## 3.3 Offline First

O PDV deve continuar realizando as principais operações mesmo sem internet.

## 3.4 Local First

A operação da venda deve ocorrer no banco local do terminal.

## 3.5 Sync Ready

Todos os dados que possam futuramente ser sincronizados devem ser modelados considerando identidade global e rastreabilidade.

## 3.6 Integrações desacopladas

iFood, TecnoSpeed e outras integrações devem ser módulos/serviços separados do núcleo de vendas.

---

# 4. Escopo funcional do MVP

## 4.1 Autenticação

O sistema deverá possuir uma tela de acesso do operador.

### Requisitos

- Login do operador.
- Senha ou PIN.
- Identificação do operador atual.
- Controle básico de permissões.
- Logout/troca de operador.

### Perfis iniciais

- Administrador
- Operador
- Gerente

As permissões deverão ser configuráveis posteriormente.

---

# 5. Cadastro de produtos

O produto deverá possuir, no mínimo:

- ID;
- nome;
- código interno/SKU;
- código de barras;
- categoria;
- preço de venda;
- custo opcional;
- estoque;
- unidade de medida;
- status ativo/inativo;
- data de criação;
- data de atualização.

### Regras

- Produto inativo não deverá aparecer para venda.
- Código de barras deverá poder ser usado para localizar rapidamente o produto.
- O preço utilizado na venda deverá ser registrado no item da venda.
- Alterações futuras de preço não deverão alterar vendas já realizadas.

---

# 6. Categorias

Cadastro simples de categorias.

Campos:

- ID;
- nome;
- descrição opcional;
- status;
- data de criação;
- data de atualização.

A tela de vendas deverá permitir filtrar produtos por categoria.

---

# 7. Terminal / PDV

Cada instalação deverá possuir uma identidade própria.

Exemplo:

```text
Loja: LOJA-01
Terminal: PDV-03
```

O identificador do terminal deverá ser único.

Esse conceito será utilizado futuramente para:

- sincronização;
- auditoria;
- identificação da origem das vendas;
- resolução de conflitos;
- relatórios por terminal;
- múltiplos PDVs.

---

# 8. Caixa

O caixa representa uma sessão operacional.

## 8.1 Abertura

O operador deverá informar:

- operador;
- terminal;
- data/hora;
- valor inicial em dinheiro.

O sistema deverá impedir a abertura de dois caixas ativos para o mesmo terminal, salvo regra administrativa explícita.

## 8.2 Durante o caixa

O sistema deverá registrar:

- vendas;
- pagamentos;
- entradas;
- saídas;
- sangrias;
- suprimentos;
- cancelamentos.

## 8.3 Fechamento

Ao fechar o caixa, o sistema deverá apresentar:

- valor inicial;
- vendas em dinheiro;
- vendas em cartão;
- vendas via PIX;
- outras formas;
- entradas;
- saídas;
- total esperado;
- valor contado;
- diferença;
- operador;
- data/hora.

O operador deverá informar o valor efetivamente contado.

O sistema deverá calcular:

```text
Diferença = Valor contado - Valor esperado
```

---

# 9. Movimentações de caixa

O sistema deverá permitir movimentações independentes de vendas.

Tipos mínimos:

- Suprimento;
- Sangria;
- Ajuste autorizado.

Cada movimentação deverá registrar:

- ID;
- caixa;
- operador;
- tipo;
- valor;
- descrição/motivo;
- data/hora;
- terminal.

Movimentações não deverão ser apagadas fisicamente após registradas.

---

# 10. Tela principal de venda

Esta é a tela mais importante do sistema.

Ela deverá priorizar velocidade.

## 10.1 Estrutura sugerida

```text
+-------------------------------------------------------------+
| Menu | Operador | Terminal | Status conexão | Hora          |
+-------------------------------------------------------------+
|                                                             |
| Categorias                                                  |
| [Bebidas] [Lanches] [Doces] [Outros]                       |
|                                                             |
| Produtos                         | Carrinho                  |
|                                  |                           |
| [Produto] [Produto] [Produto]    | Produto A   2 x 10,00    |
| [Produto] [Produto] [Produto]    | Produto B   1 x  8,00    |
| [Produto] [Produto] [Produto]    |                           |
|                                  | ----------------------- |
|                                  | TOTAL:       R$ 28,00  |
|                                  |                           |
|                                  | [FINALIZAR VENDA]       |
+-------------------------------------------------------------+
```

## 10.2 Formas de localizar produto

O operador deverá conseguir localizar um produto por:

- toque no produto;
- categoria;
- nome;
- SKU;
- código de barras.

O suporte a leitor físico de código de barras deverá ser previsto.

---

# 11. Carrinho

O carrinho deverá permitir:

- adicionar item;
- remover item;
- aumentar quantidade;
- diminuir quantidade;
- alterar quantidade;
- visualizar preço unitário;
- visualizar subtotal;
- visualizar total.

Cada item deverá guardar o preço praticado no momento da venda.

---

# 12. Finalização da venda

Ao finalizar:

1. validar carrinho;
2. calcular total;
3. selecionar forma de pagamento;
4. registrar pagamento;
5. concluir venda;
6. atualizar estoque;
7. registrar movimentações;
8. gerar ticket;
9. voltar rapidamente para uma nova venda.

---

# 13. Formas de pagamento

MVP:

- Dinheiro;
- Cartão de débito;
- Cartão de crédito;
- PIX.

O modelo deverá permitir múltiplos pagamentos na mesma venda.

Exemplo:

```text
Venda: R$ 100,00

Dinheiro: R$ 40,00
PIX:      R$ 60,00
```

## Dinheiro

O sistema deverá calcular:

```text
Troco = Valor recebido - Valor da venda
```

Nunca permitir troco negativo.

---

# 14. Venda

Uma venda deverá possuir:

- ID global;
- caixa;
- operador;
- terminal;
- data/hora;
- subtotal;
- desconto;
- total;
- status;
- origem;
- data de criação;
- data de atualização.

## Status

Inicialmente:

- Aberta;
- Concluída;
- Cancelada.

## Origem

O modelo deverá permitir:

- PDV;
- iFood;
- outro marketplace;
- integração futura.

---

# 15. Itens da venda

Cada item deverá registrar:

- ID;
- venda;
- produto;
- quantidade;
- preço unitário praticado;
- desconto;
- subtotal.

O item deve manter uma fotografia dos valores utilizados na venda.

Não depender exclusivamente do cadastro atual do produto para reconstruir uma venda antiga.

---

# 16. Pagamentos

Cada pagamento deverá possuir:

- ID;
- venda;
- forma de pagamento;
- valor;
- identificador externo opcional;
- status;
- data/hora.

Isso permitirá futuramente associar uma transação externa ao pagamento.

---

# 17. Ticket

Após a conclusão da venda, o sistema deverá permitir:

- visualizar ticket;
- imprimir;
- reimprimir;
- concluir e voltar à venda.

O ticket deverá apresentar:

- identificação da empresa;
- terminal;
- número/identificador da venda;
- data/hora;
- operador;
- itens;
- quantidade;
- preço unitário;
- subtotal;
- descontos;
- total;
- pagamentos;
- troco;
- informações adicionais quando aplicável.

A arquitetura de impressão deverá ser desacoplada da regra de negócio.

---

# 18. Estoque

No MVP, o estoque poderá ser simples.

Operações:

- entrada;
- saída;
- ajuste;
- baixa por venda;
- reversão por cancelamento.

A venda concluída deverá gerar a movimentação correspondente.

O estoque deverá ser tratado como informação operacional local no MVP.

No futuro, o estoque poderá ser sincronizado com uma base central.

---

# 19. Cancelamento

O cancelamento de uma venda deverá:

- exigir permissão adequada;
- registrar quem cancelou;
- registrar data/hora;
- registrar motivo;
- preservar o registro original;
- gerar reversão das movimentações relacionadas;
- permitir reprocessamento/sincronização futura.

Evitar exclusão física de vendas.

---

# 20. Consultas e relatórios do MVP

Relatórios mínimos:

### Vendas

- vendas do dia;
- vendas por período;
- vendas por operador;
- vendas por forma de pagamento;
- vendas por terminal.

### Produtos

- produtos vendidos;
- quantidade vendida;
- faturamento por produto.

### Caixa

- abertura;
- fechamento;
- movimentações;
- diferenças.

---

# 21. Arquitetura Offline-First

O sistema deverá iniciar utilizando banco local.

Exemplo conceitual:

```text
                    INTERNET
                       |
                       v
             +-------------------+
             |     API REST      |
             |      (VPS)        |
             +---------+---------+
                       |
                       v
             +-------------------+
             | Banco Remoto/Edge |
             +-------------------+

                       ^
                       |
                 sincronização
                       |
                       v

+------------------------------------------------+
|                 TERMINAL PDV                   |
|                                                |
|  Aplicação POS                                 |
|       |                                        |
|       v                                        |
|  Banco Local                                   |
|                                                |
|  SQLite / banco embarcado                      |
+------------------------------------------------+
```

O PDV não deverá depender da API para realizar uma venda local.

---

# 22. Preparação para sincronização

As entidades que poderão ser sincronizadas deverão utilizar identificadores globais.

Preferência:

```text
GUID / UUID
```

Evitar depender exclusivamente de:

```text
INTEGER AUTOINCREMENT
```

## Campos recomendados

Entidades sincronizáveis deverão considerar:

```text
Id
CreatedAt
UpdatedAt
DeletedAt (quando aplicável)
SyncStatus
```

Além disso:

```text
TerminalId
```

quando fizer sentido.

---

# 23. Exclusão lógica

Dados importantes para sincronização não deverão ser simplesmente removidos fisicamente.

Preferir:

```text
DeletedAt
```

ou mecanismo equivalente.

Isso permite que uma exclusão local possa ser propagada futuramente para a base central.

---

# 24. Serviço de sincronização

A sincronização será implementada em etapa posterior.

Conceito:

```text
Banco Local
    |
    | dados pendentes
    v
Sync Service
    |
    | HTTPS
    v
API REST
    |
    v
Banco Central
```

O serviço deverá futuramente:

- identificar alterações pendentes;
- enviar lotes;
- receber confirmação;
- marcar registros sincronizados;
- repetir operações que falharam;
- tratar indisponibilidade da API;
- evitar duplicidade;
- registrar erros.

---

# 25. Idempotência

A sincronização deverá ser projetada para suportar repetição.

Uma mesma operação enviada duas vezes não deverá gerar duas vendas.

Toda operação de sincronização importante deverá possuir um identificador único/idempotency key.

---

# 26. Múltiplos PDVs

O sistema deverá futuramente suportar:

```text
Empresa
  |
  +-- Loja 01
  |     +-- PDV 01
  |     +-- PDV 02
  |
  +-- Loja 02
        +-- PDV 01
        +-- PDV 02
```

As vendas deverão permitir identificar:

- empresa;
- loja;
- terminal;
- operador;
- origem.

---

# 27. Banco remoto / Edge

O banco remoto será responsável por dados centralizados e compartilhados.

Possíveis responsabilidades:

- consolidação de vendas;
- produtos;
- categorias;
- usuários;
- configurações;
- estoque central;
- relatórios;
- sincronização;
- dados de integrações.

A implementação inicial não precisa obrigatoriamente possuir toda essa estrutura.

---

# 28. API REST

A API central deverá ser responsável por:

- autenticação;
- sincronização;
- consulta de dados;
- persistência central;
- integrações externas;
- operações administrativas;
- futuras integrações entre múltiplos PDVs.

A API não deverá assumir que o PDV esteja sempre online.

---

# 29. Segurança

Requisitos:

- HTTPS;
- autenticação;
- autorização;
- tokens seguros;
- proteção de endpoints;
- validação de entrada;
- logs;
- rate limiting quando aplicável;
- proteção de credenciais;
- não armazenar senhas em texto puro.

Credenciais de integrações externas nunca deverão ser armazenadas diretamente no código-fonte.

---

# 30. Integração com iFood

A integração com iFood deverá ser tratada como módulo independente.

Objetivo:

```text
Cliente
   |
   v
iFood
   |
   v
Integração
   |
   v
API / Serviço
   |
   v
PDV
```

Funcionalidades futuras:

- receber pedidos;
- consultar detalhes;
- importar itens;
- identificar pagamento;
- aceitar pedido;
- atualizar status;
- tratar cancelamentos;
- sincronizar informações relevantes;
- registrar identificador externo.

Uma venda proveniente do iFood deverá ser identificada pela origem:

```text
Origem = IFOOD
```

O pedido externo deverá possuir seu próprio identificador.

A integração não deverá exigir que o operador redigite o pedido.

---

# 31. Integração com TecnoSpeed

A integração fiscal deverá ser isolada do núcleo do PDV.

Objetivos futuros:

- emissão fiscal;
- NFC-e;
- consulta de autorização;
- cancelamento;
- consulta de status;
- tratamento de erros;
- armazenamento dos identificadores fiscais.

A venda do PDV não deverá conhecer detalhes internos da implementação da TecnoSpeed.

Preferir uma abstração semelhante a:

```text
FiscalService
    |
    +-- TecnoSpeed
```

---

# 32. Integrações como arquitetura de adaptadores

As integrações externas deverão seguir uma arquitetura que permita substituição.

Conceito:

```text
                 +----------------+
                 | Núcleo do PDV  |
                 +-------+--------+
                         |
              interfaces/contratos
                         |
          +--------------+--------------+
          |                             |
     +----v----+                   +----v-----+
     | iFood   |                   |TecnoSpeed|
     | Adapter |                   | Adapter  |
     +---------+                   +----------+
```

O núcleo do PDV não deverá depender diretamente dos SDKs/APIs externas.

---

# 33. Observabilidade

O sistema deverá prever:

- logs estruturados;
- identificação do terminal;
- identificação do operador;
- identificação da venda;
- identificação da requisição;
- registro de erros;
- eventos de sincronização;
- eventos de integração.

Futuramente os logs poderão ser enviados para serviços como Application Insights, DataDog ou equivalente.

---

# 34. Auditoria

Operações críticas deverão possuir rastreabilidade.

Exemplos:

- login;
- abertura de caixa;
- fechamento;
- venda;
- cancelamento;
- desconto;
- sangria;
- suprimento;
- alteração de preço;
- alteração de estoque;
- sincronização;
- falha de integração.

---

# 35. Descontos

O sistema deverá ser preparado para descontos.

Tipos:

- percentual;
- valor fixo.

O desconto poderá exigir autorização conforme o perfil do operador.

O desconto deverá ser registrado na venda/item e não apenas calculado visualmente.

---

# 36. Requisitos de usabilidade

A tela principal deverá ser otimizada para operação contínua.

Requisitos:

- poucas etapas;
- botões grandes;
- alto contraste;
- feedback visual;
- teclado numérico touch quando necessário;
- evitar telas modais desnecessárias;
- navegação previsível;
- foco automático em campos relevantes;
- possibilidade de operar com leitor de código de barras;
- minimizar uso de teclado físico.

---

# 37. Requisitos de desempenho

A operação local deverá ser rápida.

Meta conceitual:

- abertura da tela de venda: imediata;
- inclusão de produto: praticamente instantânea;
- alteração de quantidade: instantânea;
- cálculo do total: instantâneo;
- finalização local: sem depender da internet.

Operações de rede não deverão bloquear desnecessariamente a operação do caixa.

---

# 38. Estados de conectividade

A interface deverá indicar:

```text
ONLINE
OFFLINE
SINCRONIZANDO
ERRO DE SINCRONIZAÇÃO
```

A perda de internet não deverá impedir o operador de continuar uma venda local, exceto em funcionalidades que dependam obrigatoriamente de serviço externo.

---

# 39. Estratégia de evolução

## Fase 1 — MVP Local

Implementar:

- autenticação;
- produtos;
- categorias;
- caixa;
- vendas;
- pagamentos;
- estoque básico;
- ticket;
- relatórios básicos;
- banco local;
- touch-first.

## Fase 2 — API

Adicionar:

- API REST;
- autenticação central;
- banco remoto;
- sincronização básica;
- observabilidade.

## Fase 3 — Multi-PDV

Adicionar:

- lojas;
- múltiplos terminais;
- sincronização bidirecional;
- conflitos;
- estoque central;
- relatórios consolidados.

## Fase 4 — Integrações

Adicionar:

- iFood;
- TecnoSpeed;
- outras integrações.

## Fase 5 — Cloud / Azure

Adicionar conforme necessidade e como laboratório de aprendizado:

- serviços Azure;
- mensageria;
- processamento assíncrono;
- armazenamento;
- monitoramento;
- CI/CD;
- containers;
- infraestrutura como código.

---

# 40. Diretrizes para implementação assistida por IA

O projeto deverá ser implementado incrementalmente.

A IA/desenvolvedor deverá:

1. não implementar toda a solução de uma única vez;
2. começar pelo domínio e banco local;
3. implementar uma funcionalidade por vez;
4. manter separação entre domínio, aplicação, infraestrutura e apresentação;
5. escrever testes para regras críticas;
6. evitar acoplamento prematuro a integrações externas;
7. manter o código simples;
8. evitar abstrações sem necessidade;
9. documentar decisões arquiteturais relevantes;
10. não adicionar dependências sem justificativa.

---

# 41. Princípio importante sobre o AZ-204

O projeto será utilizado também como laboratório prático para os conhecimentos do AZ-204.

Entretanto:

> **Não inserir uma tecnologia Azure artificialmente apenas para cumprir um tópico do exame.**

A regra deverá ser:

```text
Necessidade real do sistema
        ↓
Problema arquitetural
        ↓
Solução adequada
        ↓
Tecnologia Azure quando fizer sentido
```

Exemplos futuros:

- sincronização assíncrona → mensageria;
- processamento em background → Functions/Worker;
- armazenamento de arquivos → Blob Storage;
- observabilidade → Application Insights;
- API → App Service/container;
- CI/CD → Azure DevOps/GitHub Actions;
- configuração/segredos → serviços apropriados;
- escalabilidade → serviços gerenciados.

---

# 42. Modelo conceitual de dados

Entidades iniciais:

```text
Produto
Categoria
Usuario
Terminal
Loja
Caixa
MovimentacaoCaixa
Venda
ItemVenda
Pagamento
MovimentacaoEstoque
```

Entidades futuras:

```text
SyncOperation
SyncError
PedidoExterno
EventoIntegracao
ConfiguracaoIntegracao
DocumentoFiscal
Auditoria
```

---

# 43. Regras fundamentais de dados

## Identidade

Entidades distribuídas deverão preferencialmente utilizar GUID/UUID.

## Datas

Registrar datas de criação e atualização.

## Origem

Vendas deverão permitir identificar sua origem.

## Terminal

Vendas e operações de caixa deverão permitir identificar o terminal.

## Histórico

Vendas e operações financeiras não deverão ser apagadas fisicamente.

## Snapshot

Itens da venda deverão preservar os valores praticados no momento da venda.

---

# 44. Critérios de aceite do MVP

O MVP será considerado funcional quando for possível:

### Cenário 1 — Venda simples

1. operador entra;
2. abre caixa;
3. seleciona produto;
4. produto entra no carrinho;
5. informa pagamento;
6. sistema calcula troco;
7. venda é concluída;
8. estoque é atualizado;
9. ticket é gerado;
10. venda aparece no histórico.

### Cenário 2 — Caixa

1. operador abre caixa;
2. realiza vendas;
3. realiza sangria;
4. realiza suprimento;
5. fecha caixa;
6. sistema calcula valor esperado;
7. operador informa valor contado;
8. sistema calcula diferença.

### Cenário 3 — Offline

1. internet é indisponibilizada;
2. operador continua realizando vendas;
3. vendas continuam sendo gravadas localmente;
4. internet retorna;
5. dados permanecem disponíveis para futura sincronização.

### Cenário 4 — Múltiplos pagamentos

1. venda totaliza R$ 100;
2. operador informa R$ 40 em dinheiro;
3. operador informa R$ 60 em PIX;
4. sistema valida que pagamentos totalizam a venda;
5. venda é concluída.

### Cenário 5 — Cancelamento

1. venda concluída;
2. usuário autorizado solicita cancelamento;
3. sistema exige motivo;
4. venda passa para cancelada;
5. estoque é revertido;
6. operação é auditada.

---

# 45. Critérios de qualidade

O sistema deverá:

- ser fácil de operar;
- ser rápido;
- funcionar sem internet para operações essenciais;
- preservar dados;
- evitar duplicidade;
- possuir rastreabilidade;
- permitir evolução;
- manter integrações desacopladas;
- estar preparado para múltiplos PDVs;
- possuir arquitetura compreensível por outros desenvolvedores.

---

# 46. Fora do escopo inicial

Não implementar no primeiro MVP, salvo necessidade explícita:

- programa de fidelidade;
- CRM completo;
- marketplace próprio;
- delivery próprio;
- gestão contábil;
- folha de pagamento;
- gestão financeira empresarial completa;
- BI avançado;
- emissão fiscal completa;
- integração iFood em produção;
- replicação distribuída complexa.

Esses itens devem ser tratados como extensões futuras.

---

# 47. Princípio final

O sistema deve ser construído seguindo esta prioridade:

```text
USABILIDADE
    ↓
CONFIABILIDADE
    ↓
OPERAÇÃO OFFLINE
    ↓
INTEGRIDADE DOS DADOS
    ↓
SINCRONIZAÇÃO
    ↓
INTEGRAÇÕES
    ↓
ESCALA
```

O objetivo não é construir inicialmente um sistema enorme.

O objetivo é construir um **PDV pequeno, sólido e rápido**, cuja arquitetura permita crescer sem precisar reescrever o núcleo da aplicação.

---

# 48. Resumo executivo para o agente de desenvolvimento

Ao iniciar o projeto, considere o seguinte contexto:

> Estamos construindo um sistema de PDV touch-first, inicialmente local e offline-first. O operador deve conseguir abrir o caixa, vender produtos, receber pagamentos, calcular troco, emitir ticket e fechar o caixa sem depender da internet.
>
> O banco inicial é local. Entretanto, o modelo de dados deve ser preparado para futura sincronização com uma API REST hospedada em VPS e um banco remoto/central.
>
> Cada terminal possui identidade própria. Entidades distribuídas devem utilizar identificadores globais e informações de criação/atualização. Operações importantes devem ser auditáveis e idempotentes.
>
> Futuramente existirão múltiplos PDVs e lojas, com sincronização de dados.
>
> O sistema também deverá poder integrar-se com iFood para receber pedidos e atualizar seus estados, e com TecnoSpeed para recursos fiscais. Essas integrações devem ser isoladas por adapters/interfaces e nunca acoplar o núcleo do domínio diretamente às APIs externas.
>
> A solução também será utilizada como projeto prático de estudo para o AZ-204. Tecnologias Azure deverão ser introduzidas quando resolverem problemas reais do sistema, e não artificialmente.
>
> Priorize simplicidade, usabilidade, confiabilidade, baixo acoplamento, testes e evolução incremental.

