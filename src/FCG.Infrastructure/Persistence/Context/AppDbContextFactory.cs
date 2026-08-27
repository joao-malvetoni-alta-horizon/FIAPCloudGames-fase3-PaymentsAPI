using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FCG.Infrastructure.Persistence.Context;

/// <summary>
/// Fábrica usada apenas em tempo de design (ex.: <c>dotnet ef migrations add</c>).
/// Evita que o pipeline de inicialização da API (migrate) seja executado durante a geração
/// de migrations.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                               ?? "Host=localhost;Port=5432;Database=fcg_payments;Username=fcg;Password=fcg123";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new AppDbContext(options);
    }
}
