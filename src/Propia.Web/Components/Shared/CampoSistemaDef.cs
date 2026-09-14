using Propia.Domain.Enums;

namespace Propia.Web.Components.Shared;

/// <summary>
/// Forma NEUTRA de un campo del SISTEMA (de fabrica) de una entidad, para INYECTAR el catalogo en
/// <c>ConfigCamposEntidad</c> sin que el componente tenga que conocer cada entidad. Cada pagina de
/// modulo mapea su catalogo real (p.ej. Propia.Application.MiCopropiedad.UnidadCamposSistema.Todos)
/// a esta forma y lo pasa por el parametro <c>Catalogo</c>. Si no se inyecta, el componente usa su
/// diccionario interno (personas/vehiculos/mascotas/terceros), asi que los consumidores viejos no
/// cambian.
/// </summary>
/// <param name="Clave">Clave de configuracion en la config del tenant (columna de la ficha/tabla).</param>
/// <param name="Label">Nombre de fabrica (antes del alias que ponga la copropiedad).</param>
/// <param name="Tipo">Tipo de dato de la columna real.</param>
/// <param name="VisibleDefault">Si se muestra cuando la copropiedad no tiene fila de config.</param>
/// <param name="Fija">No se puede ocultar (identifica la fila; p.ej. el codigo de la unidad).</param>
/// <param name="OpcionesEditables">Solo para <see cref="TipoCampoTablero.Seleccion"/>: true = la lista
/// se administra por copropiedad (opciones/semilla/color, se guardan en la config); false = lista de
/// solo lectura cuyos valores salen de un enum del sistema.</param>
/// <param name="Semilla">Opciones de fabrica de una lista de Seleccion. Si OpcionesEditables=true, es la
/// semilla que la copropiedad puede editar/ocultar (no eliminar). Si OpcionesEditables=false (lista de
/// SOLO LECTURA), son los valores fijos del sistema que el componente muestra y cuenta sin conocer el
/// enum del dominio (SOLICITUD_SELLO_05). null = el componente cae a su enum interno por (prefijo, clave).</param>
/// <param name="Entero">El numero no admite decimales (piso, banos, habitaciones...).</param>
/// <param name="Ayuda">Texto de ayuda; se muestra como pista cuando el campo no tiene editor propio.</param>
/// <param name="TiposIntercambiables">Tipos a los que la copropiedad puede cambiar este campo del
/// sistema. null = usar la heuristica interna del componente; arreglo vacio = tipo FIJO (no cambiable);
/// con valores = solo esos tipos (p.ej. Numero/Moneda para 'area').</param>
/// <param name="ListaExterna">Solo para listas de sistema: true = la administracion de opciones la hace
/// la PAGINA (no el editor interno). El componente muestra "Gestionar lista" y dispara OnGestionarLista.
/// Se usa para "Tipo de unidad", cuyas opciones salen de un enum + una tabla de tipos propios.</param>
public sealed record CampoSistemaDef(
    string Clave,
    string Label,
    TipoCampoTablero Tipo,
    bool VisibleDefault = true,
    bool Fija = false,
    bool OpcionesEditables = false,
    string[]? Semilla = null,
    bool Entero = false,
    string? Ayuda = null,
    TipoCampoTablero[]? TiposIntercambiables = null,
    bool ListaExterna = false,
    bool SinFormato = false);
