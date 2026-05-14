# PROPIA - Infraestructura local con Docker Compose

Stack de desarrollo local: PostgreSQL 17 + pgAdmin 4 + Redis 7.

## Pre-requisitos

- Docker Desktop instalado y corriendo.
- Puertos del host: **5433** (Postgres), **5050** (pgAdmin), **6380** (Redis).
- Si tienes otros servicios Postgres/Redis ya corriendo, estos puertos evitan conflictos. Dentro de la red Docker, los servicios se ven en sus puertos nativos (5432, 6379).

## Setup (primera vez)

```bash
cp .env.example .env
# Editar .env y poner una password real en POSTGRES_PASSWORD
```

## Levantar todo

```bash
docker compose up -d
docker compose ps
```

Resultado esperado: 3 contenedores corriendo y saludables.

## Validar PostgreSQL

```bash
# Conectarse al contenedor y entrar a psql
docker exec -it propia-postgres psql -U propia -d propia_dev -c "SELECT version();"

# Verificar extensiones
docker exec -it propia-postgres psql -U propia -d propia_dev -c "SELECT extname FROM pg_extension ORDER BY extname;"
```

Debe listar: `citext`, `pg_trgm`, `plpgsql`, `uuid-ossp`.

## pgAdmin

Abrir `http://localhost:5050`. Login: `admin@propia.com.co` / password de `.env`.

Para registrar el servidor desde pgAdmin:

- Host: `postgres` (nombre del servicio en la red Docker)
- Puerto: `5432`
- Database: `propia_dev`
- User: `propia`
- Password: el de `.env`

## Redis

```bash
docker exec -it propia-redis redis-cli PING
# Respuesta: PONG
```

## Comandos utiles

```bash
docker compose down              # Apagar (conserva datos)
docker compose down -v           # Apagar y borrar volumenes (BD vacia al volver)
docker compose logs -f postgres  # Logs de Postgres en vivo
docker compose restart postgres  # Reiniciar solo Postgres
```

## Notas

- El volumen `postgres-data` persiste entre reinicios.
- El script `init/01-extensions.sql` corre SOLO la primera vez (cuando el volumen esta vacio). Para re-aplicar: `docker compose down -v && docker compose up -d`.
- En produccion no usamos este compose - va a Railway / RDS.
