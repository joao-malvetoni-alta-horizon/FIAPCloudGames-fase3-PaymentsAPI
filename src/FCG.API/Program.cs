using FCG.API.Configuration;
using FCG.API.Endpoints;
using FCG.Application.DependencyInjection;
using FCG.Infrastructure.DependencyInjection;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.AddSerilogConfig();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddControllers();

var app = builder.Build();

app.UseSerilogRequestLogging();

app.MapHealthCheck();
app.MapControllers();

await app.MigrateAsync();

app.Run();

// Exposto para testes de integração (WebApplicationFactory<Program>).
public partial class Program;
