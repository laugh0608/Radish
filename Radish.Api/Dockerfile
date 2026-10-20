# Build on each target architecture; Debian 12 fixes the native glibc floor.
FROM node:24-bookworm-slim AS native-node
FROM rust:1.96.0-bookworm AS native
COPY --from=native-node /usr/local/bin/node /usr/local/bin/node
WORKDIR /src
COPY Lib/radish.lib Lib/radish.lib
COPY LICENSE LICENSE
COPY Scripts/build-native-images.mjs Scripts/build-native-images.mjs
COPY Clients/radish.flutter/assets/fonts/noto Clients/radish.flutter/assets/fonts/noto
RUN node Scripts/build-native-images.mjs

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
COPY --from=native-node /usr/local/bin/node /usr/local/bin/node
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY . .
COPY --from=native /src/artifacts/native /src/artifacts/native
RUN dotnet restore "./Radish.Api/Radish.Api.csproj"
RUN dotnet publish "./Radish.Api/Radish.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false /p:ErrorOnDuplicatePublishOutputFiles=false \
    && cp /src/Radish.Api/appsettings.json /app/publish/appsettings.json

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 5100
COPY --from=build /app/publish .
COPY --from=build /src/Docs ./Docs
RUN mkdir -p /app/DataBases/Uploads /app/Logs
ENTRYPOINT ["dotnet", "Radish.Api.dll"]
