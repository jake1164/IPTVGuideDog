FROM mcr.microsoft.com/dotnet/nightly/sdk:10.0

RUN apt-get update \
    && apt-get install -y --no-install-recommends python3 curl ca-certificates diffutils \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /opt/integration

COPY cli/tests/integration /opt/integration

ENTRYPOINT ["bash", "/opt/integration/entrypoint.sh"]
