using FCG.Application.Payments.Interfaces;
using FCG.Application.Shared.Cache;
using FCG.Domain.Payments.Interfaces;
using FiapCloudGames.Contracts.Catalog;
using Microsoft.AspNetCore.Mvc;

namespace FCG.API.Controllers;

/// <summary>
/// Endpoints REST para consultar os pagamentos processados e para disparar manualmente o
/// processamento de um pagamento (útil para demonstração/teste sem depender do CatalogAPI).
/// O fluxo principal do serviço continua orientado a eventos (consumo de <c>OrderPlacedEvent</c>).
/// </summary>
[ApiController]
[Route("payments")]
public sealed class PaymentsController(
    IProcessOrderPlacedUseCase processOrderPlaced,
    IPaymentRepository payments,
    ICacheService cacheService) : ControllerBase
{
    /// <summary>
    /// Simula um pedido de compra (equivalente a um <c>OrderPlacedEvent</c>), processa o
    /// pagamento e persiste o resultado. Assim como no fluxo por evento, publica um
    /// <c>PaymentProcessedEvent</c> real no broker.
    /// </summary>
    [HttpPost("process")]
    public async Task<ActionResult<PaymentResponse>> Process(
        [FromBody] ProcessPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var orderPlaced = new OrderPlacedEvent(request.UserId, request.GameId, request.Price);
        await processOrderPlaced.ExecuteAsync(orderPlaced, cancellationToken);

        var payment = await payments.GetByEventIdAsync(orderPlaced.EventId, cancellationToken);
        if (payment is null)
        {
            return Problem("O pagamento não foi encontrado após o processamento.");
        }

        return Ok(PaymentResponse.From(payment));
    }

    /// <summary>Lista todos os pagamentos processados.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PaymentResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var cacheKey = "payments:all";

        var all = await cacheService.GetOrSetAsync(
            cacheKey,
            async () =>
            {
                return await payments.GetAllAsync(cancellationToken);
            },
            TimeSpan.FromMinutes(3),
            cancellationToken
        );

        if (all is null)
        {
            return NotFound();
        }

        return Ok(all.Select(PaymentResponse.From).ToList());
    }

    /// <summary>Busca um pagamento pelo <c>Id</c> interno.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PaymentResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var cacheKey = $"payment:{id}";

        var payment = await cacheService.GetOrSetAsync(
            cacheKey,
            async () =>
            {
                return await payments.GetByIdAsync(id, cancellationToken);
            },
            TimeSpan.FromMinutes(1),
            cancellationToken
        );

        if (payment is null)
        {
            return NotFound();
        }

        return Ok(PaymentResponse.From(payment!));
    }

    /// <summary>Busca o pagamento correspondente ao <c>EventId</c> do <c>OrderPlacedEvent</c> de origem.</summary>
    [HttpGet("by-event/{eventId:guid}")]
    public async Task<ActionResult<PaymentResponse>> GetByEventId(Guid eventId, CancellationToken cancellationToken)
    {
        var cacheKey = $"payment-by-event:{eventId}";

        var payment = await cacheService.GetOrSetAsync(
            cacheKey,
            async () =>
            {
                return await payments.GetByEventIdAsync(eventId, cancellationToken);
            },
            TimeSpan.FromMinutes(1),
            cancellationToken
        );

        if (payment is null)
        {
            return NotFound();
        }

        return Ok(PaymentResponse.From(payment!));
    }
}
