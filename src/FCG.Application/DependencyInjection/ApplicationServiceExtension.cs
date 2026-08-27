using FCG.Application.Payments.Interfaces;
using FCG.Application.Payments.UseCases;
using Microsoft.Extensions.DependencyInjection;

namespace FCG.Application.DependencyInjection;

public static class ApplicationServiceExtension
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddApplication()
        {
            return services.AddUseCases();
        }

        private IServiceCollection AddUseCases()
        {
            return services.AddScoped<IProcessOrderPlacedUseCase, ProcessOrderPlacedUseCase>();
        }
    }
}
