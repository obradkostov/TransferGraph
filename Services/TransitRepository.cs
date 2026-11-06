using Neo4j.Driver;
using TransferGraph.Model;

namespace TransferGraph.Services;

// Ovde su svi Cypher upiti. Kontroler ne zna nista o bazi.
public class TransitRepository(IDriver driver)
{
    // Pomocna funkcija: otvori sesiju, izvrsi upit, vrati redove
    private async Task<List<IRecord>> QueryAsync(string cypher, object? parameters = null)
    {
        await using var session = driver.AsyncSession();
        var cursor = parameters is null
            ? await session.RunAsync(cypher)
            : await session.RunAsync(cypher, parameters);
        return await cursor.ToListAsync();
    }

    // Sve stanice (za padajuce liste)
    public async Task<List<Stop>> GetStopsAsync()
    {
        var rows = await QueryAsync(@"
            MATCH (s:Stop)
            RETURN s.id AS id, s.name AS name, coalesce(s.closed, false) AS closed
            ORDER BY s.name");

        return rows.Select(r => new Stop
        {
            Id = r["id"].As<string>(),
            Name = r["name"].As<string>(),
            Closed = r["closed"].As<bool>()
        }).ToList();
    }

    // Put od A do B. mode: stops | fastest | transfers
    public async Task<RouteResult?> FindRouteAsync(string from, string to, string mode)
    {
        const string head = "MATCH (a:Stop {id:$from}), (b:Stop {id:$to}) ";
        const string open = "WHERE none(n IN nodes(p) WHERE coalesce(n.closed, false) = true) ";

        string? cypher = mode switch
        {
            // najmanje stanica
            "stops" => head + "MATCH p = shortestPath((a)-[:NEXT*]->(b)) " + open +
                       "RETURN [n IN nodes(p) | n.name] AS tok, length(p) AS stanica, " +
                       "reduce(t = 0, r IN relationships(p) | t + r.minutes) AS minuta",

            // najbrze (najmanji zbir minuta)
            "fastest" => head + "MATCH p = (a)-[:NEXT*..10]->(b) " + open +
                         "RETURN [n IN nodes(p) | n.name] AS tok, length(p) AS stanica, " +
                         "reduce(t = 0, r IN relationships(p) | t + r.minutes) AS minuta " +
                         "ORDER BY minuta ASC LIMIT 1",

            // najmanje presedanja (put ide kroz cvorove linija)
            "transfers" => head + "MATCH p = shortestPath((a)-[:ON_LINE*]-(b)) " + open +
                           "RETURN [n IN nodes(p) | n.name] AS tok, length(p) / 2 - 1 AS presedanja",

            _ => null
        };
        if (cypher is null) return null;

        var rows = await QueryAsync(cypher, new { from, to });
        if (rows.Count == 0) return null;

        var r = rows[0];
        return new RouteResult
        {
            Tok = r["tok"].As<List<string>>(),
            Stanica = r.Keys.Contains("stanica") ? r["stanica"].As<int>() : null,
            Minuta = r.Keys.Contains("minuta") ? r["minuta"].As<int>() : null,
            Presedanja = r.Keys.Contains("presedanja") ? r["presedanja"].As<int>() : null
        };
    }

    // Do kojih stanica stizem za najvise X minuta
    public async Task<List<ReachItem>> GetReachAsync(string from, int minutes)
    {
        var rows = await QueryAsync(@"
            MATCH p = (a:Stop {id:$from})-[:NEXT*1..8]->(b:Stop)
            WHERE b <> a AND none(n IN nodes(p) WHERE coalesce(n.closed, false) = true)
            WITH b, min(reduce(t = 0, r IN relationships(p) | t + r.minutes)) AS minuta
            WHERE minuta <= $minutes
            RETURN b.name AS stanica, minuta
            ORDER BY minuta, stanica", new { from, minutes });

        return rows.Select(r => new ReachItem
        {
            Stanica = r["stanica"].As<string>(),
            Minuta = r["minuta"].As<int>()
        }).ToList();
    }

    // Cvorista: stanice kroz koje prolaze bar dve linije
    public async Task<List<HubItem>> GetHubsAsync()
    {
        var rows = await QueryAsync(@"
            MATCH (s:Stop)-[:ON_LINE]->(l:Line)
            WITH s, count(l) AS broj, collect(l.name) AS linije
            WHERE broj > 1
            RETURN s.name AS stanica, linije
            ORDER BY broj DESC, stanica");

        return rows.Select(r => new HubItem
        {
            Stanica = r["stanica"].As<string>(),
            Linije = r["linije"].As<List<string>>()
        }).ToList();
    }

    // Zatvori / otvori stanicu (menja jedno polje); false ako stanica ne postoji
    public async Task<bool> SetClosedAsync(string id, bool closed)
    {
        var rows = await QueryAsync(
            "MATCH (s:Stop {id:$id}) SET s.closed = $closed RETURN s.id AS id",
            new { id, closed });
        return rows.Count > 0;
    }

    // Nova stanica vezana za postojecu na datoj liniji; vraca id nove stanice ili null
    public async Task<string?> AddStopAsync(NewStop stop)
    {
        var rows = await QueryAsync(@"
            MATCH (s:Stop)
            WITH max(toInteger(substring(s.id, 1))) + 1 AS n
            MATCH (a:Stop {id:$connectTo}), (l:Line {name:$line})
            CREATE (b:Stop {id: 'S' + toString(n), name: $name})
            CREATE (a)-[:NEXT {line:$line, minutes:$minutes}]->(b)
            CREATE (b)-[:NEXT {line:$line, minutes:$minutes}]->(a)
            MERGE (b)-[:ON_LINE]->(l)
            RETURN b.id AS id",
            new { name = stop.Name.Trim(), connectTo = stop.ConnectTo, line = stop.Line, minutes = stop.Minutes });

        return rows.Count == 0 ? null : rows[0]["id"].As<string>();
    }
}
