using FiapCloudGames.Contracts.Catalog;

namespace FCG.Application.Payments.Interfaces;

/// <summary>
/// Processa um pedido de compra (<see cref="OrderPlacedEvent"/>), simulando o pagamento e
/// publicando o resultado.
/// </summary>
public interface IProcessOrderPlacedUseCase
{
    Task ExecuteAsync(OrderPlacedEvent orderPlaced, CancellationToken cancellationToken = default);
}
