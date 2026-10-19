# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY src/MssqlExporter/MssqlExporter.csproj src/MssqlExporter/
RUN dotnet restore src/MssqlExporter/MssqlExporter.csproj
COPY src ./src
RUN dotnet publish src/MssqlExporter/MssqlExporter.csproj -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime

ARG VERSION=dev
ARG REVISION=unknown
LABEL org.opencontainers.image.title="prometheus-mssql-exporter" \
      org.opencontainers.image.description="Prometheus exporter for Microsoft SQL Server" \
      org.opencontainers.image.source="https://github.com/BoBoBaSs84/prometheus-mssql-exporter" \
      org.opencontainers.image.licenses="MIT" \
      org.opencontainers.image.authors="Pierre Awaragi (pierre@awaragi.com), cobolbaby" \
      org.opencontainers.image.version="${VERSION}" \
      org.opencontainers.image.revision="${REVISION}"

# Microsoft.Data.SqlClient does not support .NET's globalization invariant mode, which the Alpine image enables.
RUN apk add --no-cache icu-libs
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    DOTNET_gcServer=0

WORKDIR /app
COPY --from=build /app ./

USER $APP_UID
EXPOSE 4000

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD wget -q -O /dev/null "http://localhost:${EXPOSE:-4000}/healthz" || exit 1

ENTRYPOINT ["dotnet", "MssqlExporter.dll"]
