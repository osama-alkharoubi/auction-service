using Application.Common.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.BackgroundServices;

public class OutboxBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxBackgroundService> _logger;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(5);
    private const int MaxAttempts = 3;

    public OutboxBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<OutboxBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxBackgroundService is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while processing Outbox messages.");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }

        _logger.LogInformation("OutboxBackgroundService is stopping.");
    }

    private async Task ProcessOutboxMessagesAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var outboxSink = scope.ServiceProvider.GetRequiredService<IOutboxSink>();

        using var transaction = await context.Database.BeginTransactionAsync(ct);

        var messages = await context.OutboxMessages
            .FromSqlRaw(@"
                SELECT * FROM outbox_messages 
                WHERE processed_utc IS NULL AND attempts < {0} 
                ORDER BY occurred_utc 
                LIMIT 20 
                FOR UPDATE SKIP LOCKED", MaxAttempts)
            .ToListAsync(ct);

        if (!messages.Any())
            return;

        foreach (var message in messages)
        {
            try
            {
                message.Attempts++;

                await outboxSink.PublishAsync(message, ct);

                message.ProcessedUtc = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process Outbox Message with Id: {Id}", message.Id);
            }

            await context.SaveChangesAsync(ct);
        }

        await transaction.CommitAsync(ct);
    }
}