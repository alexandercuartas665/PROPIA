namespace Propia.Domain.Enums;

/// <summary>Estado del turno de porteria. Spec 2.12 seccion 6 + RN-01.</summary>
public enum EstadoTurnoPorteria
{
    Activo = 1,
    Cerrado = 2
}

/// <summary>Tipo de visitante. Spec 2.12 seccion 4.1.</summary>
public enum TipoVisitante
{
    VisitaPersonal = 1,
    Proveedor = 2,
    Delivery = 3,
    PersonalExterno = 4,
    Otro = 5
}

/// <summary>Tipo de documento del visitante. Spec 2.12 seccion 7.</summary>
public enum TipoDocumentoVisitante
{
    Cc = 1,
    Ce = 2,
    Pasaporte = 3,
    Otro = 4
}

/// <summary>Tipo de evento (ingreso o egreso). Spec 2.12 seccion 7 + 8.</summary>
public enum TipoEventoAccesoPorteria
{
    Ingreso = 1,
    Egreso = 2
}

/// <summary>Destino del visitante (unidad o zona comun). Spec 2.12 seccion 7 + RN-07.</summary>
public enum DestinoVisita
{
    UnidadPrivada = 1,
    ZonaComun = 2
}

/// <summary>Origen de la autorizacion previa. Spec 2.12 seccion 4.4.</summary>
public enum OrigenAutorizacion
{
    PortalResidente = 1,
    WhatsappIa = 2,
    Administrador = 3
}

/// <summary>Estado de la autorizacion previa. Spec 2.12 seccion 10 + RN-03/RN-05.</summary>
public enum EstadoAutorizacion
{
    Activo = 1,
    Usado = 2,
    Expirado = 3,
    Revocado = 4
}

/// <summary>Tipo de vehiculo autorizado. Spec 2.12 seccion 8.</summary>
public enum TipoVehiculo
{
    Automovil = 1,
    Moto = 2,
    Bicicleta = 3,
    Camioneta = 4,
    Otro = 5
}

/// <summary>Origen del registro vehicular. Webhook LPR diferido a Fase 2.</summary>
public enum OrigenRegistroVehiculo
{
    Manual = 1,
    LprWebhook = 2
}

/// <summary>Tipo de correspondencia. Spec 2.12 seccion 9 + RN-06.</summary>
public enum TipoCorrespondencia
{
    Paquete = 1,
    Carta = 2,
    Encomienda = 3,
    Documento = 4,
    Otro = 5
}

/// <summary>Estado del ciclo de correspondencia. Spec 2.12 seccion 4.3.</summary>
public enum EstadoCorrespondencia
{
    Recibido = 1,
    Notificado = 2,
    Entregado = 3,
    Devuelto = 4
}

/// <summary>Canal de notificacion configurable. Spec 2.12 seccion 9 - T.2 diferido.</summary>
public enum CanalNotificacionPaquete
{
    Whatsapp = 1,
    Email = 2,
    Push = 3,
    Ninguno = 4
}

/// <summary>Semaforo de paquete pendiente. Calculado en backend. Spec 2.12 RN-13.</summary>
public enum SemaforoPaquete
{
    Verde = 1,
    Amarillo = 2,
    Rojo = 3
}
