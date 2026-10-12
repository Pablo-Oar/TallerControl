using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace TallerControl.Api.IntegrationTests.Persistence;

/// <summary>Convenciones de modelo fijadas en doc 04 §1 y doc 02 §9.</summary>
[Collection(PostgresCollection.Name)]
public sealed class ConventionTests
{
    private static readonly Guid TenantA = Guid.CreateVersion7();

    private readonly PostgresFixture _postgres;

    public ConventionTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    private async Task<(string ConnectionString, TestDbContext Context)> NewContextAsync()
    {
        var connectionString = await _postgres.CreateDatabaseAsync();
        var context = TestDbContext.Create(connectionString, TenantA);
        await context.Database.EnsureCreatedAsync();
        return (connectionString, context);
    }

    private static async Task<List<string>> QueryStringsAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        var values = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            values.Add(reader.GetString(0));
        }

        return values;
    }

    [Fact]
    public async Task Tablas_y_columnas_usan_snake_case_y_los_tipos_del_plan()
    {
        var (connectionString, context) = await NewContextAsync();
        await using var _ = context;

        var columns = await QueryStringsAsync(
            connectionString,
            """
            SELECT column_name || ':' || data_type || COALESCE(':' || numeric_precision || ',' || numeric_scale, '')
            FROM information_schema.columns WHERE table_name = 'test_items' ORDER BY column_name
            """);

        Assert.Contains("id:uuid", columns);
        Assert.Contains("tenant_id:uuid", columns);
        Assert.Contains("name:text", columns);
        Assert.Contains("status:text", columns);
        Assert.Contains("price:numeric:14,2", columns);
        Assert.Contains("occurred_at:timestamp with time zone", columns);
        Assert.DoesNotContain(columns, c => c.StartsWith("version", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Los_enums_se_guardan_como_texto_snake_case()
    {
        var (connectionString, context) = await NewContextAsync();
        await using var _ = context;
        context.Items.Add(new TestItem("a", TestStatus.InProgress, 10m, DateTimeOffset.UtcNow));
        await context.SaveChangesAsync();

        var stored = await QueryStringsAsync(connectionString, "SELECT status FROM test_items");

        Assert.Equal(["in_progress"], stored);
        Assert.Equal(TestStatus.InProgress, (await context.Items.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public async Task Un_valor_de_enum_invalido_viola_el_CHECK()
    {
        var (connectionString, context) = await NewContextAsync();
        await using var _ = context;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "INSERT INTO test_items (id, tenant_id, name, status, price, occurred_at) " +
            "VALUES (gen_random_uuid(), gen_random_uuid(), 'x', 'inexistente', 1, now())",
            connection);

        var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());

        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
    }

    [Fact]
    public async Task El_dinero_se_redondea_a_dos_decimales_en_la_base()
    {
        var (_, context) = await NewContextAsync();
        await using var _ = context;
        context.Items.Add(new TestItem("a", TestStatus.Active, 10.126m, DateTimeOffset.UtcNow));
        await context.SaveChangesAsync();

        var price = (await context.Items.AsNoTracking().SingleAsync()).Price;

        Assert.Equal(10.13m, price);
    }

    [Fact]
    public async Task Una_fecha_con_offset_distinto_de_UTC_se_rechaza()
    {
        var (_, context) = await NewContextAsync();
        await using var _ = context;
        var local = new DateTimeOffset(2026, 10, 12, 10, 0, 0, TimeSpan.FromHours(-3));
        context.Items.Add(new TestItem("a", TestStatus.Active, 1m, local));

        await Assert.ThrowsAnyAsync<Exception>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task La_concurrencia_optimista_usa_xmin()
    {
        var (connectionString, first) = await NewContextAsync();
        await using var _ = first;
        var item = new TestItem("a", TestStatus.Active, 1m, DateTimeOffset.UtcNow);
        first.Items.Add(item);
        await first.SaveChangesAsync();

        await using var second = TestDbContext.Create(connectionString, TenantA);
        var other = await second.Items.SingleAsync();

        item.Name = "primero";
        await first.SaveChangesAsync();

        other.Name = "segundo";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }
}
