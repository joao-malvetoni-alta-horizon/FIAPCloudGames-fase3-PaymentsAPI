using Amazon.SimpleNotificationService;
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
using Microsoft.Extensions.Options;

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
            // RabbitMQ: CONSOME o OrderPlacedEvent do CatalogAPI e também é usado para
            // PUBLICAR o PaymentProcessedEvent de volta pro CatalogAPI (adiciona o jogo
            // à biblioteca se aprovado) — esse fluxo não muda com a migração da Lambda.
            services.AddRabbitMq(configuration);
            services.AddSingleton<RabbitMqIntegrationEventPublisher>();

            // SNS: publica o MESMO PaymentProcessedEvent para a Lambda do NotificationsAPI
            // (que envia o email de confirmação). NotificationsAPI é serverless, acionado
            // por SNS -> SQS, não mais por uma exchange RabbitMQ.
            services.Configure<SnsOptions>(configuration.GetSection(SnsOptions.SectionName));
            services.AddSingleton<IAmazonSimpleNotificationService>(provider =>
            {
                var options = provider.GetRequiredService<IOptions<SnsOptions>>().Value;

                // ServiceUrl só vem preenchido em teste (ex.: LocalStack). Em produção o
                // SDK resolve o endpoint real da AWS a partir da região (AWS_REGION).
                if (string.IsNullOrWhiteSpace(options.ServiceUrl))
                    return new AmazonSimpleNotificationServiceClient();

                return new AmazonSimpleNotificationServiceClient(
                    new AmazonSimpleNotificationServiceConfig { ServiceURL = options.ServiceUrl });
            });
            services.AddSingleton<SnsIntegrationEventPublisher>();

            // PaymentProcessedEvent tem dois consumidores (CatalogAPI via RabbitMQ, Lambda
            // via SNS): publica nos dois transportes.
            services.AddSingleton<IIntegrationEventPublisher, CompositeIntegrationEventPublisher>();

            return services.AddRabbitMqConsumer<OrderPlacedMessageProcessor>(
                new RabbitMqConsumerDefinition(
                    CatalogMessaging.Exchange,
                    "payments.order-placed",
                    CatalogMessaging.RoutingKeys.OrderPlaced));
        }
    }
}
