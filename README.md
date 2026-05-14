# PROPIA

SaaS multi-tenant de gestion integral de copropiedades (propiedad horizontal, Ley 675 de 2001 - Colombia).

**Cliente y operador:** A&D GROUP S.A.S
**Autor:** Alex Alvis
**Estado:** En desarrollo (Fase 1 - MVP Comercial)

---

## Que es PROPIA

PROPIA es una plataforma SaaS para administradoras de copropiedades y residentes con WhatsApp como canal nativo, asistente IA integrado y arquitectura multi-tenant estricta. La data pertenece al tenant (la Copropiedad), no al administrador.

**Arquitectura:** 3 capas, 25 modulos.

- **Capa 0** - Operador A&D GROUP (4 modulos)
- **Capa 1** - Organizacion administradora (5 modulos)
- **Capa 2** - Copropiedad / Tenant (16 modulos)

La especificacion funcional completa vive en un vault Obsidian aparte. Este repositorio contiene unicamente el codigo.

---

## Stack tecnico

| Capa | Tecnologia |
|------|------------|
| Backend | ASP.NET Core Web API + .NET 9 LTS (migrar a .NET 10 cuando GA en nov 2026) |
| Frontend | Blazor Web App modo Auto (Server + WASM hibrido) |
| Portal Capa 0 | Blazor Server separado (URL distinta) |
| Lenguaje | C# 13 |
| ORM | EF Core 9 + Dapper (para reportes pesados) |
| BD | PostgreSQL 17 con Row-Level Security por `tenant_id` |
| Cache | Redis 7 |
| Jobs | Hangfire (PostgreSQL) |
| Auth | ASP.NET Core Identity + OpenIddict (OAuth2/OIDC) |
| Logs | Serilog + OpenTelemetry |
| Test | xUnit + Testcontainers (Postgres efimero por test) |
| IA | Anthropic SDK (Claude Haiku/Sonnet) |
| WhatsApp | Meta Business Cloud API |
| Pagos | Wompi (Colombia) |
| Hosting | Railway (MVP) -> AWS ECS + RDS (escala) |

---

## Estructura del repo

```
src/
  Propia.Domain          Entidades, value objects, enums (sin deps)
  Propia.Application     Casos de uso, interfaces, DTOs internos
  Propia.Infrastructure  EF Core, integraciones externas
  Propia.Shared          DTOs/validaciones compartidos API <-> Web
  Propia.Api             REST API (host principal)
  Propia.Web             Blazor Web App (cliente y admin)
  Propia.Web.Client      Cliente WASM del Web App
  Propia.SuperAdmin      Portal Capa 0 (Blazor Server, URL distinta)
  Propia.Workers         Hangfire jobs background

tests/
  Propia.Domain.Tests
  Propia.Application.Tests
  Propia.Integration.Tests   (Testcontainers - Postgres real)

deploy/
  docker/                docker-compose para dev local

.claude/
  skills/
    consultar-obsidian-propia/   Recordatorio: leer vault antes de implementar
```

---

## Setup local (~10 min la primera vez)

### Pre-requisitos

- Docker Desktop
- .NET SDK 9.0
- Git
- (Opcional) DBeaver o pgAdmin

### Levantar la infraestructura

```bash
cd deploy/docker
cp .env.example .env
# Editar .env y poner password real en POSTGRES_PASSWORD
docker compose up -d
```

3 contenedores deben quedar saludables:

```
propia-postgres   Up (healthy)   localhost:5433
propia-redis      Up (healthy)   localhost:6380
propia-pgadmin    Up             http://localhost:5050
```

### Aplicar migraciones

```bash
cd src/Propia.Api
dotnet ef database update --project ../Propia.Infrastructure --startup-project .
```

### Correr el API

```bash
dotnet run
# GET https://localhost:7xxx/health -> { "status": "ok" }
```

### Correr los tests

```bash
cd ../..
dotnet test
```

El proyecto `Propia.Integration.Tests` levanta un PostgreSQL efimero con Testcontainers y valida que la Row-Level Security esta blindando los datos entre tenants.

---

## Multi-tenancy (regla critica)

PROPIA usa **shared database + shared schema + tenant_id + Row-Level Security**:

1. Cada tabla operativa de Capa 2 lleva columna `tenant_id`.
2. PostgreSQL RLS bloquea cualquier SELECT/INSERT/UPDATE/DELETE cuyo `tenant_id` no coincida con `app.tenant_id` de la sesion (red de seguridad final).
3. EF Core `HasQueryFilter` filtra por `TenantId` (red de seguridad de aplicacion).
4. Middleware ASP.NET Core lee el claim `tenant_id` del JWT y ejecuta `SET LOCAL app.tenant_id` antes de cada query.
5. La app corre con el rol `propia_app` (NOSUPERUSER, NOBYPASSRLS). El rol `propia` (superuser) solo se usa para migraciones.
6. Tests `TenantIsolationTests` validan el aislamiento en cada build - **bloquean merge si se rompe**.

**Nunca** insertar SQL raw sin filtro de tenant en codigo que toca `TenantEntity`. Si dudas, lee `CLAUDE.md` y la skill `consultar-obsidian-propia`.

---

## Convenciones

- **Solo ASCII** en codigo y comentarios.
- **Espanol** en mensajes a usuario, **ingles** en codigo (clases, metodos, variables).
- **PascalCase** para clases (singular), **snake_case** para tablas (plural).
- **Guid** para todos los IDs.
- Test unitario por cada caso de uso.
- Test de integracion con Testcontainers para flujos que cruzan capas.

Detalles completos en `CLAUDE.md`.

---

## Estado de implementacion

Ver seccion 5 del `INVENTARIO GENERAL.md` del vault Obsidian.

**Resumen actual:**

- [x] Docker stack (Postgres + Redis + pgAdmin)
- [x] Solucion .NET Clean Architecture (12 proyectos)
- [x] Entidades base de dominio
- [x] EF Core + migracion `InitialCreate`
- [x] Row-Level Security PostgreSQL + tests de aislamiento (3/3 verdes)
- [x] TenantMiddleware
- [ ] Identity + OpenIddict (paso 6)
- [ ] CI con GitHub Actions (paso 6.5)
- [ ] Integracion plantilla NexLink en Blazor (paso 7)
- [ ] Modulo 0.1 Super Admin Console (paso 8)

---

## Licencia

Propietaria - A&D GROUP S.A.S. Ver `LICENSE`.
