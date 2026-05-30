using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SupportPilot.Application.Notifications;

namespace SupportPilot.Infrastructure.Services;

public sealed class RabbitMqNotificationWorker(
    IConfiguration configuration,
    IOptions<NotificationOptions> options,
    IServiceScopeFactory scopeFactory,
    ILogger<RabbitMqNotificationWorker> logger) : BackgroundService
{
    private IConnection? _connection;
    private IModel? _channel;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connectionString = configuration.GetConnectionString("RabbitMQ");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            logger.LogWarning("RabbitMQ connection string is not configured. Notification worker is idle.");
            return Task.CompletedTask;
        }

        var queueName = options.Value.QueueName;
        var factory = new ConnectionFactory
        {
            Uri = new Uri(connectionString),
            DispatchConsumersAsync = true
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
        _channel.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false);
        _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += async (_, args) =>
        {
            try
            {
                var notification = JsonSerializer.Deserialize<NotificationMessage>(args.Body.Span);
                if (notification is null)
                {
                    throw new JsonException("Notification message body is empty.");
                }

                using var scope = scopeFactory.CreateScope();
                var inbox = scope.ServiceProvider.GetRequiredService<NotificationInbox>();
                await inbox.StoreAsync(notification, stoppingToken);
                _channel.BasicAck(args.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process notification message.");
                _channel?.BasicNack(args.DeliveryTag, multiple: false, requeue: true);
            }
        };

        _channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);
        logger.LogInformation("RabbitMQ notification worker is consuming queue {QueueName}.", queueName);

        return Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }

    public override void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        base.Dispose();
    }
}
