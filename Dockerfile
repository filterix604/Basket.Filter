# Dockerfile (Complete Linux version)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Basket.Filter.csproj", "."]
RUN dotnet restore "./Basket.Filter.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "./Basket.Filter.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Basket.Filter.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Copy Firestore credentials
COPY firestore-key.json /app/firestore-key.json

# Cloud Run environment variables
ENV ASPNETCORE_URLS=http://*:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV GOOGLE_APPLICATION_CREDENTIALS=/app/firestore-key.json

ENTRYPOINT ["dotnet", "Basket.Filter.dll"]