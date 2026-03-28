FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
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
