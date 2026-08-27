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

EXPOSE 8080

ENTRYPOINT ["dotnet", "FCG.API.dll"]
