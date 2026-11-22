# syntax=docker/dockerfile:1

# ---------------------------------------------------------------------------
# Taiga MCP server image.
#
#  - the application is installed in /opt/mcp;
#  - the process runs as an unprivileged user (app, UID 1654);
#  - startup parameters come from environment variables (ENV / --env-file);
#  - configuration (appsettings.Local.json) and certificates are mounted as files
#    at startup, not baked into the image (see the "Docker" section of README.md).
# ---------------------------------------------------------------------------

# ------------------------------ Build --------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Manifests first, so the restore layer is cached between builds.
COPY global.json ./
COPY TaigaMcp.csproj ./
RUN dotnet restore TaigaMcp.csproj

# Then the source code, published to /opt/mcp inside the intermediate image.
COPY . .
RUN dotnet publish TaigaMcp.csproj \
        -c Release \
        -o /opt/mcp \
        --no-restore \
        /p:UseAppHost=false

# ------------------------------ Runtime ------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

# The application lives in /opt/mcp.
WORKDIR /opt/mcp

# Default values. Any of them can be overridden through ENV / --env-file
# or a mounted appsettings.Local.json.
ENV DOTNET_EnableDiagnostics=0 \
    MCP_HOST=0.0.0.0 \
    MCP_PORT=8080 \
    AUDIT_FILE_PATH=/opt/mcp/logs/audit.log

COPY --from=build /opt/mcp /opt/mcp

# Directories for the audit log and certificates (mount points). curl is for HEALTHCHECK.
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
 && mkdir -p /opt/mcp/logs /opt/mcp/certs \
 && chown -R app:app /opt/mcp

# Run as non-root: the app user (UID 1654) already exists in .NET 8 images.
USER $APP_UID

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD curl -fsS "http://127.0.0.1:${MCP_PORT}/health/live" || exit 1

ENTRYPOINT ["dotnet", "/opt/mcp/TaigaMcp.dll"]
