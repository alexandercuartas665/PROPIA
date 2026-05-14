# CLAUDE.md - PROPIA

## Que es PROPIA

PROPIA es un SaaS multi-tenant de gestion integral de copropiedades (propiedad horizontal en Colombia, Ley 675 de 2001) operado por **A&D GROUP S.A.S**. Cliente y autor: **Alex Alvis**.

**25 modulos en 3 capas:**

- **Capa 0** - Operador A&D GROUP (4 modulos: 0.1 a 0.4)
- **Capa 1** - Organizacion / Multi-tenant (5 modulos: 1.1 a 1.5)
- **Capa 2** - Copropiedad / Tenant (16 modulos: 2.1 a 2.16)

## Fuente de verdad funcional - Vault Obsidian

```
C:\Users\acuartas\Documents\Personal\OneDrive\Clientes\05. Propia\02. Obsidian\PROPIA\PROPIA\
```

Archivos clave:

- `02. INVENTARIO MODULOS\INVENTARIO GENERAL.md` - Indice navegable de los 25 modulos + matriz de dependencias + tracker.
- `02. INVENTARIO MODULOS\STACK TECNOLOGICO.md` - Stack tecnico y alternativas evaluadas.
- `02. INVENTARIO MODULOS\HOJA DE RUTA DESARROLLO.md` - Hoja de ruta paso a paso del desarrollo.
- `01. REQUERIMIENTO\PropIA_Modulos_Maestro.md` - Referencia macro + 12 principios de arquitectura.
- `01. REQUERIMIENTO\PropIA_Roadmap_Dev.md` - Roadmap, fases y alcance MVP por modulo.
- `01. REQUERIMIENTO\Capa X. .../*.md` - Especificacion detallada por modulo.

**Existe una skill `consultar-obsidian-propia` en `.claude/skills/` que se activa cuando hay dudas funcionales.** USAR siempre que se vaya a implementar un modulo, crear una entidad, definir un endpoint o decidir un patron.

## Stack tecnico (resumen)

- **Backend**: ASP.NET Core Web API (.NET 9 LTS, migrar a .NET 10 cuando GA en nov 2026)
- **Frontend**: Blazor Web App modo Auto (Server + WASM)
- **Lenguaje**: C# 13
- **DB**: PostgreSQL 17 en Docker (dev, puerto **5433** del host) / RDS PostgreSQL (prod)
- **ORM**: EF Core 9 + Dapper para reportes pesados
- **Auth**: ASP.NET Core Identity + OpenIddict (OAuth2/OIDC)
- **Jobs**: Hangfire con PostgreSQL
- **Cache**: Redis 7 (dev, puerto **6380** del host)
- **UI components**: MudBlazor + assets de plantilla NexLink (Bootstrap 5)
- **Logs**: Serilog + OpenTelemetry
- **IA**: Anthropic SDK (Claude Haiku/Sonnet)
- **WhatsApp**: Meta Business Cloud API
- **Pagos**: Wompi API (Colombia)
- **Hosting**: Railway (MVP) -> AWS ECS Fargate (escala)

## Arquitectura

- **Clean Architecture / Onion**: `Propia.Domain` <- `Propia.Application` <- `Propia.Infrastructure` <- `Propia.Api` / `Propia.Web`.
- `Propia.Domain` no depende de nada. No conoce EF Core ni ASP.NET.
- `Propia.Application` define casos de uso, interfaces (`ITenantContext`, repos).
- `Propia.Infrastructure` implementa EF Core, integraciones externas.
- `Propia.Shared` contiene DTOs y validaciones compartidas API/Web.
- `Propia.SuperAdmin` es Blazor separado para Capa 0 (URL distinta, sin selector tenant).

## Multi-tenancy (regla critica)

