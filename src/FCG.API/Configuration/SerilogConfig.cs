using Serilog;

namespace FCG.API.Configuration;

public static class SerilogConfig
{
    private const string OutputTemplate =
        "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}";

    public static WebApplicationBuilder AddSerilogConfig(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, loggerConfig) =>
            loggerConfig
                .ReadFrom.Configuration(context.Configuration)
                .Enrich.FromLogContext()
                .WriteTo.Console(outputTemplate: OutputTemplate));

        return builder;
    }
}
