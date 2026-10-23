# ==============================================================================
# Dockerfile Multi-Etapa para Sparnarok API (.NET 10)
# Optimizado para Render y despliegue en contenedores Docker de producción
# ==============================================================================

# ------------------------------------------------------------------------------
# Etapa 1: Compilación y Publicación (SDK .NET 10)
# ------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# 1. Copia aislada del .csproj para maximizar el uso de caché de capas en Docker
COPY Sparnarok.Api.csproj ./
RUN dotnet restore Sparnarok.Api.csproj

# 2. Copia del código fuente del backend y publicación de binarios en modo Release
COPY . ./
RUN dotnet publish Sparnarok.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# ------------------------------------------------------------------------------
# Etapa 2: Entorno de Ejecución Ligero (ASP.NET Core 10 Runtime)
# ------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Configuración obligatoria para el enrutamiento de tráfico en Render
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

# Copia de los binarios compilados desde la etapa de build
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Sparnarok.Api.dll"]
