using FCG.Application.Messaging;
using FCG.Domain.Payments;
using FCG.Domain.Payments.Interfaces;
using FCG.Infrastructure.Messaging;
using FCG.Infrastructure.Persistence;
using FCG.Infrastructure.Persistence.Context;
using FCG.Infrastructure.Persistence.Repositories;
using FiapCloudGames.Contracts.Catalog;
using FiapCloudGames.RabbitMq.Consumers;
using FiapCloudGames.RabbitMq.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FCG.Infrastructure.DependencyInjection;

public static class InfrastructureServiceExtension
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddInfrastructure(IConfiguration configuration)
        {
            return services
                .ConfigureDb(configuration)
                .AddRepositories()
                .AddUnitOfWork()
                .AddDomainServices()
                .AddMessaging(configuration);
        }

        private IServiceCollection ConfigureDb(IConfiguration configuration)
        {
            return services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));
        }

        private IServiceCollection AddRepositories()
        {
            return services.AddScoped<IPaymentRepository, PaymentRepository>();
        }

        private IServiceCollection AddUnitOfWork()
        {
            return services.AddScoped<UnitOfWork>()
                .AddScoped<IPaymentUnitOfWork>(provider => provider.GetRequiredService<UnitOfWork>());
        }

        private IServiceCollection AddDomainServices()
        {
            return services.AddSingleton<IPaymentApprovalPolicy, RandomApprovalPolicy>();
        }

        private IServiceCollection AddMessaging(IConfiguration configuration)
        {
            services.AddRabbitMq(configuration);
            services.AddSingleton<IIntegrationEventPublisher, RabbitMqIntegrationEventPublisher>();

            return services.AddRabbitMqConsumer<OrderPlacedMessageProcessor>(
                new RabbitMqConsumerDefinition(
                    CatalogMessaging.Exchange,
                    "payments.order-placed",
                    CatalogMessaging.RoutingKeys.OrderPlaced));
        }
    }
}
