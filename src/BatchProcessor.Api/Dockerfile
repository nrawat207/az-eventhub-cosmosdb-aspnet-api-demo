FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["az-eventhub-cosmosdb-aspnet-api-demo.sln", "./"]
COPY ["src/BatchProcessor.Api/BatchProcessor.Api.csproj", "src/BatchProcessor.Api/"]
COPY ["src/Shared.Contracts/Shared.Contracts.csproj", "src/Shared.Contracts/"]

RUN dotnet restore "src/BatchProcessor.Api/BatchProcessor.Api.csproj"

COPY . .
RUN dotnet publish "src/BatchProcessor.Api/BatchProcessor.Api.csproj" \
    --configuration Release \
    --output /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

RUN groupadd --gid 10001 appgroup \
    && useradd --uid 10001 --gid appgroup --create-home appuser

COPY --from=build /app/publish .
USER appuser

ENTRYPOINT ["dotnet", "BatchProcessor.Api.dll"]
