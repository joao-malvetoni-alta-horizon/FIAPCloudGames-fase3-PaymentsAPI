namespace FCG.API.Endpoints;

/// <summary>
/// Endpoints para health check da aplicação.
/// </summary>
public static class HealthCheckEndpoints
{
    public static void MapHealthCheck(this WebApplication app)
    {
        app.MapGet("/health", GetHealth)
            .WithName("HealthCheck")
            .WithTags("Health")
            .WithDescription("Verifica o status de saúde da API");
    }

    private static IResult GetHealth()
    {
        return Results.Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            service = "Payments API"
        });
    }
}
