# syntax=docker/dockerfile:1

ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src

COPY global.json ./
COPY src/IPTVGuideDog.Web/IPTVGuideDog.Web.csproj src/IPTVGuideDog.Web/
COPY src/IPTVGuideDog.Core/IPTVGuideDog.Core.csproj src/IPTVGuideDog.Core/
RUN dotnet restore src/IPTVGuideDog.Web/IPTVGuideDog.Web.csproj

COPY src/ src/
RUN dotnet publish src/IPTVGuideDog.Web/IPTVGuideDog.Web.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS final
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish ./
RUN mkdir -p /app/Data \
    && chown -R app:app /app

USER app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_HTTP_PORTS=8080

VOLUME ["/app/Data"]
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
    CMD curl --fail --silent http://127.0.0.1:8080/health || exit 1

ENTRYPOINT ["dotnet", "IPTVGuideDog.Web.dll"]
