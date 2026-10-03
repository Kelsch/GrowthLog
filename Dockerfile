# syntax=docker/dockerfile:1

# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first (better layer caching).
COPY src/GrowthLog.Web/GrowthLog.Web.csproj src/GrowthLog.Web/
RUN dotnet restore src/GrowthLog.Web/GrowthLog.Web.csproj

# Copy the rest of the source and publish.
COPY . .
RUN dotnet publish src/GrowthLog.Web/GrowthLog.Web.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Run as a non-root user. The /data volume is owned by this user so SQLite
# can create and write the database file.
RUN adduser --disabled-password --gecos "" --uid 10001 appuser \
    && mkdir -p /data \
    && chown -R appuser:appuser /data /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    ConnectionStrings__GrowthLog="Data Source=/data/growthlog.db"

EXPOSE 8080

USER appuser

ENTRYPOINT ["dotnet", "GrowthLog.Web.dll"]
