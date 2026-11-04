using Microsoft.AspNetCore.Mvc;
using Neo4j.Driver;

[ApiController]
[Route("api")]
public class TransitController : ControllerBase
{
    private readonly IDriver _driver;
    public TransitController(IDriver driver) => _driver = driver;

    // Spisak stanica (za padajuće liste)
    [HttpGet("stops")]
    public async Task<IActionResult> Stops()
    {
        await using var session = _driver.AsyncSession();
        var cursor = await session.RunAsync(
            "MATCH (s:Stop) RETURN s.id AS id, s.name AS name, coalesce(s.closed,false) AS closed ORDER BY s.name");
        var rows = await cursor.ToListAsync();
        return Ok(rows.Select(r => new
        {
            id = r["id"].As<string>(),
            name = r["name"].As<string>(),
            closed = r["closed"].As<bool>()
        }));
    }

    // Put od stanice A do B: mode = stops | fastest | transfers
    [HttpGet("route")]
    public async Task<IActionResult> Route(string from, string to, string mode = "stops")
    {
        const string head = "MATCH (a:Stop {id:$from}), (b:Stop {id:$to}) ";
        const string open = "WHERE none(n IN nodes(p) WHERE coalesce(n.closed,false) = true) ";

        string cypher = mode switch
        {
            // najmanje stanica (BFS, svaka deonica = 1)
            "stops" => head + "MATCH p = shortestPath((a)-[:NEXT*]->(b)) " + open +
                       "RETURN [n IN nodes(p) | n.name] AS tok, length(p) AS stanica, " +
                       "reduce(t=0, r IN relationships(p) | t + r.minutes) AS minuta",

            // najbrže (zbir minuta, pa najmanji)
            "fastest" => head + "MATCH p = (a)-[:NEXT*..10]->(b) " + open +
                         "RETURN [n IN nodes(p) | n.name] AS tok, length(p) AS stanica, " +
                         "reduce(t=0, r IN relationships(p) | t + r.minutes) AS minuta " +
                         "ORDER BY minuta ASC LIMIT 1",

            // najmanje presedanja (kroz čvorove linija)
            "transfers" => head + "MATCH p = shortestPath((a)-[:ON_LINE*]-(b)) " + open +
                           "RETURN [n IN nodes(p) | n.name] AS tok, length(p)/2 - 1 AS presedanja",

            _ => ""
        };
        if (cypher == "") return BadRequest(new { error = "Nepoznat mode" });

        await using var session = _driver.AsyncSession();
        var cursor = await session.RunAsync(cypher, new { from, to });
        var rows = await cursor.ToListAsync();

        if (rows.Count == 0) return NotFound(new { error = "Nema puta (stanica je možda zatvorena)." });
        return Ok(rows[0].Values);
    }
}