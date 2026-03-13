FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/PurviewOps.Api.csproj src/
RUN dotnet restore src/PurviewOps.Api.csproj
COPY src/ src/
RUN dotnet publish src/PurviewOps.Api.csproj -c Release -o /app/publish
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PurviewOps.Api.dll"]
