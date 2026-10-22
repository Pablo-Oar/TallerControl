using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TallerControl.Application.Common;
using TallerControl.Domain.Quotes;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Infrastructure.Maintenance;

/// <summary>
/// Tareas periódicas de todos los talleres (cada hora): marcar como vencidos los presupuestos enviados cuya
/// vigencia pasó. Cada tarea corre aislada: si una falla, las demás siguen.
/// </summary>
internal sealed class MaintenanceWorker : BackgroundService
{
    private static readonly TimeSpan StartDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MaintenanceWorker> _logger;

    public MaintenanceWorker(IServiceScopeFactory scopeFactory, ILogger<MaintenanceWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartDelay, stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                await RunOnceAsync(stoppingToken);
                await Task.Delay(Interval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Apagado normal.
        }
    }

    internal async Task RunOnceAsync(CancellationToken ct)
    {
        await RunTaskAsync("vencimiento de presupuestos", ExpireQuotesAsync, ct);
    }

    private async Task RunTaskAsync(string name, Func<IServiceProvider, CancellationToken, Task> task, CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            await task(scope.ServiceProvider, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Falló la tarea periódica: {Task}", name);
        }
    }

    private async Task ExpireQuotesAsync(IServiceProvider services, CancellationToken ct)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var today = ArgentinaTime.Today(services.GetRequiredService<TimeProvider>().GetUtcNow());

        // Sin sesión: se cargan los de todos los talleres. No se cambia el taller de ninguno.
        var overdue = await db.Set<Quote>()
            .IgnoreQueryFilters()
            .Where(q => q.Status == QuoteStatus.Sent && q.ValidUntil < today && q.DeletedAt == null)
            .ToListAsync(ct);

        var expired = overdue.Count(q => q.CheckExpiry(today));
        if (expired == 0) return;

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("{Count} presupuestos marcados como vencidos", expired);
    }
}
