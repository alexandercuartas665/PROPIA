# Pull Request

## Que cambia

<!-- Describe brevemente que hace este PR -->

## Por que

<!-- Por que es necesario - link al modulo del INVENTARIO si aplica -->

## Modulo afectado

<!-- Ej. 0.1 Super Admin Console, 2.3 Mi Copropiedad, "transversal" si aplica a multiples -->

## Checklist

- [ ] `dotnet build` sin warnings nuevos
- [ ] `dotnet test` todos verdes (especialmente `TenantIsolationTests`)
- [ ] `dotnet format` sin cambios pendientes
- [ ] Cambio tiene test unitario o de integracion nuevo (cuando aplica)
- [ ] **Cambio NO expone datos entre tenants** (revisar manualmente queries nuevas)
- [ ] Si toca un modulo del INVENTARIO, version/fecha/estado actualizado en su nota Obsidian
- [ ] Codigo solo ASCII (sin tildes, sin emojis)

## Como probarlo

<!-- Pasos manuales para validar el cambio -->

## Notas

<!-- Cualquier consideracion adicional, riesgo, decision tomada -->
