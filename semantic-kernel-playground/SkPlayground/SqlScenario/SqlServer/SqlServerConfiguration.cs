namespace SemanticKernel.Orchestration.SampleAgents.SqlServer;

public class SqlServerConfiguration
{
    public string ConnectionString { get; set; } = "Server=localhost\\SQLEXPRESS;Database=master;Trusted_Connection=True;TrustServerCertificate=True;";
}
