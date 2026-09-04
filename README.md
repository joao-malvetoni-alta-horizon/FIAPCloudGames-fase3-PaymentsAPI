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
4. Publica `PaymentProcessedEvent` (`UserId`, `GameId`, `Status`) em **dois transportes**,
   pois há dois consumidores independentes:
   - **RabbitMQ** (exchange `payments.exchange`, routing key `payment.status`) — consumido
     pelo `CatalogAPI`, que adiciona o jogo à biblioteca se `Approved`;
   - **SNS** (tópico `fcg-payment-events`) — consumido pela função Lambda do
     `NotificationsAPI` (via SQS), que envia o email de confirmação se `Approved`.

   `NotificationsAPI` é serverless (Fase 3): não existe mais como container consumindo do
   RabbitMQ, por isso a publicação para ele precisou migrar para SNS. O `CompositeIntegrationEventPublisher`
   (`FCG.Infrastructure/Messaging`) publica nos dois ao mesmo tempo; uma falha em um
   transporte não impede a publicação no outro (cada um loga e segue, sem padrão Outbox).

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
- RabbitMQ via pacote NuGet `FiapCloudGames.RabbitMq` (consome `OrderPlacedEvent`; publica `PaymentProcessedEvent` de volta pro CatalogAPI)
- AWS SNS (`AWSSDK.SimpleNotificationService`) — publica `PaymentProcessedEvent` para a Lambda do NotificationsAPI
- Contratos de eventos compartilhados via pacote NuGet `FiapCloudGames.Contracts`
- Serilog (logs estruturados)
- Testes: xUnit + Shouldly + NSubstitute

## Estrutura

```
src/
  FCG.Domain          # Payment (entidade), IPaymentApprovalPolicy (regra da simulação)
  FCG.Application     # Caso de uso: processa OrderPlacedEvent, publica PaymentProcessedEvent
  FCG.Infrastructure  # EF Core (AppDbContext, repositório, migrations) + RabbitMq + SNS
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
| `Sns__TopicArn` | ARN do tópico SNS onde o `PaymentProcessedEvent` também é publicado | `arn:aws:sns:us-east-1:450753703903:fcg-payment-events` |
| `AWS_REGION` / `AWS_ACCESS_KEY_ID` / `AWS_SECRET_ACCESS_KEY` | Credenciais AWS para publicar no SNS (padrão do SDK; sem elas a publicação SNS falha silenciosamente, mas o RabbitMQ continua funcionando normalmente) | — |
| `ASPNETCORE_ENVIRONMENT` | Ambiente (`Development`/`Production`) | `Development` |

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
