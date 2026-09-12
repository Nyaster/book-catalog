using Npgsql;
using Testcontainers.PostgreSql;

namespace BookCatalog.IntegrationTests.Infrastructure;

public sealed class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private bool _started;

    public async Task InitializeAsync()
    {
        try
        {
            _container = new PostgreSqlBuilder(
                    "postgres:17@sha256:67f41722b7a8cbdb868a44a4995c846eddfdc2973bccb291ce937dce88ad5675")
                .WithDatabase("postgres")
                .WithUsername("bookcatalog_test")
                .WithPassword(Guid.NewGuid().ToString("N"))
                .WithLabel("bookcatalog.integration-tests", "true")
                .Build();

            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            await _container.StartAsync(timeout.Token);
            _started = true;
        }
        catch (Exception exception)
        {
            await DisposeAsync();
            throw new InvalidOperationException(
                "Could not start test PostgreSQL. Start Docker with Linux containers, check Docker access " +
                "(docker info), and allow image downloads. Integration tests require a real database.", exception);
        }
    }

    public string ConnectionStringFor(string database) => new NpgsqlConnectionStringBuilder(
        (_container ?? throw new InvalidOperationException("PostgreSQL has not started.")).GetConnectionString())
    {
        Database = database,
        Timeout = 10,
        CommandTimeout = 15
    }.ConnectionString;

    public Task CreateDatabaseAsync(string database) => ExecuteAsync($"CREATE DATABASE {QuoteDatabase(database)}");

    public async Task PauseAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await _container!.PauseAsync(timeout.Token);
    }

    public async Task ResumeAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await _container!.UnpauseAsync(timeout.Token);
    }

    public Task DropDatabaseAsync(string database) =>
        ExecuteAsync($"DROP DATABASE IF EXISTS {QuoteDatabase(database)} WITH (FORCE)");

    private async Task ExecuteAsync(string sql)
    {
        var connectionString = new NpgsqlConnectionStringBuilder(ConnectionStringFor("postgres"))
        {
            Pooling = false
        }.ConnectionString;
        await using var connection = new NpgsqlConnection(connectionString);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await connection.OpenAsync(timeout.Token);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(timeout.Token);
    }

    private static string QuoteDatabase(string database)
    {
        // Only generated test databases may be created or dropped by this fixture.
        if (!database.StartsWith("test_", StringComparison.Ordinal) ||
            !Guid.TryParseExact(database[5..], "N", out _))
            throw new ArgumentException("Expected a generated test database name.", nameof(database));

        return $"\"{database}\"";
    }

    public async Task DisposeAsync()
    {
        if (_container is null) return;

        try
        {
            if (_started) await AssertNoTestDatabasesAsync();
        }
        finally
        {
            await _container.DisposeAsync();
            _container = null;
            _started = false;
        }
    }

    private async Task AssertNoTestDatabasesAsync()
    {
        var connectionString = new NpgsqlConnectionStringBuilder(ConnectionStringFor("postgres"))
        {
            Pooling = false
        }.ConnectionString;
        await using var connection = new NpgsqlConnection(connectionString);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await connection.OpenAsync(timeout.Token);
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM pg_database WHERE datname LIKE 'test_%'", connection);
        var remaining = (long)(await command.ExecuteScalarAsync(timeout.Token))!;
        Assert.True(remaining == 0, $"Per-test cleanup left {remaining} databases behind.");
    }
}