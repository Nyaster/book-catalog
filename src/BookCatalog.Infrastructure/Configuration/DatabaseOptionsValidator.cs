using Microsoft.Extensions.Options;
using Npgsql;

namespace BookCatalog.Infrastructure.Configuration;

internal sealed class DatabaseOptionsValidator : IValidateOptions<DatabaseOptions>
{
    private const string ConfigurationKey = $"{DatabaseOptions.SectionName}:ConnectionString";

    public ValidateOptionsResult Validate(string? name, DatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            return ValidateOptionsResult.Fail($"{ConfigurationKey} is required.");
        }

        NpgsqlConnectionStringBuilder connectionStringBuilder;

        try
        {
            connectionStringBuilder = new NpgsqlConnectionStringBuilder(options.ConnectionString);
        }
        catch (ArgumentException)
        {
            return ValidateOptionsResult.Fail($"{ConfigurationKey} is not a valid PostgreSQL connection string.");
        }

        if (string.IsNullOrWhiteSpace(connectionStringBuilder.Host) ||
            string.IsNullOrWhiteSpace(connectionStringBuilder.Database) ||
            string.IsNullOrWhiteSpace(connectionStringBuilder.Username))
        {
            return ValidateOptionsResult.Fail(
                $"{ConfigurationKey} must specify Host, Database, and Username.");
        }

        return ValidateOptionsResult.Success;
    }
}
