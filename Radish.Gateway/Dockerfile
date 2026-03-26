FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY . .
RUN dotnet restore "./Radish.Gateway/Radish.Gateway.csproj"
RUN dotnet publish "./Radish.Gateway/Radish.Gateway.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 5000
COPY --from=build /app/publish .
COPY --from=build /src/Certs ./certs
RUN mkdir -p /app/Logs
ENTRYPOINT ["dotnet", "Radish.Gateway.dll"]
