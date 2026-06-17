namespace Propia.Web.Services;

/// <summary>
/// Catalogo geografico minimo para los selectores de Identidad de la copropiedad.
/// Cubre Colombia (32 departamentos + Bogota DC) con sus principales ciudades.
/// Para otros paises se muestra solo el nombre del pais sin departamentos (texto libre).
/// </summary>
public static class ColombiaCatalogo
{
    public static readonly string[] Paises =
    {
        "Colombia", "Argentina", "Chile", "Ecuador", "Mexico", "Panama", "Peru", "Espana", "Otros"
    };

    /// <summary>Departamento -> ciudades principales (ordenadas alfabeticamente).</summary>
    public static readonly Dictionary<string, string[]> ColombiaDepartamentosCiudades = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Amazonas"] = new[] { "Leticia", "Puerto Narino" },
        ["Antioquia"] = new[] { "Apartado", "Bello", "Caldas", "Copacabana", "Envigado", "Itagui", "La Estrella", "Marinilla", "Medellin", "Rionegro", "Sabaneta", "Turbo" },
        ["Arauca"] = new[] { "Arauca", "Saravena", "Tame" },
        ["Atlantico"] = new[] { "Barranquilla", "Malambo", "Puerto Colombia", "Sabanagrande", "Soledad" },
        ["Bogota DC"] = new[] { "Bogota" },
        ["Bolivar"] = new[] { "Arjona", "Cartagena", "Magangue", "Mompos", "Turbaco" },
        ["Boyaca"] = new[] { "Chiquinquira", "Duitama", "Paipa", "Sogamoso", "Tunja", "Villa de Leyva" },
        ["Caldas"] = new[] { "Chinchina", "La Dorada", "Manizales", "Riosucio", "Villamaria" },
        ["Caqueta"] = new[] { "Florencia", "San Vicente del Caguan" },
        ["Casanare"] = new[] { "Aguazul", "Tauramena", "Villanueva", "Yopal" },
        ["Cauca"] = new[] { "Patia", "Popayan", "Puerto Tejada", "Santander de Quilichao" },
        ["Cesar"] = new[] { "Aguachica", "Bosconia", "Codazzi", "Valledupar" },
        ["Choco"] = new[] { "Istmina", "Quibdo" },
        ["Cordoba"] = new[] { "Cerete", "Lorica", "Monteria", "Sahagun" },
        ["Cundinamarca"] = new[] { "Cajica", "Chia", "Cota", "Facatativa", "Funza", "Fusagasuga", "Girardot", "La Calera", "Madrid", "Mosquera", "Sopo", "Soacha", "Tabio", "Tenjo", "Tocancipa", "Ubate", "Zipaquira" },
        ["Guainia"] = new[] { "Inirida" },
        ["Guaviare"] = new[] { "San Jose del Guaviare" },
        ["Huila"] = new[] { "Garzon", "La Plata", "Neiva", "Pitalito" },
        ["La Guajira"] = new[] { "Maicao", "Manaure", "Riohacha", "Uribia" },
        ["Magdalena"] = new[] { "Cienaga", "El Banco", "Santa Marta" },
        ["Meta"] = new[] { "Acacias", "Granada", "Puerto Lopez", "Villavicencio" },
        ["Narino"] = new[] { "Ipiales", "Pasto", "Tumaco", "Tuquerres" },
        ["Norte de Santander"] = new[] { "Cucuta", "Ocana", "Pamplona", "Villa del Rosario" },
        ["Putumayo"] = new[] { "Mocoa", "Orito", "Puerto Asis", "Sibundoy" },
        ["Quindio"] = new[] { "Armenia", "Calarca", "Circasia", "La Tebaida", "Montenegro", "Quimbaya" },
        ["Risaralda"] = new[] { "Dosquebradas", "La Virginia", "Pereira", "Santa Rosa de Cabal" },
        ["San Andres y Providencia"] = new[] { "Providencia", "San Andres" },
        ["Santander"] = new[] { "Barrancabermeja", "Bucaramanga", "Floridablanca", "Giron", "Piedecuesta", "San Gil", "Socorro" },
        ["Sucre"] = new[] { "Corozal", "Sampues", "Sincelejo" },
        ["Tolima"] = new[] { "Espinal", "Honda", "Ibague", "Melgar" },
        ["Valle del Cauca"] = new[] { "Buenaventura", "Buga", "Cali", "Cartago", "Jamundi", "Palmira", "Tulua", "Yumbo", "Zarzal" },
        ["Vaupes"] = new[] { "Mitu" },
        ["Vichada"] = new[] { "Puerto Carreno" }
    };

    public static string[] DepartamentosColombia => ColombiaDepartamentosCiudades.Keys.OrderBy(k => k).ToArray();

    public static string[] CiudadesDelDepartamento(string? departamento)
    {
        if (string.IsNullOrWhiteSpace(departamento)) return Array.Empty<string>();
        return ColombiaDepartamentosCiudades.TryGetValue(departamento, out var arr) ? arr : Array.Empty<string>();
    }
}