- **Cada tabla operativa de Capa 2 tiene `tenant_id`** (Guid).
- **PostgreSQL Row-Level Security (RLS)** activa sobre `tenant_id` - red de seguridad final.
- **EF Core `HasQueryFilter`** sobre `TenantEntity` - red de seguridad de aplicacion.
- **Middleware** lee `tenant_id` del JWT y ejecuta `SET LOCAL app.tenant_id = ...` por request.
- **Tablas globales** (sin tenant_id): `tenants`, `organizaciones`, `personas`, `super_admin_*`.
- **NUNCA** escribir una query SQL raw sin filtro de tenant_id (a menos que se opere sobre tablas globales).
- **Test `TenantIsolationTests`** corre en CI y bloquea merge si el aislamiento se rompe.

## Convenciones de codigo

- **Solo ASCII en codigo y comentarios.** Sin tildes, sin "n con tilde", sin emojis. (regla del proyecto)
- **Espanol en mensajes de usuario** (UI, errores legibles), **ingles en codigo** (clases, metodos, variables, comentarios tecnicos).
- **Naming**: clases en `PascalCase` singular (`Tenant`, `Persona`), tablas en `snake_case` plural (`tenants`, `personas`).
- **IDs en `Guid`** salvo justificacion explicita.
- **Enums** en `Propia.Domain.Enums` o anidados en la entidad cuando son especificos.
- **DTOs** en `Propia.Shared` con sufijo `Dto` o `Request`/`Response`.

## Reglas de testing

- **Test unitario** por cada caso de uso en `Propia.Application`.
- **Test de integracion** con Testcontainers (PostgreSQL real) para flujos que cruzan capas.
- **Tests de aislamiento de tenant** son obligatorios para cada entidad nueva con `tenant_id`.
- `dotnet test` debe pasar verde antes de cada commit.

## Checklist pre-commit

- [ ] `dotnet build` sin warnings nuevos.
- [ ] `dotnet test` todos verdes (especialmente `TenantIsolationTests`).
- [ ] `dotnet format` sin cambios pendientes.
- [ ] El cambio tiene test unitario o de integracion nuevo (cuando aplica).
- [ ] El cambio no expone datos entre tenants.
- [ ] Si toca un modulo del INVENTARIO, actualizar version/fecha/estado en su nota Obsidian.

## Estado actual del desarrollo

- **Fase**: Setup inicial (paso 1-2 de la HOJA DE RUTA DESARROLLO).
- **Modulo activo**: ninguno - primero llegamos a tener BD corriendo.
- **Proximo modulo a implementar**: `0.1 Super Admin Console` (bloqueante absoluto del roadmap).

## Orden estricto de construccion

```
0.1 -> 0.2 -> 2.1 -> 2.3 -> 2.4 -> 2.5 -> 1.3 -> 1.1 -> 2.2 -> 2.10
```

No saltarse pasos sin justificacion. Ver `INVENTARIO GENERAL.md` seccion 4 para matriz completa.

## Puertos del entorno de desarrollo

| Servicio | Puerto host | Puerto interno | URL |
|----------|-------------|----------------|-----|
| PostgreSQL | 5433 | 5432 | Owner: `Username=propia` (superuser - solo migraciones). App: `Username=propia_app;Password=PropiaAppDev2026!` (runtime - respeta RLS) |
| Redis | 6380 | 6379 | `localhost:6380` |
| pgAdmin | 5050 | 80 | http://localhost:5050 (login `admin@propia.com.co`) |

> Puertos alternos (5433/6380) porque el entorno del usuario ya tiene otros contenedores usando 5432/6379. Dentro de la red Docker `propia-net`, los servicios usan los puertos nativos.

## Comandos frecuentes

```bash
# Levantar BD
cd deploy/docker && docker compose up -d

# Build + tests
dotnet build && dotnet test

# Migrations
cd src/Propia.Api
dotnet ef migrations add <Nombre> --project ../Propia.Infrastructure --startup-project .
dotnet ef database update --project ../Propia.Infrastructure --startup-project .

# Format
dotnet format

# Run API
cd src/Propia.Api && dotnet run

# Run Web
cd src/Propia.Web && dotnet run
```
