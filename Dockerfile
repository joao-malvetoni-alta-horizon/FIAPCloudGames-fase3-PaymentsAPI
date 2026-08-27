# Build multi-stage otimizado para produção: restaura e publica a FCG.API na imagem SDK e
# copia apenas o resultado para uma imagem de runtime enxuta.
# Contexto de build: a raiz do repositório (ver docker-compose.yml / .dockerignore).

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia primeiro os arquivos de projeto (e os props/editorconfig da raiz que eles importam)
# para aproveitar o cache de camadas: o restore só re-executa se algum .csproj mudar.
COPY Directory.Build.props .editorconfig ./
COPY src/FCG.Domain/FCG.Domain.csproj src/FCG.Domain/
COPY src/FCG.Application/FCG.Application.csproj src/FCG.Application/
COPY src/FCG.Infrastructure/FCG.Infrastructure.csproj src/FCG.Infrastructure/
COPY src/FCG.API/FCG.API.csproj src/FCG.API/
RUN dotnet restore src/FCG.API/FCG.API.csproj

COPY src/ src/
RUN dotnet publish src/FCG.API/FCG.API.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Executa como usuário não-root (definido nas imagens oficiais .NET).
USER $APP_UID

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "FCG.API.dll"]
