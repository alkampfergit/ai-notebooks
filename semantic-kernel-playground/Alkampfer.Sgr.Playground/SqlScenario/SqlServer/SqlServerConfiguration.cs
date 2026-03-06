namespace Alkampfer.Sgr.Playground.SqlScenario.SqlServer;

public class SqlServerConfiguration
{
    public string ConnectionString { get; set; } = "Server=localhost\\SQLEXPRESS;Database=master;Trusted_Connection=True;TrustServerCertificate=True;";
}
