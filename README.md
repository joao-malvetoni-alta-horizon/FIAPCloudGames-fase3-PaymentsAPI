# FIAP Cloud Games — PaymentsAPI (Fase 2)

Microsserviço de **Pagamentos** da plataforma FIAP Cloud Games. Responsável por processar
(simular) o pagamento de uma compra de jogo.

Serviço orientado a eventos: o fluxo principal consome `OrderPlacedEvent` e publica
`PaymentProcessedEvent`. Também expõe uma API REST para **consulta** dos pagamentos processados
e para **disparar manualmente** um pagamento (útil em demonstração/teste).

## Fluxo

1. Consome `OrderPlacedEvent` (publicado pelo `CatalogAPI` na exchange `catalog.exchange`,
   routing key `order.placed`), em fila própria (`payments.order-placed`).
2. Verifica, por `EventId`, se esse evento já foi processado antes (idempotência — ver
   "Persistência" abaixo). Se sim, pula a decisão de pagamento e só republica o resultado já
   gravado.
3. Caso contrário, simula a decisão do pagamento (`IPaymentApprovalPolicy` — hoje
   `RandomApprovalPolicy`, que aprova ~90% das tentativas; a Fase 2 não integra com um gateway
   de pagamento real) e persiste o resultado.
4. Publica `PaymentProcessedEvent` (`UserId`, `GameId`, `Status`) na exchange
   `payments.exchange`, routing key `payment.status`, consumido pelo `CatalogAPI` (adiciona o
   jogo à biblioteca se `Approved`) e pelo `NotificationsAPI` (envia email de confirmação se
   `Approved`).

## API REST

O fluxo por evento é o principal; a API REST complementa para consulta e testes manuais.

| Método | Rota | Descrição |
|--------|------|-----------|
| `POST` | `/payments/process` | Simula um pedido (`UserId`, `GameId`, `Price`), processa e persiste o pagamento. Publica um `PaymentProcessedEvent` real no broker, como no fluxo por evento. |
| `GET`  | `/payments` | Lista todos os pagamentos processados. |
| `GET`  | `/payments/{id}` | Busca um pagamento pelo `Id` interno. |
| `GET`  | `/payments/by-event/{eventId}` | Busca o pagamento pelo `EventId` do `OrderPlacedEvent` de origem. |
| `GET`  | `/health` | Health check. |

## Persistência

Cada pagamento decidido é gravado na tabela `Payments` (PostgreSQL via EF Core), com índice
único em `EventId` (o `EventId` do `OrderPlacedEvent` de origem). Isso existe porque o RabbitMQ
entrega mensagens no mínimo uma vez ("at-least-once"): sem esse registro, uma reentrega do
mesmo `OrderPlacedEvent` (ex.: após um crash do consumidor antes do ack) reprocessaria o
pagamento e publicaria um `PaymentProcessedEvent` duplicado. Com o registro, a reentrega apenas
republica o status já decidido, sem rodar a política de aprovação de novo.

Migrations são aplicadas automaticamente na subida da API (`app.MigrateAsync()`), com retry
para aguardar o banco ficar pronto (útil em `docker-compose`/Kubernetes).

## Stack

- .NET 10 — Minimal API (health check) + Controllers (API REST de pagamentos)
- EF Core + Npgsql (PostgreSQL) para persistência dos pagamentos processados
- RabbitMQ via pacote NuGet `FiapCloudGames.RabbitMq` (publisher + consumidor genérico)
- Contratos de eventos compartilhados via pacote NuGet `FiapCloudGames.Contracts`
- Serilog (logs estruturados)
- New Relic (APM: métricas, logs e traces) via pacote NuGet `NewRelic.Agent`
- Testes: xUnit + Shouldly + NSubstitute

## Estrutura

```
src/
  FCG.Domain          # Payment (entidade), IPaymentApprovalPolicy (regra da simulação)
  FCG.Application     # Caso de uso: processa OrderPlacedEvent, publica PaymentProcessedEvent
  FCG.Infrastructure  # EF Core (AppDbContext, repositório, migrations) + RabbitMq
  FCG.API             # Composição, health check, API REST e migração do banco na subida
tests/
  FCG.UnitTests        # xUnit + Shouldly + NSubstitute
Dockerfile             # build multi-stage de produção
docker-compose.yml     # API + PostgreSQL + RabbitMQ para rodar o serviço isolado
```

