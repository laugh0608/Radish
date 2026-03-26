FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY . .
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
