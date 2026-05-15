namespace Propia.Domain.Enums;

/// <summary>Genero registrado de una persona natural. Spec 2.4 v1.0.</summary>
public enum GeneroPersona
{
    Masculino = 1,
    Femenino = 2,
    NoBinario = 3,
    NoIdentificado = 4
}

/// <summary>Canal por donde el titular acepto el tratamiento de datos (Ley 1581 de 2012).</summary>
public enum CanalAceptacionDatos
{
    FormularioWeb = 1,
    Qr = 2,
    Importacion = 3,
    Manual = 4
}

/// <summary>Estado de una persona o empresa en el Directorio. Spec 2.4 RN-04.</summary>
public enum EstadoDirectorio
{
    Activo = 1,
    Inactivo = 2
}

/// <summary>Tipo de contacto en el Directorio (multiples por entidad).</summary>
public enum TipoContacto
{
    Email = 1,
    Telefono = 2,
    Whatsapp = 3,
    Direccion = 4,
    Otro = 5
}

/// <summary>Visibilidad de un contacto (spec RN-08).</summary>
public enum VisibilidadContacto
{
    /// <summary>Visible para cualquier copropiedad donde la persona tenga vinculo.</summary>
    Plataforma = 1,
    /// <summary>Solo visible para la copropiedad que lo capturo (default).</summary>
    Copropiedad = 2,
    /// <summary>Solo visible para el titular del dato.</summary>
    Privado = 3
}

/// <summary>Tipo de entidad referenciada por contacto, vinculo, auditoria.</summary>
public enum EntidadDirectorio
{
    Persona = 1,
    Empresa = 2
}

/// <summary>Estado del vinculo persona/empresa con una copropiedad.</summary>
public enum EstadoVinculo
{
    Activo = 1,
    Inactivo = 2,
    /// <summary>Spec 2.4 RN-13: autoregistro via QR generico queda pendiente de validar.</summary>
    PendienteValidacion = 3
}

/// <summary>Grupo de etiquetas - separa identidad de cargo (spec seccion 5).</summary>
public enum GrupoEtiqueta
{
    Identidad = 1,
    Cargo = 2
}

/// <summary>A que entidad aplica una etiqueta del catalogo.</summary>
public enum AplicaEtiqueta
{
    Persona = 1,
    Empresa = 2,
    Ambos = 3
}
