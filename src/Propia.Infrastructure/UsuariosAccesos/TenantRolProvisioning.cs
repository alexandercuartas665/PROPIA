namespace Propia.Infrastructure.UsuariosAccesos;

/// <summary>
/// Provisioning de roles al CREAR una copropiedad (modulo 2.5, spec RN-07). En vez de insertar el
/// UsuarioTenant administrador con rol_id NULL y depender del link perezoso (EnsureRolesBaseAsync, que
/// solo corre cuando alguien abre /roles), la copropiedad "nace" con su admin YA enlazado a su rol base
/// "Administrador": se setea usuarios_tenant.rol_id y se crea su fila en usuario_tenant_roles (patron
/// canonico de CambiarRolAsync: rol principal + fila en la tabla puente multi-rol).
///
/// El catalogo de roles base es GLOBAL (roles_copropiedad con tenant_id NULL); aqui NO se duplican roles,
/// solo se enlaza el admin. Si por lo que sea el rol base "Administrador" no existiera, rol_id queda NULL
/// y el link perezoso lo completa luego (fail-safe, sin romper la creacion).
///
/// El SQL fija app.tenant_id al tenant NUEVO al inicio (la RLS de usuarios_tenant / usuario_tenant_roles
/// exige tenant_id = current_tenant_id()); debe ejecutarse en la misma sesion/conexion donde el flujo
/// hace su trabajo con set_config (y, si la conexion es compartida por el request, restaurar el tenant
/// activo al terminar, como ya hacen los flujos). Se interpolan GUIDs (no texto de usuario), siguiendo
/// el patron de SQL crudo ya presente en estos flujos.
/// </summary>
internal static class TenantRolProvisioning
{
    /// <summary>
    /// SQL (3 sentencias) que fija el tenant, inserta el UsuarioTenant admin con rol_id = rol base
    /// "Administrador" y crea su fila en usuario_tenant_roles (solo si el rol base existe).
    /// </summary>
    public static string InsertarAdminConRolSql(Guid tenantId, Guid personaId)
    {
        var usuarioTenantId = Guid.NewGuid();
        var usuarioTenantRolId = Guid.NewGuid();
        return $@"
            SELECT set_config('app.tenant_id', '{tenantId}', false);
            INSERT INTO usuarios_tenant (id, tenant_id, persona_id, rol, rol_id, estado, fecha_activacion, created_at)
            VALUES ('{usuarioTenantId}', '{tenantId}', '{personaId}', 'Administrador',
                    (SELECT id FROM roles_copropiedad
                     WHERE nombre = 'Administrador' AND tipo = 2 AND tenant_id IS NULL
                     ORDER BY created_at LIMIT 1),
                    1, now(), now());
            INSERT INTO usuario_tenant_roles (id, tenant_id, usuario_tenant_id, rol_id, created_at)
            SELECT '{usuarioTenantRolId}', '{tenantId}', '{usuarioTenantId}', id, now()
            FROM roles_copropiedad
            WHERE nombre = 'Administrador' AND tipo = 2 AND tenant_id IS NULL
            ORDER BY created_at LIMIT 1;";
    }
}
