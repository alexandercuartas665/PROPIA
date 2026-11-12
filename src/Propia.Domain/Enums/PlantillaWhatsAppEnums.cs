namespace Propia.Domain.Enums;

// =====================================================================================
// Plantillas de WhatsApp (HSM). Portado de ECOREX (WhatsAppTemplate) con nombre propio en
// PROPIA para NO chocar con otros modulos de "plantillas" ya funcionales (plantillas de
// unidades en MiCopropiedad, plantillas de agente del Super Admin). El enfoque de este corte
// es IMPORTAR las plantillas HSM que ya existen en la cuenta YCloud de la linea.
// =====================================================================================

/// <summary>
/// Categoria de Meta para una plantilla HSM de WhatsApp. Determina las reglas de revision y el
/// tipo de contenido permitido. Marketing (promos, requiere opt-in), Utility (transaccional),
/// Authentication (codigos OTP, componentes especiales de Meta).
/// </summary>
public enum PlantillaWaCategoria
{
    Marketing = 1,
    Utility = 2,
    Authentication = 3
}

/// <summary>
/// Tipo de encabezado (header) de una plantilla HSM: sin header (None), texto, o media
/// (imagen/documento/video). Las plantillas importadas de YCloud suelen llegar sin la media
/// (Meta no la expone al listar); Meta la exige al enviar.
/// </summary>
public enum PlantillaWaEncabezado
{
    None = 0,
    Text = 1,
    Image = 2,
    Document = 3,
    Video = 4
}

/// <summary>
/// Estado del ciclo de aprobacion de una plantilla ante Meta: Draft -> Submitted ->
/// Approved / Rejected (Paused/Disabled los reporta el proveedor). Al IMPORTAR de YCloud, el
/// estado se mapea del que reporta el proveedor (p.ej. APPROVED -> Approved).
/// </summary>
public enum PlantillaWaEstado
{
    Draft = 0,
    Submitted = 1,
    Approved = 2,
    Rejected = 3,
    Paused = 4,
    Disabled = 5
}
