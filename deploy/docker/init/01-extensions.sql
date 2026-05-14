-- PROPIA - Extensiones PostgreSQL necesarias
-- Este script se ejecuta automaticamente la primera vez que el contenedor
-- inicializa la base de datos (cuando el volumen postgres-data esta vacio).
-- Si necesitas re-ejecutar: docker compose down -v && docker compose up -d

\connect propia_dev;

-- uuid_generate_v4() para Guids generados en SQL
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Busqueda por similitud (trigramas) para buscar personas, unidades, etc.
CREATE EXTENSION IF NOT EXISTS "pg_trgm";

-- Texto case-insensitive (util para emails)
CREATE EXTENSION IF NOT EXISTS "citext";

-- Verificacion
SELECT extname, extversion FROM pg_extension
WHERE extname IN ('uuid-ossp', 'pg_trgm', 'citext')
ORDER BY extname;
