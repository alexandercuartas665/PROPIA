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
        bool peligro = true)
    {
        var res = await modals.OpenAsync<ConfirmModal, bool>(
            new ModalParameters()
                .Set("Mensaje", mensaje)
                .Set("Titulo", titulo)
                .Set("Confirmar", confirmar)
                .Set("Cancelar", cancelar)
                .Set("Peligro", peligro),
            new ModalOptions { Width = "440px" });
        return res == true;
    }
}
