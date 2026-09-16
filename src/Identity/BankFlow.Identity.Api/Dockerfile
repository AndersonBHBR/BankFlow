FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY . .
RUN dotnet restore src/Identity/BankFlow.Identity.Api/BankFlow.Identity.Api.csproj
RUN dotnet publish src/Identity/BankFlow.Identity.Api/BankFlow.Identity.Api.csproj --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "BankFlow.Identity.Api.dll"]
