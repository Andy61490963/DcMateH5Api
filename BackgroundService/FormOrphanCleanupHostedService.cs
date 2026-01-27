using DcMateH5Api.Areas.Form.Interfaces;

namespace DcMateH5Api.Areas.Form.Services;

public sealed class FormOrphanCleanupHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FormOrphanCleanupHostedService> _logger;

    private static readonly SemaphoreSlim _runLock = new(1, 1);

    public FormOrphanCleanupHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<FormOrphanCleanupHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var taipeiTz = GetTaipeiTimeZone();

        while (!stoppingToken.IsCancellationRequested)
        {
            var nowTpe = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, taipeiTz);
            var nextMidnightTpe = new DateTimeOffset(
                nowTpe.Year, nowTpe.Month, nowTpe.Day, 0, 0, 0, nowTpe.Offset).AddDays(1);

            var delay = nextMidnightTpe - nowTpe;
            if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;

            _logger.LogInformation("OrphanCleanup scheduled. Now(TPE)={Now}, Next(TPE)={Next}, Delay={Delay}",
                nowTpe, nextMidnightTpe, delay);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (!await _runLock.WaitAsync(TimeSpan.Zero, stoppingToken))
            {
                _logger.LogWarning("OrphanCleanup skipped: previous run still in progress.");
                continue;
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var cleanup = scope.ServiceProvider.GetRequiredService<IFormOrphanCleanupService>();

                // 背景作業不要用 GetCurrentUserId()（沒有 HttpContext）
                // 建議：用固定系統帳號 Guid（可改成從設定檔讀）
                var systemUserId = Guid.Empty;

                var cleaned = await cleanup.SoftDeleteOrphansAsync(systemUserId, stoppingToken);
                _logger.LogInformation("OrphanCleanup done. Cleaned={Cleaned}", cleaned);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "OrphanCleanup failed.");
            }
            finally
            {
                _runLock.Release();
            }
        }
    }

    private static TimeZoneInfo GetTaipeiTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Taipei"); }
        catch { return TimeZoneInfo.FindSystemTimeZoneById("Taipei Standard Time"); }
    }
}
