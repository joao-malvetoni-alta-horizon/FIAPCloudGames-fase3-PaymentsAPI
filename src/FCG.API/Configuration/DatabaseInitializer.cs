using FCG.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace FCG.API.Configuration;

public static class DatabaseInitializer
{
    private const int MaxRetries = 10;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(3);

    public static async Task MigrateAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        for (var attempt = 1; attempt <= MaxRetries; attempt++)
            try
            {
                await db.Database.MigrateAsync();
                return;
            }
            catch (Exception ex) when (attempt < MaxRetries)
            {
                app.Logger.LogWarning(ex, "Database not ready yet, retrying in {Delay}s... ({Attempt}/{MaxRetries})",
                    RetryDelay.TotalSeconds, attempt, MaxRetries);
                await Task.Delay(RetryDelay);
            }
    }
}
