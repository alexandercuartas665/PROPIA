using System.Threading.Tasks;

namespace Propia.Web.Components.Shared.Modals;

/// <summary>
/// Helper para pedir una confirmacion in-app (reemplazo del confirm() nativo del navegador).
/// Devuelve true solo si el usuario confirma; false si cancela o cierra por backdrop.
/// Uso: if (!await Modals.ConfirmAsync("Eliminar esto?")) return;
/// Requiere que la pagina tenga un &lt;ModalHost /&gt; (como el resto de modales globales).
/// </summary>
public static class ConfirmExtensions
{
    public static async Task<bool> ConfirmAsync(
        this IModalService modals,
        string mensaje,
        string? titulo = null,
        string confirmar = "Si, continuar",
        string cancelar = "Cancelar",
        bool peligro = true,
        string? confirmacionCheck = null)
    {
        var res = await modals.OpenAsync<ConfirmModal, bool>(
            new ModalParameters()
                .Set("Mensaje", mensaje)
                .Set("Titulo", titulo)
                .Set("Confirmar", confirmar)
                .Set("Cancelar", cancelar)
                .Set("Peligro", peligro)
                .Set("ConfirmacionCheck", confirmacionCheck),
            new ModalOptions { Width = "440px" });
        return res == true;
    }

    /// <summary>
    /// Confirmacion FUERTE (doble confirmacion) para eliminar un campo dinamico desde el menu de columna:
    /// el mismo dialogo en todos los modulos, con checkbox obligatorio porque borra la definicion del campo
    /// y TODOS sus valores de forma irreversible. Devuelve true solo si el usuario marca el check y confirma.
    /// </summary>
    public static Task<bool> ConfirmEliminarCampoAsync(this IModalService modals, string label) =>
        modals.ConfirmAsync(
            $"Se elimina la definicion del campo y TODOS sus valores en todos los registros de la copropiedad. Esta accion no se puede deshacer.",
            titulo: $"Eliminar campo \"{label}\"",
            confirmar: "Eliminar campo",
            confirmacionCheck: "Entiendo que se borra el campo y todos sus valores, y que es irreversible.");
}
