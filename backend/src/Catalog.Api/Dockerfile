# Build context is `backend/`, so the other projects are reachable:
#   docker build -f src/Catalog.Api/Dockerfile ./backend

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Project files first, on their own layer. Restore is the slow step, and it only needs to rerun
# when a dependency changes — not on every source edit.
COPY Directory.Build.props global.json ./
COPY src/Catalog.Domain/Catalog.Domain.csproj                 src/Catalog.Domain/
COPY src/Catalog.Application/Catalog.Application.csproj       src/Catalog.Application/
COPY src/Catalog.Infrastructure/Catalog.Infrastructure.csproj src/Catalog.Infrastructure/
COPY src/Catalog.Api/Catalog.Api.csproj                       src/Catalog.Api/

RUN dotnet restore src/Catalog.Api/Catalog.Api.csproj

COPY src/ src/

RUN dotnet publish src/Catalog.Api/Catalog.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# The aspnet image is Debian slim and ships neither wget nor curl, so a container healthcheck that
# shells out to one fails forever with "not found" — the container just sits in `starting` until
# it is marked unhealthy. Nothing noticed while the healthcheck was decorative, but compose's
# `depends_on: condition: service_healthy` and the CI deploy gate both read it. ~7 MB for a
# health signal that actually works.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Runs as a non-root user. A container that does not need root should not have it, and the
# aspnet image ships an `app` user precisely so this is a one-line decision. Note this comes
# after the apt install, which needs root.
USER $APP_UID

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true

EXPOSE 8080

HEALTHCHECK --interval=10s --timeout=5s --retries=5 --start-period=20s \
    CMD curl --fail --silent http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "Catalog.Api.dll"]
