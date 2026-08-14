using FarmKart.Application.Abstractions.Auctions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FarmKart.Infrastructure.Services;

public sealed class AuctionFinalizationBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<AuctionFinalizationBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Auction Finalization Background Service started.");

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var finalizationService = scope.ServiceProvider.GetRequiredService<IAuctionFinalizationService>();
                var finalizedCount = await finalizationService.FinalizeExpiredAuctionsAsync(stoppingToken);

                if (finalizedCount > 0)
                {
                    logger.LogInformation("Auction Finalization Background Service finalized {Count} expired auction(s).", finalizedCount);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error occurred during background auction finalization.");
            }
        }
    }
}
