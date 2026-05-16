namespace Propia.Domain.Enums;

/// <summary>Tipo de evento interno del calendario. Spec 1.2 seccion 8.1.</summary>
public enum TipoEventoInterno
{
    RecordatorioPersonal = 1,
    RecordatorioEquipo = 2,
    Bloqueo = 3
}

/// <summary>Vista por defecto del calendario. Spec 1.2 seccion 4.1.</summary>
public enum VistaCalendario
{
    Agenda = 1,
    Mes = 2,
    Semana = 3,
    Dia = 4
}

/// <summary>Categoria de un evento agregado en el calendario - determina el color base.</summary>
public enum CategoriaEvento
{
    Asamblea = 1,           // 2.8 - Purpura
    VencimientoContrato = 2, // 2.3 - Rojo
    Tarea = 3,              // 2.10 - Azul
    Mantenimiento = 4,      // 2.11 - Naranja
    Pqrsd = 5,              // 2.9 - Ambar
    BloqueoZona = 6,        // 2.13 - Gris
    Interno = 7             // Calendario interno - Verde
}

/// <summary>Severidad de un evento critico para la pestana de Criticos (spec 1.2 seccion 4.2).</summary>
public enum SeveridadCritico
{
    Rojo = 1,
    Naranja = 2,
    Amarillo = 3
}
