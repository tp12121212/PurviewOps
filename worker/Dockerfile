FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/PurviewOps.Worker.csproj src/
RUN dotnet restore src/PurviewOps.Worker.csproj
COPY src/ src/
RUN dotnet publish src/PurviewOps.Worker.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/powershell:7.4-ubuntu-22.04 AS ps
RUN pwsh -NoProfile -Command "Install-Module ExchangeOnlineManagement -Scope AllUsers -Force"

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=ps /usr/local/share/powershell /usr/local/share/powershell
COPY --from=ps /opt/microsoft/powershell /opt/microsoft/powershell
ENTRYPOINT ["dotnet", "PurviewOps.Worker.dll"]
