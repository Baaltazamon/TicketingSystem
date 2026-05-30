using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using SupportPilot.Application.Abstractions;
using SupportPilot.Application.Notifications;

namespace SupportPilot.Infrastructure.Services;

public sealed class RabbitMqNotificationPublisher(
    IConfiguration configuration,
    IOptions<NotificationOptions> options) : INotificationPublisher
{
    private readonly NotificationOptions _options = options.Value;

    public Task PublishAsync(NotificationMessage notification, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var connectionString = configuration.GetConnectionString("RabbitMQ");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("RabbitMQ connection string is not configured.");
        }

        var factory = new ConnectionFactory { Uri = new Uri(connectionString) };
        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        channel.QueueDeclare(
            queue: _options.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false);

        var body = JsonSerializer.SerializeToUtf8Bytes(notification);
        var properties = channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.ContentType = "application/json";
        properties.MessageId = Guid.NewGuid().ToString("N");

        channel.BasicPublish(
            exchange: string.Empty,
            routingKey: _options.QueueName,
            basicProperties: properties,
            body: body);

        return Task.CompletedTask;
    }
}
