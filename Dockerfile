# syntax=docker/dockerfile:1.7
# Сборка и публикация API. Версии образов зафиксированы точными тегами.

# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0.402 AS build
WORKDIR /src
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1

# Сначала только файлы, влияющие на restore: слой с пакетами переиспользуется между сборками
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/AlfaCredit.Core/AlfaCredit.Core.csproj src/AlfaCredit.Core/packages.lock.json src/AlfaCredit.Core/
COPY src/AlfaCredit.Infrastructure/AlfaCredit.Infrastructure.csproj src/AlfaCredit.Infrastructure/packages.lock.json src/AlfaCredit.Infrastructure/
COPY src/AlfaCredit.Api/AlfaCredit.Api.csproj src/AlfaCredit.Api/packages.lock.json src/AlfaCredit.Api/
# --locked-mode: сборка падает, если packages.lock.json не соответствует csproj
RUN dotnet restore src/AlfaCredit.Api/AlfaCredit.Api.csproj --locked-mode

COPY src/ src/
RUN dotnet publish src/AlfaCredit.Api/AlfaCredit.Api.csproj \
    --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0.13 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1

# curl нужен только для healthcheck в docker compose
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /app/storage \
    && chown -R $APP_UID:$APP_UID /app/storage

COPY --from=build /app/publish ./

# Непривилегированный пользователь из базового образа (APP_UID=1654)
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "AlfaCredit.Api.dll"]
