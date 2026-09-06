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
builder.Services.AddSwaggerConfig();

var app = builder.Build();

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
    app.UseSwaggerConfig();

app.MapHealthCheck();
app.MapControllers();

await app.MigrateAsync();

app.Run();

// Exposto para testes de integração (WebApplicationFactory<Program>).
public partial class Program;
