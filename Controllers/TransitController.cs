using Microsoft.AspNetCore.Mvc;
using TransferGraph.Model;
using TransferGraph.Services;

namespace TransferGraph.Controllers;

// Tanak kontroler: prima zahtev, proveri ulaz, pozove servis i vrati odgovor.
[ApiController]
[Route("api")]
public class TransitController(TransitRepository repository) : ControllerBase
{
    [HttpGet("stops")]
    public async Task<ActionResult<List<Stop>>> GetStops() => Ok(await repository.GetStopsAsync());

    [HttpGet("route")]
    public async Task<ActionResult<RouteResult>> GetRoute(
        string from,
        string to,
        string mode = "stops"
    )
    {
        if (mode is not ("stops" or "fastest" or "transfers"))
            return BadRequest(new { error = "Nepoznat mode." });

        var result = await repository.FindRouteAsync(from, to, mode);
        return result is null
            ? NotFound(new { error = "Nema puta (stanica je mozda zatvorena)." })
            : Ok(result);
    }

    [HttpGet("reach")]
    public async Task<ActionResult<List<ReachItem>>> GetReach(string from, int minutes = 10) =>
        Ok(await repository.GetReachAsync(from, Math.Clamp(minutes, 1, 30)));

    [HttpGet("hubs")]
    public async Task<ActionResult<List<HubItem>>> GetHubs() => Ok(await repository.GetHubsAsync());

    [HttpPost("stops/{id}/closed")]
    public async Task<ActionResult> SetClosed(string id, bool closed) =>
        await repository.SetClosedAsync(id, closed) ? Ok() : NotFound();

    [HttpPost("stops")]
    public async Task<ActionResult> AddStop([FromBody] NewStop stop)
    {
        var name = stop.Name?.Trim() ?? "";
        if (name.Length == 0 || name.Length > 30)
            return BadRequest(new { error = "Naziv mora imati 1 do 30 karaktera." });
        if (stop.Minutes < 1 || stop.Minutes > 30)
            return BadRequest(new { error = "Minuta mora biti od 1 do 30." });
        if (await repository.StopNameExistsAsync(name)) 
            return Conflict(new { error = "Stanica sa tim imenom već postoji." }); 

        var id = await repository.AddStopAsync(stop);
        return id is null
            ? NotFound(new { error = "Izabrana stanica nije na izabranoj liniji." })
            : Ok(new { id });
    }
}
