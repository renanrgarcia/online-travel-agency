# Online Travel Agency

[![CI/CD](https://github.com/renanrgarcia/online-travel-agency/actions/workflows/ci-cd.yml/badge.svg)](https://github.com/renanrgarcia/online-travel-agency/actions/workflows/ci-cd.yml)

Read in [English](README.md).

**FlightAi** — um sistema de busca e reserva de voos assistido por IA, e o primeiro módulo desta
agência de viagens online. Construído passo a passo como exercício de aprendizado: ao mesmo tempo uma
implementação de referência de uma aposta arquitetural específica, e um caminho prático pelo Azure
(este repositório também serve de estudo para o AZ-104 — Bicep, App Service, Functions, Static Web
Apps e o CI/CD que amarra tudo isso). Voos são o sistema inteiro hoje; o plano é crescer isso para o
resto de uma OTA — hotéis, carros, pacotes — sobre a mesma fundação, à medida que o projeto se expande.
Tudo abaixo descreve o FlightAi especificamente, o módulo que existe agora.

**A aposta central de design:** manter busca, ranqueamento, precificação e emissão de bilhetes totalmente
determinísticos, e usar IA apenas nas duas pontas — transformar uma consulta em linguagem natural em uma
busca estruturada, e transformar um resultado já ranqueado e já precificado em um texto legível. Nada no
meio do caminho chama um modelo, e nenhuma saída de modelo chega a um usuário sem passar antes por código
determinístico.

## Como uma busca flui

🤖 marca os dois únicos pontos em que um modelo de linguagem está envolvido; todo o resto é código de
backend determinístico, sem IA no meio.

![Diagrama: o pipeline de busca em três linhas -- Cliente, IntentAgent e busca nos fornecedores fluindo da esquerda para a direita; OfferScorer, PriceAssertionService e PriceReferenceStore fluindo da direita para a esquerda; ExplanationAgent, o renderizador de placeholders e o cliente fluindo novamente da esquerda para a direita -- com uma seta descendente ligando a última caixa de cada linha à primeira caixa da linha seguinte.](docs/assets/search-flow-pt-BR.svg)

O modelo recebe apenas tokens opacos (`{{PRICE_LCC-002}}`, `{{SUPERLATIVE_CHEAPEST_LCC-001}}`, ...),
nunca um preço ou comparação de verdade — veja
[`docs/reference/02-price-integrity.md`](docs/reference/02-price-integrity.md) para entender por que
essa fronteira é estrutural, e não apenas uma instrução no prompt.

## Experimente ao vivo

| Peça | URL |
|---|---|
| Frontend (chat) | https://victorious-meadow-0d0da3c03.3.azurestaticapps.net |
| API de busca | https://flightai-api-dev.azurewebsites.net |
| Saga de reserva (Functions) | https://flightai-booking-dev.azurewebsites.net |

Os três são recursos Azure reais, implantados, em camada gratuita — não é uma maquete. Uma ressalva
honesta: a camada F1 do App Service "esfria" após ficar ociosa, então a primeira requisição pode levar
alguns segundos. Os três são reimplantados automaticamente a cada merge para `main` (veja
[CI/CD e implantação](#cicd-e-implantação) abaixo), então o que está no ar sempre corresponde
especificamente à `main` — `develop` pode estar à frente dela entre um merge e outro.

**Busca** (transmite quatro eventos Server-Sent — intenção interpretada, um `supplier-result` por
fornecedor, ofertas ranqueadas, depois uma explicação):

```bash
curl -N --get "https://flightai-api-dev.azurewebsites.net/api/search/stream" \
  --data-urlencode "q=cheapest flight from São Paulo to Lisbon"
```

**Reserva** (inicia a saga em Durable Functions — pagamento, pedido, bilhete, confirmação — depois faça
polling do resultado):

```bash
curl -X POST https://flightai-booking-dev.azurewebsites.net/api/bookings \
  -H "Content-Type: application/json" \
  -d '{"bookingId":"demo-001","offerId":"NDC-abc123","travellerEmail":"t@example.com","amount":791.00,"currency":"USD","paymentMethodToken":"tok_test"}'

curl https://flightai-booking-dev.azurewebsites.net/api/bookings/demo-001
```

Um `offerId` contendo `FAIL-TICKET` (ex.: `NDC-FAIL-TICKET-xyz`) falha a emissão do bilhete de forma
determinística, então dá para observar a saga compensando — estornando o pagamento, cancelando o
pedido — em vez de deixar uma reserva cobrada e não cumprida. Contrato completo:
[`docs/reference/06-api-sse-contract.md`](docs/reference/06-api-sse-contract.md) e
[`docs/reference/07-booking-saga.md`](docs/reference/07-booking-saga.md).

## Stack técnica

| Camada | Escolha |
|---|---|
| Backend | .NET 10, ASP.NET Core Minimal APIs, Server-Sent Events |
| Camada de IA | `Microsoft.Agents.AI` + `Microsoft.Extensions.AI` — modelo real (Gemini, camada gratuita) por trás de `IChatClient`, controlado por configuração; recai para um substituto offline determinístico quando não há chave configurada |
| Fluxo de reserva | Azure Durable Functions (o padrão saga: passos com checkpoint, cada um com uma ação compensatória) |
| Frontend | React 19 + TypeScript + Vite, Vitest + Testing Library |
| Infraestrutura como código | Bicep, com escopo de assinatura (cria seu próprio grupo de recursos) |
| CI/CD | GitHub Actions — testa a cada push/PR, implanta a cada push para `main` |
| Hospedagem | Azure Static Web Apps (Free), App Service (F1/Free), Functions (Consumption), Storage |
| Testes | xUnit (backend), Vitest (frontend) — ambos organizados como um teste por avaliação documentada, não cobertura ad hoc |

Todo recurso Azure usado aqui tem como alvo uma camada gratuita ou quase gratuita, por design — veja
[`docs/deployment.md`](docs/deployment.md) para o detalhamento completo de custo e a única troca real que
isso impõe (a camada de hospedagem de modelos do Azure não tem camada gratuita perpétua, então as pontas
de IA rodam offline/determinísticas até que isso seja deliberadamente trocado).

## O que está construído

O status de construção do próprio FlightAi — o resto da agência de viagens online ainda não existe como
código. Reflete o que está implementado hoje, na `develop` — o branch que esta tabela deve acompanhar.
Ela pode estar à frente do que está no ar em `main` entre um merge e outro; veja
[CI/CD e implantação](#cicd-e-implantação).

**Backend** — [`docs/features/01-backend/`](docs/features/01-backend/README.md)

| Etapa | O quê | Status |
|---|---|---|
| 1. Núcleo de integridade de preço | Tokens de preço no servidor; um modelo pode referenciar um preço, nunca autorá-lo | ✅ Pronto |
| 2. Ranqueamento | Pontuação de ofertas determinística | ✅ Pronto |
| 3. Fornecedores | Conectores mock GDS/NDC/LCC, fan-out, orçamento + circuit breaker | ✅ Pronto |
| 4. Camada de IA, offline | Interpretação de intenção e explicação, contra um modelo substituto determinístico | ✅ Pronto |
| 5. API + SSE | `GET /api/search/stream`, o pipeline completo de quatro eventos | ✅ Pronto |
| 6. Suporte à decisão | Fatos de comparação (diferenças, superlativos) para a explicação afirmar | ✅ Pronto |
| 7. Saga de reserva | Saga em Durable Functions, caminho feliz + compensação + idempotência | ✅ Pronto |
| 8. Seguro para expor | CORS, rate limiting, preços com autoridade no servidor, tratamento estruturado de erros | ✅ Pronto |
| 9. Modelo real | Trocar o substituto offline por um `IChatClient` real | ✅ Pronto |

O roadmap de backend acima está completo — as nove etapas prontas. Veja
[`docs/features/01-backend/README.md`](docs/features/01-backend/README.md) para a ordem completa de
construção e [`docs/reference/09-lessons-learned.md`](docs/reference/09-lessons-learned.md) para o que
quebrou pelo caminho, incluindo três bugs reais que um modelo de verdade revelou e que o substituto
offline determinístico jamais revelaria.

**Frontend** — [`docs/features/02-frontend/`](docs/features/02-frontend/README.md)

| Tarefa | O quê | Status |
|---|---|---|
| F01 | Scaffold Vite + cliente SSE tipado | ✅ Pronto |
| F02 | Shell do chat, alternador EN/PT-BR | ✅ Pronto |
| F03 | O turno de busca — o stream SSE real conduzindo o chat | ✅ Pronto |
| F04 | Cartões de oferta e comparação | ✅ Pronto |
| F05 | O turno de reserva — a saga a partir da UI do chat, incluindo compensação | ✅ Pronto |
| F06 | Estados degradados | ✅ Pronto |
| F07 | UI bilíngue (além do alternador do F02) | ✅ Pronto |

**Infraestrutura** — [`docs/features/03-infra/`](docs/features/03-infra/README.md)

| Tarefa | O quê | Status |
|---|---|---|
| 01 | Infra do Functions (plano Consumption, Storage) + CI/CD | ✅ Pronto, no ar |
| 02 | Static Web App + CORS para os dois backends | ✅ Pronto, no ar |

## Estrutura do repositório

```
backend/            .NET 10 -- a solution vive aqui, não na raiz do repositório.
  FlightAi.slnx     Cobre apenas os projetos .NET.
  src/
    FlightAi.Core/          Domínio + lógica determinística. Nenhuma dependência de IA.
    FlightAi.Agents/        A camada de IA (o único projeto que toca um modelo).
    FlightAi.Api/           Minimal API -- GET /api/search/stream (Server-Sent Events).
    FlightAi.Booking.Functions/  Saga de reserva em Azure Durable Functions.
  tests/
    FlightAi.Tests/         xUnit. Um teste por avaliação documentada, veja docs/features/01-backend/tasks/.
frontend/           SPA em React + TypeScript + Vite -- uma interface de chat sobre o backend.
infra/              Bicep. Com escopo de assinatura, provisiona os recursos Azure.
.github/workflows/  CI em develop + PRs, implantação a cada push para main.
docs/
  reference/        Como o sistema funciona, em ordem de leitura.
  features/         O plano de construção: um roadmap e tarefas com escopo, com avaliações, por feature.
  deployment.md     Topologia Azure, restrições de camada gratuita, ordem de implantação.
```

## Rodando localmente

**API do backend** (`http://localhost:5294`):

```bash
dotnet run --project backend/src/FlightAi.Api
```

**Booking Functions** — precisa do [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite)
(emulador de storage local do Durable Task) e do [Azure Functions Core Tools](https://learn.microsoft.com/azure/azure-functions/functions-run-local):

```bash
azurite --skipApiVersionCheck &
cd backend/src/FlightAi.Booking.Functions && func start   # http://localhost:7071
```

**Frontend** (`http://localhost:5173`) — a URL da API é configuração em tempo de build (nunca
hardcoded, `frontend/src/config.ts`), então aponte-a explicitamente para a API acima;
`appsettings.Development.json` já libera essa origem no CORS:

```bash
cd frontend && npm install
echo "VITE_API_BASE_URL=http://localhost:5294" > .env.development
npm run dev
```

## Testes

```bash
dotnet test backend/FlightAi.slnx     # backend -- xUnit
cd frontend && npm test               # frontend -- Vitest + Testing Library
```

As duas suítes são organizadas como um teste por avaliação documentada, em vez de cobertura ad hoc —
cada task card em `docs/features/*/tasks/` lista suas avaliações com um ID e o motivo de cada uma
existir, e o arquivo de teste correspondente referencia esses IDs diretamente. Veja
[`docs/features/README.md`](docs/features/README.md) para a disciplina por trás disso.

## CI/CD e implantação

Push para `develop` ou abertura de PR contra `main` → `build-and-test` (backend) e
`build-and-test-frontend` rodam; ambos são checks obrigatórios. Push para `main` → três jobs de
implantação rodam em paralelo: a API e as Functions publicam via seus publish profiles do Azure, o
frontend builda e implanta no Static Web Apps com as URLs base da API e das Functions fornecidas como
variáveis de build (vindas dos mesmos outputs do Bicep que provisionaram esses recursos, não duplicadas
manualmente).

A infraestrutura em si é provisionada separadamente, rodando o Bicep diretamente contra a assinatura —
veja [`infra/README.md`](infra/README.md) para os comandos exatos e a ressalva de região/cota que
moldou `main.bicepparam`. [`docs/deployment.md`](docs/deployment.md) cobre a topologia completa e o
raciocínio de custo; [`docs/features/03-infra/README.md`](docs/features/03-infra/README.md) cobre por
que a infraestrutura é sua própria feature em vez de estar embutida no backend ou no frontend.

## Para se aprofundar

Comece por [`docs/README.md`](docs/README.md) para entender como o spec está organizado, depois
[`docs/reference/`](docs/reference/README.md) para entender o sistema de ponta a ponta, e então escolha
um roadmap de feature: [backend](docs/features/01-backend/README.md),
[frontend](docs/features/02-frontend/README.md), [infra](docs/features/03-infra/README.md).
