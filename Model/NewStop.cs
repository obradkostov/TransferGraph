namespace TransferGraph.Model;

// Telo zahteva za dodavanje nove stanice
public class NewStop
{
    public string Name { get; set; } = "";
    public string ConnectTo { get; set; } = "";   // id postojece stanice
    public string Line { get; set; } = "";        // "1".."5"
    public int Minutes { get; set; }
}
