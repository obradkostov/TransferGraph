using Neo4j.Driver;

var builder = WebApplication.CreateBuilder(args);

var driver = GraphDatabase.Driver(
    builder.Configuration["Neo4j:Uri"]!,
    AuthTokens.Basic(builder.Configuration["Neo4j:User"]!, builder.Configuration["Neo4j:Password"]!));
await driver.VerifyConnectivityAsync();
Console.WriteLine("Neo4j veza radi");

builder.Services.AddSingleton(driver);
builder.Services.AddControllers();

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();
app.Run();