## Variáveis de ambiente

| Variável | Descrição | Exemplo |
|----------|-----------|---------|
| `ConnectionStrings__DefaultConnection` | Connection string do PostgreSQL | `Host=db;Port=5432;Database=fcg_payments;Username=fcg;Password=fcg123` |
| `RabbitMq__Host` | Host do RabbitMQ | `rabbitmq` |
| `RabbitMq__Port` | Porta do RabbitMQ | `5672` |
| `RabbitMq__Username` | Usuário do RabbitMQ | `fcg` |
| `RabbitMq__Password` | Senha do RabbitMQ | `fcg123` |
| `RabbitMq__VirtualHost` | Virtual host do RabbitMQ | `/` |
| `ASPNETCORE_ENVIRONMENT` | Ambiente (`Development`/`Production`) | `Development` |
| `NEW_RELIC_LICENSE_KEY` | License key (ingest) da conta New Relic. **Segredo** — vem de Kubernetes Secret ou do `.env` local, nunca do código | `eu01xx...NRAL` |
| `NEW_RELIC_APP_NAME` | Nome da aplicação no New Relic (já definido nos Dockerfiles) | `FCG-PaymentsAPI` |

> As demais variáveis do agente (`CORECLR_*`, `NEW_RELIC_DISTRIBUTED_TRACING_ENABLED`,
> `NEW_RELIC_APPLICATION_LOGGING_*`, `NEW_RELIC_LOG_*`) já vêm prontas nos Dockerfiles —
> ver "Observabilidade" abaixo.

## Observabilidade (New Relic)

A plataforma de observabilidade escolhida para a Fase 3 é o **New Relic** (opção B: APM
gerenciado). O agente vem do pacote NuGet `NewRelic.Agent`, que publica o agente e o profiler
em `./newrelic` junto com a aplicação; os dois Dockerfiles do repositório já definem as
variáveis `CORECLR_*` que fazem o CoreCLR carregar o profiler, então **a imagem sobe
instrumentada sem nenhuma mudança em `Program.cs`**. Sem `NEW_RELIC_LICENSE_KEY` a API roda
normalmente — o agente apenas não conecta.

### Os três pilares

| Pilar | Como é atendido |
|-------|-----------------|
| **Métricas** | Automáticas do agente APM: latência, throughput e taxa de erro por transação (endpoints REST e o consumo da fila). O dashboard é montado na UI do New Relic. |
| **Logs** | `NEW_RELIC_APPLICATION_LOGGING_FORWARDING_ENABLED=true` faz o agente encaminhar os logs do Serilog (que o agente instrumenta sozinho) para o New Relic, e `..._LOCAL_DECORATING_ENABLED=true` injeta os identificadores de trace em cada linha, ligando log ↔ trace. Nenhum sink HTTP foi adicionado. |
| **Traces** | Distributed tracing ligado (`NEW_RELIC_DISTRIBUTED_TRACING_ENABLED=true`). O consumo do `OrderPlacedEvent` é marcado com `[Transaction]` e anotado com os atributos `fcg.orderEventId`, `fcg.userId` e `fcg.gameId` (só GUIDs, nada de dado pessoal). **Limitação conhecida** — ver abaixo. |

### Limitação: o trace não atravessa o RabbitMQ

O agente .NET 10.54 instrumenta o `RabbitMQ.Client` apenas até a versão **6.8.1**
(`maxVersion="6.8.1"` em `NewRelic.Providers.Wrapper.RabbitMq.Instrumentation.xml`, e o
matcher de consumo é o `EventingBasicConsumer`). Este serviço resolve
**`RabbitMQ.Client` 7.2.1** (transitivo de `FiapCloudGames.RabbitMq` 1.0.0), que usa o
`AsyncEventingBasicConsumer`. Consequência prática:

