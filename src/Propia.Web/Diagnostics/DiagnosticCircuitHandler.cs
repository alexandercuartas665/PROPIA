using Microsoft.AspNetCore.Components.Server.Circuits;

namespace Propia.Web.Diagnostics;

/// <summary>
/// Diagnostico (temporal) del circuito Blazor Server. Registra en el log del servidor cuando un
/// circuito se abre, se conecta, se desconecta y se cierra. Sirve para cazar el caso "la UI se queda
/// pensando pero el server respondio 200": si el circuito del cliente hace ConnectionDown justo despues
/// de disparar una accion, la actualizacion server->cliente nunca llega aunque el handler termine bien.
///
/// Es barato (pocas lineas por sesion) y no cambia el comportamiento. Se puede dejar puesto; si molesta,
/// subir la categoria "Propia.Web.Diagnostics" a Warning en la config de logs.
/// </summary>
public sealed class DiagnosticCircuitHandler : CircuitHandler
{
    private readonly ILogger<DiagnosticCircuitHandler> _log;

    public DiagnosticCircuitHandler(ILogger<DiagnosticCircuitHandler> log) => _log = log;

    // Orden alto: corre despues de los handlers normales (no interfiere con ellos).
    public override int Order => 1000;

    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken ct)
    {
        _log.LogInformation("[CIRCUIT] abierto {CircuitId}", circuit.Id);
        return Task.CompletedTask;
    }

    public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken ct)
    {
        _log.LogInformation("[CIRCUIT] conexion ARRIBA {CircuitId}", circuit.Id);
        return Task.CompletedTask;
    }

    public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken ct)
    {
        // Esta es la linea clave: si aparece justo tras una accion del usuario, el cliente perdio
        // el canal en vivo y por eso no se pinta la respuesta (aunque el server la haya producido).
        _log.LogWarning("[CIRCUIT] conexion ABAJO {CircuitId}", circuit.Id);
        return Task.CompletedTask;
    }

    public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken ct)
    {
        _log.LogInformation("[CIRCUIT] cerrado {CircuitId}", circuit.Id);
        return Task.CompletedTask;
    }
}
