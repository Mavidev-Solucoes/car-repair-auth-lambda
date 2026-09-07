namespace CarRepair.Auth.Infrastructure.Configuration;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public string Schema { get; set; } = "public";

    public string CustomersTableName { get; set; } = "customers";
}
