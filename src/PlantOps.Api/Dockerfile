# Multi-stage build: the SDK image compiles, the much smaller ASP.NET runtime image runs.
# Build from the repository root: docker build -f src/PlantOps.Api/Dockerfile .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore with only the project files first, so this layer stays cached until dependencies change.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/PlantOps.Domain/PlantOps.Domain.csproj src/PlantOps.Domain/
COPY src/PlantOps.Application/PlantOps.Application.csproj src/PlantOps.Application/
COPY src/PlantOps.Infrastructure/PlantOps.Infrastructure.csproj src/PlantOps.Infrastructure/
COPY src/PlantOps.Api/PlantOps.Api.csproj src/PlantOps.Api/
RUN dotnet restore src/PlantOps.Api/PlantOps.Api.csproj

COPY src/ src/
RUN dotnet publish src/PlantOps.Api/PlantOps.Api.csproj --configuration Release --no-restore --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# The .NET images ship a non-root "app" user; running as it limits what a compromised process could do.
# Non-root images listen on port 8080.
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "PlantOps.Api.dll"]
