# ===== Stage 1: Build =====
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["Basket.Filter.csproj", "."]
RUN dotnet restore "./Basket.Filter.csproj"

# Copy all source files
COPY . .

# Build project
RUN dotnet build "./Basket.Filter.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish project
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Basket.Filter.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# ===== Stage 2: Runtime =====
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Copy published app
COPY --from=publish /app/publish .

# Copy both service account keys
COPY firestore-key.json /app/firestore-key.json
COPY vertexai-key.json /app/vertexai-key.json

# Cloud Run environment variables
ENV ASPNETCORE_URLS=http://*:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV GOOGLE_APPLICATION_CREDENTIALS=/app/vertexai-key.json
ENV FIRESTORE_CREDENTIALS_PATH=/app/firestore-key.json
ENV VERTEX_AI_CREDENTIALS_PATH=/app/vertexai-key.json
ENV GOOGLE_CLOUD_PROJECT=basket-filter-engine
ENV PORT=8080

# Entry point
ENTRYPOINT ["dotnet", "Basket.Filter.dll"]