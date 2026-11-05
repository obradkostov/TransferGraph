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
    // Do kojih stanica stižem za najviše X minuta
[HttpGet("reach")]
public async Task<IActionResult> Reach(string from, int minutes = 10)
{
    await using var session = _driver.AsyncSession();
    var cursor = await session.RunAsync(@"
        MATCH p = (a:Stop {id:$from})-[:NEXT*1..8]->(b:Stop)
        WHERE b <> a AND none(n IN nodes(p) WHERE coalesce(n.closed,false) = true)
        WITH b, min(reduce(t=0, r IN relationships(p) | t + r.minutes)) AS minuta
        WHERE minuta <= $minutes
        RETURN b.name AS stanica, minuta
        ORDER BY minuta, stanica", new { from, minutes });
    var rows = await cursor.ToListAsync();
    return Ok(rows.Select(r => new { stanica = r["stanica"].As<string>(), minuta = r["minuta"].As<long>() }));
}

// Čvorišta: stanice kroz koje prolazi najviše linija
[HttpGet("hubs")]
public async Task<IActionResult> Hubs()
{
    await using var session = _driver.AsyncSession();
    var cursor = await session.RunAsync(@"
        MATCH (s:Stop)-[:ON_LINE]->(l:Line)
        RETURN s.name AS stanica, count(l) AS broj, collect(l.name) AS linije
        ORDER BY broj DESC, stanica LIMIT 10");
    var rows = await cursor.ToListAsync();
    return Ok(rows.Select(r => new { stanica = r["stanica"].As<string>(), linije = r["linije"].As<List<string>>() }));
}

// Zatvori / otvori stanicu
[HttpPost("stops/{id}/closed")]
public async Task<IActionResult> SetClosed(string id, bool closed)
{
    await using var session = _driver.AsyncSession();
    var cursor = await session.RunAsync(
        "MATCH (s:Stop {id:$id}) SET s.closed = $closed RETURN s.name AS name", new { id, closed });
    var rows = await cursor.ToListAsync();
    return rows.Count == 0 ? NotFound() : Ok(new { name = rows[0]["name"].As<string>(), closed });
}
}