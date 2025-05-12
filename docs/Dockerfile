# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj files first and restore dependencies
COPY ["src/DataProcessingService.API/DataProcessingService.API.csproj", "src/DataProcessingService.API/"]
COPY ["src/DataProcessingService.Core/DataProcessingService.Core.csproj", "src/DataProcessingService.Core/"]
COPY ["src/DataProcessingService.Infrastructure/DataProcessingService.Infrastructure.csproj", "src/DataProcessingService.Infrastructure/"]
COPY ["src/DataProcessingService.Worker/DataProcessingService.Worker.csproj", "src/DataProcessingService.Worker/"]
RUN dotnet restore "src/DataProcessingService.API/DataProcessingService.API.csproj"
RUN dotnet restore "src/DataProcessingService.Worker/DataProcessingService.Worker.csproj"

# Copy the rest of the source code
COPY ["src/", "/src/"]

# Build the API application
WORKDIR "/src/DataProcessingService.API"
RUN dotnet build "DataProcessingService.API.csproj" -c Release -o /app/build/api

# Build the Worker application
WORKDIR "/src/DataProcessingService.Worker"
RUN dotnet build "DataProcessingService.Worker.csproj" -c Release -o /app/build/worker

# Publish stage - API
FROM build AS publish-api
WORKDIR "/src/DataProcessingService.API"
RUN dotnet publish "DataProcessingService.API.csproj" -c Release -o /app/publish/api /p:UseAppHost=false

# Publish stage - Worker
FROM build AS publish-worker
WORKDIR "/src/DataProcessingService.Worker"
RUN dotnet publish "DataProcessingService.Worker.csproj" -c Release -o /app/publish/worker /p:UseAppHost=false

# Final stage - API
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final-api
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Install tools for health checks and debugging
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Copy published API application
COPY --from=publish-api /app/publish/api .

# Add healthcheck
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
  CMD curl -f http://localhost/health/ready || exit 1

# Create a non-root user to run the application
RUN addgroup --system --gid 1000 appgroup && \
    adduser --system --uid 1000 --ingroup appgroup --shell /bin/sh appuser
USER appuser

ENTRYPOINT ["dotnet", "DataProcessingService.API.dll"]

# Final stage - Worker
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final-worker
WORKDIR /app

# Copy published Worker application
COPY --from=publish-worker /app/publish/worker .

# Create a non-root user to run the application
RUN addgroup --system --gid 1000 appgroup && \
    adduser --system --uid 1000 --ingroup appgroup --shell /bin/sh appuser
USER appuser

ENTRYPOINT ["dotnet", "DataProcessingService.Worker.dll"]
