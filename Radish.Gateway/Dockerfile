FROM node:24-bookworm-slim AS sqlite-node
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
COPY --from=sqlite-node /usr/local/bin/node /usr/local/bin/node
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY . .
RUN dotnet restore "./Radish.Gateway/Radish.Gateway.csproj"
RUN dotnet publish "./Radish.Gateway/Radish.Gateway.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 5000
# CVE-2026-109518: disable download retries until APT itself is patched.
RUN apt-get update -o Acquire::Retries=0 \
    && apt-get install -o Acquire::Retries=0 -y --no-install-recommends --only-upgrade apt libapt-pkg6.0t64 \
    && dpkg --compare-versions "$(dpkg-query -W -f='${Version}' apt)" ge 2.8.4ubuntu0.1 \
    && dpkg --compare-versions "$(dpkg-query -W -f='${Version}' libapt-pkg6.0t64)" ge 2.8.4ubuntu0.1 \
    && apt-get install -y --no-install-recommends openssl ca-certificates \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
COPY --from=build /src/Certs ./certs
COPY --from=build /src/Scripts/docker ./scripts
RUN chmod +x /app/scripts/*.sh \
    && mkdir -p /app/Logs
ENTRYPOINT ["/app/scripts/gateway-entrypoint.sh"]
