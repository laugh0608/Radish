FROM node:24-bookworm-slim AS sqlite-node
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
COPY --from=sqlite-node /usr/local/bin/node /usr/local/bin/node
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY . .
RUN dotnet restore "./Radish.DbMigrate/Radish.DbMigrate.csproj"
RUN dotnet publish "./Radish.DbMigrate/Radish.DbMigrate.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
# CVE-2026-109518: disable download retries until APT itself is patched.
RUN apt-get update -o Acquire::Retries=0 \
    && apt-get install -o Acquire::Retries=0 -y --no-install-recommends --only-upgrade apt libapt-pkg6.0t64 \
    && dpkg --compare-versions "$(dpkg-query -W -f='${Version}' apt)" ge 2.8.4ubuntu0.1 \
    && dpkg --compare-versions "$(dpkg-query -W -f='${Version}' libapt-pkg6.0t64)" ge 2.8.4ubuntu0.1 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
RUN mkdir -p /app/DataBases /app/Logs
ENTRYPOINT ["dotnet", "Radish.DbMigrate.dll"]
CMD ["apply"]
