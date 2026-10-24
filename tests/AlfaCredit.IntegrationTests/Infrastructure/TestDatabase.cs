using Npgsql;

namespace AlfaCredit.IntegrationTests.Infrastructure;

/// <summary>
/// Отдельная тестовая база на внешнем PostgreSQL вместо контейнера Testcontainers: переменная окружения
/// ALFACREDIT_TEST_POSTGRES содержит строку подключения к служебной базе с правом CREATEDB
/// (например Host=localhost;Username=alfacredit;Password=alfacredit;Database=postgres). Для каждого стенда создаётся
/// своя база alfacredit_test_&lt;id&gt;, после тестов она удаляется.
/// </summary>
internal sealed class TestDatabase
{
    public const string EnvironmentVariable = "ALFACREDIT_TEST_POSTGRES";

    private readonly string _admin;
    private readonly string _name;

    private TestDatabase(string admin, string name)
    {
        _admin = admin;
        _name = name;
        ConnectionString = new NpgsqlConnectionStringBuilder(admin) { Database = name }.ConnectionString;
    }

    public static string? ExternalAdminConnectionString
    {
        get
        {
            var value = Environment.GetEnvironmentVariable(EnvironmentVariable);
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }

    public string ConnectionString { get; }

    public static async Task<TestDatabase> CreateAsync(string admin)
    {
        var name = $"alfacredit_test_{Guid.NewGuid():N}"[..32];
        await using var connection = new NpgsqlConnection(admin);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE \"{name}\"";
        await command.ExecuteNonQueryAsync();
        return new TestDatabase(admin, name);
    }

    public async Task DropAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(_admin);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS \"{_name}\" WITH (FORCE)";
        await command.ExecuteNonQueryAsync();
    }
}
