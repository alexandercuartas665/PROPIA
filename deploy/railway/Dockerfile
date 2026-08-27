# syntax=docker/dockerfile:1.7
#
# Dockerfile parametrizado para PROPIA - usado por propia-api y propia-web en Railway.
#
# Build args:
#   PROJECT      - nombre del .csproj sin extension. Ej. "Propia.Api" o "Propia.Web"
#   PROJECT_DIR  - ruta relativa al .csproj. Ej. "src/Propia.Api"
#
# Uso local:
#   docker build -t propia-api -f deploy/railway/Dockerfile \
#     --build-arg PROJECT=Propia.Api --build-arg PROJECT_DIR=src/Propia.Api .
#   docker build -t propia-web -f deploy/railway/Dockerfile \
#     --build-arg PROJECT=Propia.Web --build-arg PROJECT_DIR=src/Propia.Web .
#
# En Railway: configurar build args por servicio.

ARG PROJECT=Propia.Api
ARG PROJECT_DIR=src/Propia.Api

# ---------- Build stage ----------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG PROJECT
ARG PROJECT_DIR
WORKDIR /src

# Copiar archivos de proyecto primero para cachear restore
COPY Propia.sln ./
COPY src/Propia.Domain/Propia.Domain.csproj          src/Propia.Domain/
COPY src/Propia.Application/Propia.Application.csproj src/Propia.Application/
COPY src/Propia.Infrastructure/Propia.Infrastructure.csproj src/Propia.Infrastructure/
COPY src/Propia.Shared/Propia.Shared.csproj          src/Propia.Shared/
COPY src/Propia.Api/Propia.Api.csproj                src/Propia.Api/
COPY src/Propia.Web/Propia.Web.csproj                src/Propia.Web/
COPY src/Propia.Web.Client/Propia.Web.Client.csproj  src/Propia.Web.Client/
COPY src/Propia.Workers/Propia.Workers.csproj        src/Propia.Workers/
COPY src/Propia.SuperAdmin/Propia.SuperAdmin.csproj  src/Propia.SuperAdmin/
COPY tests/Propia.Domain.Tests/Propia.Domain.Tests.csproj           tests/Propia.Domain.Tests/
COPY tests/Propia.Application.Tests/Propia.Application.Tests.csproj tests/Propia.Application.Tests/
COPY tests/Propia.Integration.Tests/Propia.Integration.Tests.csproj tests/Propia.Integration.Tests/

RUN dotnet restore "${PROJECT_DIR}/${PROJECT}.csproj"

# Copiar el resto y publicar
COPY . .
RUN dotnet publish "${PROJECT_DIR}/${PROJECT}.csproj" \
    -c Release \
    --no-restore \
    -o /app/publish \
    /p:UseAppHost=false

# ---------- Runtime stage ----------
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
ARG PROJECT
WORKDIR /app

# Chromium para HTML->PDF (PuppeteerSharp genera el PDF de las respuestas PQRSD con membrete).
# Se instala por apt y se apunta con PUPPETEER_EXECUTABLE_PATH para NO descargarlo en runtime.
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        chromium fonts-liberation fonts-dejavu-core ca-certificates \
    && rm -rf /var/lib/apt/lists/*
ENV PUPPETEER_EXECUTABLE_PATH=/usr/bin/chromium

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_HTTP_PORTS=8080
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV PROJECT_DLL=${PROJECT}.dll

EXPOSE 8080

# shell form para que ${PROJECT_DLL} se expanda en runtime
ENTRYPOINT dotnet ${PROJECT_DLL}
