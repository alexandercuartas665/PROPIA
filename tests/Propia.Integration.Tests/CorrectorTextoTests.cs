using Microsoft.Extensions.Logging.Abstractions;
using Propia.Application.InfraestructuraIa;
using Propia.Infrastructure.InfraestructuraIa;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// "Mejorar redaccion" de las cajas de texto (agente del sistema Corrector de redaccion, clave corrector-redaccion):
/// limites del texto, prompt con el campo y el texto, limpieza de lo que agregan algunos modelos y errores de la IA.
/// Sin base de datos: el agente y la inferencia son falsos.
/// </summary>
public class CorrectorTextoTests
{
    private sealed class IaFalsa : IAiInferenceService
    {
        public AiChatResult Respuesta { get; set; } = new(true, "Texto corregido.", null);
        public List<(Guid Agente, string Prompt)> Llamadas { get; } = new();
        public Task<AiChatResult> TestChatAsync(Guid agentId, IReadOnlyList<AiChatTurn> turns, string? systemPromptOverride = null, string? bearerToken = null,
            string? contactPhone = null, Guid? conversationId = null, CancellationToken ct = default)
        { Llamadas.Add((agentId, turns[0].Text)); return Task.FromResult(Respuesta); }
        public Task<string?> DescribeInboundMediaAsync(Guid agentId, string mimeType, string base64, string instruction, CancellationToken ct = default)
            => Task.FromResult<string?>(null);
    }

    private sealed class AgentesFalsos : IAgentesSistema
    {
        public Guid Id { get; } = Guid.NewGuid();
        public bool SinCopropiedad { get; set; }
        public List<string> Claves { get; } = new();
        public Task<AgenteSistema?> AsegurarAsync(string clave, CancellationToken ct = default)
        { Claves.Add(clave); return Task.FromResult(SinCopropiedad ? null : new AgenteSistema(Id, true)); }
    }

    private static (CorrectorTextoService Svc, IaFalsa Ia, AgentesFalsos Agentes) Build()
    {
        var ia = new IaFalsa(); var ag = new AgentesFalsos();
        return (new CorrectorTextoService(ag, ia, NullLogger<CorrectorTextoService>.Instance), ia, ag);
    }

    [Fact]
    public async Task Mejora_con_el_agente_corrector_y_el_contexto_del_campo()
    {
        var (svc, ia, ag) = Build();
        ia.Respuesta = new AiChatResult(true, "La impresora del segundo piso no imprime los recibos.", null);

        var r = await svc.MejorarAsync(new MejorarTextoRequest("  la impresora del segundo piso eh no no imprime los recibos  ", "Descripcion de una PQRSD"));

        Assert.True(r.Ok);
        Assert.Equal("La impresora del segundo piso no imprime los recibos.", r.Texto);
        Assert.Equal(PlantillaAgente.ClaveCorrectorTexto, Assert.Single(ag.Claves));
        var (agente, prompt) = Assert.Single(ia.Llamadas);
        Assert.Equal(ag.Id, agente);
        Assert.Contains("Campo del formulario: Descripcion de una PQRSD", prompt);
        Assert.Contains("<<<\nla impresora del segundo piso eh no no imprime los recibos\n>>>", prompt);
    }

    [Fact]
    public async Task Rechaza_texto_vacio_o_muy_largo_sin_llamar_a_la_IA()
    {
        var (svc, ia, _) = Build();
        Assert.False((await svc.MejorarAsync(new MejorarTextoRequest(" a "))).Ok);
        var largo = await svc.MejorarAsync(new MejorarTextoRequest(new string('x', CorrectorTextoAgente.MaxCaracteres + 1)));
        Assert.False(largo.Ok);
        Assert.Contains("muy largo", largo.Error);
        Assert.Empty(ia.Llamadas);
    }

    [Fact]
    public async Task Errores_de_la_IA_y_sin_copropiedad()
    {
        var (svc, ia, ag) = Build();
        ia.Respuesta = new AiChatResult(false, null, "Cuota de IA agotada.");
        var r = await svc.MejorarAsync(new MejorarTextoRequest("Texto suficientemente largo para corregir"));
        Assert.Equal((false, "Cuota de IA agotada."), (r.Ok, r.Error));

        ia.Respuesta = new AiChatResult(true, "   ", null);
        Assert.False((await svc.MejorarAsync(new MejorarTextoRequest("Texto suficientemente largo para corregir"))).Ok);

        ag.SinCopropiedad = true;
        Assert.Equal("No hay copropiedad activa.", (await svc.MejorarAsync(new MejorarTextoRequest("Texto suficientemente largo"))).Error);
    }

    [Theory]
    [InlineData("\"Texto limpio.\"", "Texto limpio.")]
    [InlineData("```\nTexto limpio.\n```", "Texto limpio.")]
    [InlineData("```text\nLinea 1.\nLinea 2.\n```", "Linea 1.\nLinea 2.")]
    [InlineData("<<<\nTexto limpio.\n>>>", "Texto limpio.")]
    [InlineData("  Texto limpio.  ", "Texto limpio.")]
    public void Limpia_lo_que_agregan_los_modelos(string respuesta, string esperado)
        => Assert.Equal(esperado, CorrectorTextoService.Limpiar(respuesta));
}
