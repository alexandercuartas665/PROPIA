using Propia.Application.Asambleas;

namespace Propia.Infrastructure.Asambleas;

/// <summary>Voz a texto en el navegador (Web Speech API de Chrome/Edge, es-CO): el servidor solo recibe el texto final.</summary>
public sealed class NavegadorTranscripcionProvider : ITranscripcionProvider
{
    public string Clave => "navegador";
    public bool EnServidor => false;
}
