namespace FCG.Infrastructure.Messaging;

/// <summary>
/// Configuração do publisher SNS. O tópico é o único dado necessário: credenciais e
/// região da AWS são resolvidas pelo SDK a partir do ambiente (AWS_ACCESS_KEY_ID,
/// AWS_SECRET_ACCESS_KEY, AWS_REGION), igual a qualquer outro cliente AWS.
/// </summary>
public sealed class SnsOptions
{
    public const string SectionName = "Sns";

    /// <summary>ARN do tópico SNS onde o PaymentProcessedEvent é publicado (fcg-payment-events).</summary>
    public required string TopicArn { get; init; }

    /// <summary>
    /// Endpoint alternativo do SNS. Só usado em testes (ex.: LocalStack); em produção
    /// fica vazio e o SDK resolve o endpoint real da AWS a partir da região (AWS_REGION).
    /// </summary>
    public string? ServiceUrl { get; init; }
}