- o agente **não** injeta nem lê os headers de distributed tracing nas mensagens, então o
  trace do fluxo de "Compra de Jogo" **quebra na fila**: aparece um trace no `FCG-CatalogAPI`
  (a requisição HTTP de compra) e outro, separado, no `FCG-PaymentsAPI` (o processamento da
  mensagem);
- os dois lados continuam sendo correlacionáveis pelos atributos customizados
  (`fcg.orderEventId`, `fcg.userId`, `fcg.gameId`) e pelos logs;
- o `[Transaction]` no `OrderPlacedMessageProcessor` existe justamente porque, sem
  instrumentação da fila, o processamento do pagamento não geraria transação nenhuma — sem
  ele não haveria métrica nem trace do consumo.

Optou-se por **não** reescrever o wrapper de mensageria (`FiapCloudGames.RabbitMq`, pacote
externo) para propagar os headers manualmente: seria mudança de infraestrutura de negócio,
fora do escopo deste trabalho de observabilidade.

### Configurar a license key no Kubernetes (requisito do desafio)

A chave é uma credencial e por isso vive num **Secret**, nunca em ConfigMap ou no código.
O `k8s/deployment.yaml` a consome do Secret `fcg-secrets`, chave `NewRelic__LicenseKey`:

```bash
kubectl -n fcg create secret generic fcg-secrets \
  --from-literal=NewRelic__LicenseKey=<sua-license-key> \
  --from-literal=RabbitMq__Password=<senha-do-rabbitmq> \
  --dry-run=client -o yaml | kubectl apply -f -
```

### Rodar localmente com o agente

Com `docker compose`, a chave vem do ambiente do host (ou de um arquivo `.env`, que está no
`.gitignore` — copie o `.env.example`):

```bash
cp .env.example .env      # e preencha NEW_RELIC_LICENSE_KEY
docker compose up --build
```

Rodando com `dotnet run` o agente **não** é carregado (o profiler depende das variáveis
`CORECLR_*` definidas no Dockerfile). Para instrumentar também nesse cenário, exporte antes:

```bash
export CORECLR_ENABLE_PROFILING=1
export CORECLR_PROFILER='{36032161-FFC0-4B61-B559-F6C5D41BAE5A}'
export CORECLR_NEWRELIC_HOME="$PWD/src/FCG.API/bin/Debug/net10.0/newrelic"
export CORECLR_PROFILER_PATH="$CORECLR_NEWRELIC_HOME/libNewRelicProfiler.so"
export NEW_RELIC_APP_NAME=FCG-PaymentsAPI
export NEW_RELIC_LICENSE_KEY=<sua-license-key>
dotnet run --project src/FCG.API
```

### Log do próprio agente e usuário não-root

A imagem da raiz roda como usuário não-root (`USER $APP_UID`) e o agente escreve o próprio
log em disco — no diretório padrão (`/app/newrelic/logs`, que pertence ao root) isso falharia.
Por isso os Dockerfiles apontam `NEW_RELIC_LOG_DIRECTORY` e `NEW_RELIC_PROFILER_LOG_DIRECTORY`
para `/tmp/newrelic` (criado com permissão `1777`, portanto escrito por qualquer UID, inclusive
se o Kubernetes forçar um `runAsUser` diferente) e ligam `NEW_RELIC_LOG_CONSOLE=true`, para o
log do agente também sair no stdout do container, onde Docker/Kubernetes já coletam.

## Executar com Docker (serviço isolado)

Sobe API + PostgreSQL + RabbitMQ:

```bash
docker compose up --build
```

- API: http://localhost:8080 (ex.: `GET http://localhost:8080/health`)
- Painel do RabbitMQ: http://localhost:15672 (usuário `fcg` / senha `fcg123`)

> Para o fluxo completo entre microsserviços (Catalog → Payments → Notifications), use o
> repositório de orquestração, onde todos os serviços compartilham o mesmo RabbitMQ.

## Executar localmente (dotnet run)

Sobe só a infraestrutura em container e roda a API no host:

```bash
docker compose up -d db rabbitmq   # PostgreSQL (5432) + RabbitMQ (5672)
dotnet run --project src/FCG.API
```

No ambiente `Development` a API aponta para `localhost` (ver `appsettings.Development.json`).

## Testes

```bash
dotnet test
```
