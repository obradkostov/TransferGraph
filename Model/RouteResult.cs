using System.Text.Json.Serialization;

namespace TransferGraph.Model;

// Odgovor za put od A do B. Polja koja nisu popunjena se ne salju (JS ih tada ne prikazuje).
public class RouteResult
{
    public List<string> Tok { get; set; } = new();

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Stanica { get; set; }       // broj deonica

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Minuta { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Presedanja { get; set; }
}
