using TransferGraph.Configurations;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
await builder.Services.AddNeo4jAsync(builder.Configuration);

var app = builder.Build();

app.UseDefaultFiles();   // otvara wwwroot/index.html na "/"
app.UseStaticFiles();    // servira html, css, js iz wwwroot
app.MapControllers();
app.Run();
