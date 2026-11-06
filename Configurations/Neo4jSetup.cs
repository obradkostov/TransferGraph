using Neo4j.Driver;
using TransferGraph.Services;

namespace TransferGraph.Configurations;

public static class Neo4jSetup
{
    public static async Task AddNeo4jAsync(this IServiceCollection services, IConfiguration config)
    {
        // Uri, korisnik i lozinka dolaze iz user-secrets, nikad iz koda
        var driver = GraphDatabase.Driver(
            config["Neo4j:Uri"]!,
            AuthTokens.Basic(config["Neo4j:User"]!, config["Neo4j:Password"]!));

        await driver.VerifyConnectivityAsync();   // pukne odmah ako veza ne radi
        Console.WriteLine("Neo4j veza radi");

        // jedan driver za celu aplikaciju (drzi pool konekcija)
        services.AddSingleton<IDriver>(driver);
        services.AddSingleton<TransitRepository>();
    }
}
