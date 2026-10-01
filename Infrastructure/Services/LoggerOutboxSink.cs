using Application.Common.Interfaces;
using Domain.Entities;
using Microsoft.Extensions.Logging;

public class LoggerOutboxSink : IOutboxSink
{
    private readonly ILogger<LoggerOutboxSink> _logger;

    public LoggerOutboxSink(ILogger<LoggerOutboxSink> logger)
    {
        _logger = logger;
    }

    public Task PublishAsync(OutboxMessage message, CancellationToken ct)
    {
        _logger.LogInformation(
            "Publishing Outbox Event | Id: {Id} | Type: {Type} | Payload: {Payload}",
            message.Id, message.Type, message.PayloadJson);

        return Task.CompletedTask;
    }
}