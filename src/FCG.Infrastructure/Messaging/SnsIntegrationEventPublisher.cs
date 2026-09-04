using System.Diagnostics;
using System.Text.Json;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using FCG.Application.Messaging;
using FiapCloudGames.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FCG.Infrastructure.Messaging;

/// <summary>
/// Publica eventos de integração em um tópico SNS. Substitui o RabbitMQ para este
/// evento (Fase 3): o NotificationsAPI migrou para uma função Lambda acionada por
/// SNS -> SQS, e não consome mais de uma exchange do RabbitMQ. O RabbitMQ continua
/// em uso neste serviço apenas para consumir o OrderPlacedEvent do CatalogAPI (ver
/// OrderPlacedMessageProcessor) — isso é independente deste publisher.
/// </summary>
public sealed class SnsIntegrationEventPublisher(
    IAmazonSimpleNotificationService snsClient,
    IOptions<SnsOptions> options,
    ILogger<SnsIntegrationEventPublisher> logger) : IIntegrationEventPublisher
{
    public async Task PublishAsync<TEvent>(
        TEvent integrationEvent,
        CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        try
        {
            // Serialização com as opções padrão do System.Text.Json (sem policy de
            // casing): o Lambda do NotificationsAPI desserializa com as mesmas opções
            // padrão, então os nomes de propriedade (PascalCase) precisam bater
            // exatamente dos dois lados.
            var message = JsonSerializer.Serialize(integrationEvent);

            var request = new PublishRequest
            {
                TopicArn = options.Value.TopicArn,
                Message = message
            };

            // SNS/SQS não propagam contexto de trace automaticamente. Injetamos o
            // traceparent (W3C) como message attribute para o consumidor (Lambda do
            // NotificationsAPI) poder linkar o span dele ao trace de origem no New Relic.
            // Nota: isso só continua o trace do "Compra de Jogo" ponta a ponta se o
            // OrderPlacedMessageProcessor também propagar o traceparent recebido do
            // RabbitMQ para este Activity — ver SDD.md §9 para o desenho completo.
            var traceparent = Activity.Current?.Id;
            if (!string.IsNullOrEmpty(traceparent))
            {
                request.MessageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    ["traceparent"] = new MessageAttributeValue { DataType = "String", StringValue = traceparent }
                };
            }

            await snsClient.PublishAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            // Trade-off consciente: sem padrão Outbox, uma falha de publish não deve
            // derrubar o processamento do evento (que já foi consumido/decidido). Em
            // produção, o Outbox garantiria a entrega mesmo com o SNS fora do ar.
            logger.LogWarning(
                ex,
                "Evento {EventType} (EventId {EventId}) não pôde ser publicado no SNS e será perdido (sem Outbox)",
                integrationEvent.GetType().Name,
                integrationEvent.EventId);
        }
    }
}
