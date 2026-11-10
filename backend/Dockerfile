# Multi-stage production build for ACUD GeoManager .NET API
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy CPM and project files for layer caching
COPY Directory.Build.props Directory.Packages.props ./
COPY src/UtilityNetwork.Core/UtilityNetwork.Core.csproj src/UtilityNetwork.Core/
COPY src/UtilityNetwork.Data/UtilityNetwork.Data.csproj src/UtilityNetwork.Data/
COPY src/UtilityNetwork.Api/UtilityNetwork.Api.csproj src/UtilityNetwork.Api/

RUN dotnet restore src/UtilityNetwork.Api/UtilityNetwork.Api.csproj

# Copy remaining source code
COPY src/UtilityNetwork.Core/ src/UtilityNetwork.Core/
COPY src/UtilityNetwork.Data/ src/UtilityNetwork.Data/
COPY src/UtilityNetwork.Api/ src/UtilityNetwork.Api/

WORKDIR /src/src/UtilityNetwork.Api
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime Image with non-root security
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Ensure uploads directory is owned and writable
RUN mkdir -p /app/uploads/attachments && chown -R app:app /app

USER app

COPY --from=build --chown=app:app /app/publish .

ENV ASPNETCORE_HTTP_PORTS=5000
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 5000

ENTRYPOINT ["dotnet", "UtilityNetwork.Api.dll"]
