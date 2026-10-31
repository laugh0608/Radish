FROM node:24-bookworm-slim AS sqlite-node
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
COPY --from=sqlite-node /usr/local/bin/node /usr/local/bin/node
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY . .
RUN dotnet restore "./Radish.DbMigrate/Radish.DbMigrate.csproj"
RUN dotnet publish "./Radish.DbMigrate/Radish.DbMigrate.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
RUN mkdir -p /app/DataBases /app/Logs
ENTRYPOINT ["dotnet", "Radish.DbMigrate.dll"]
CMD ["apply"]
