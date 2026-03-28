FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY . .
RUN dotnet restore "./Radish.Auth/Radish.Auth.csproj"
RUN dotnet publish "./Radish.Auth/Radish.Auth.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 5200
RUN apt-get update \
    && apt-get install -y --no-install-recommends openssl ca-certificates \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
COPY --from=build /src/Certs ./certs
COPY --from=build /src/Scripts/docker ./scripts
RUN chmod +x /app/scripts/*.sh \
    && mkdir -p /app/DataBases /app/Logs
ENTRYPOINT ["/app/scripts/auth-entrypoint.sh"]
