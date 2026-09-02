FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/FCG.Domain/FCG.Domain.csproj src/FCG.Domain/
COPY src/FCG.Application/FCG.Application.csproj src/FCG.Application/
COPY src/FCG.Infrastructure/FCG.Infrastructure.csproj src/FCG.Infrastructure/
COPY src/FCG.API/FCG.API.csproj src/FCG.API/

RUN dotnet restore src/FCG.API/FCG.API.csproj

COPY src/ src/

RUN dotnet publish src/FCG.API/FCG.API.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# ---------------------------------------------------------------------------------------
# Observabilidade: agente APM do New Relic (Tech Challenge Fase 3, opção "APM gerenciado").
# O pacote NuGet NewRelic.Agent publica o agente em /app/newrelic; o profiler do CoreCLR só
# é carregado quando as variáveis CORECLR_* abaixo existem, então a imagem já sobe
# instrumentada. A license key NÃO fica aqui: vem de Kubernetes Secret (k8s/deployment.yaml)
# ou do ambiente do host (docker-compose). Sem ela o agente apenas não conecta.
# ---------------------------------------------------------------------------------------
ENV CORECLR_ENABLE_PROFILING=1 \
    CORECLR_PROFILER={36032161-FFC0-4B61-B559-F6C5D41BAE5A} \
    CORECLR_NEWRELIC_HOME=/app/newrelic \
    CORECLR_PROFILER_PATH=/app/newrelic/libNewRelicProfiler.so \
    NEW_RELIC_APP_NAME=FCG-PaymentsAPI \
    NEW_RELIC_DISTRIBUTED_TRACING_ENABLED=true \
    NEW_RELIC_APPLICATION_LOGGING_ENABLED=true \
    NEW_RELIC_APPLICATION_LOGGING_FORWARDING_ENABLED=true \
    NEW_RELIC_APPLICATION_LOGGING_LOCAL_DECORATING_ENABLED=true

# Mesmo destino de log da imagem da raiz (Dockerfile do repositório), que roda como não-root
# e não conseguiria escrever no diretório padrão /app/newrelic/logs. Mantido igual aqui para
# as duas imagens se comportarem da mesma forma; o stdout é onde o Docker/Kubernetes coleta.
RUN mkdir -p /tmp/newrelic && chmod 1777 /tmp/newrelic
ENV NEW_RELIC_LOG_DIRECTORY=/tmp/newrelic \
    NEW_RELIC_PROFILER_LOG_DIRECTORY=/tmp/newrelic \
    NEW_RELIC_LOG_CONSOLE=true

EXPOSE 8080

ENTRYPOINT ["dotnet", "FCG.API.dll"]
