using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

namespace AutoTest.Core;

internal sealed record MqttReceivedMessage(string Topic, string Payload);

internal sealed class MqttTestClient : IAsyncDisposable
{
    private readonly EnvironmentStore environment;
    private readonly IMqttClient client;
    private readonly SemaphoreSlim connectionLock = new(1, 1);
    private TaskCompletionSource<MqttReceivedMessage>? pendingMessage;
    private string? connectionIdentity;

    public MqttTestClient(EnvironmentStore environment)
    {
        this.environment = environment;
        client = new MqttFactory().CreateMqttClient();
        client.ApplicationMessageReceivedAsync += args =>
        {
            string payload = args.ApplicationMessage.ConvertPayloadToString();
            pendingMessage?.TrySetResult(new(args.ApplicationMessage.Topic, payload));
            return Task.CompletedTask;
        };
    }

    public async Task ConnectAsync(string? username, string? password, string? clientId, CancellationToken cancellationToken)
    {
        username ??= environment.Get("MQTT_USERNAME");
        password ??= environment.Get("MQTT_PASSWORD");
        clientId ??= environment.Get("MQTT_CLIENT_ID") ?? "tool-qc";
        string identity = $"{username}\n{clientId}";
        if (client.IsConnected && connectionIdentity == identity) return;
        await connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (client.IsConnected && connectionIdentity == identity) return;
            if (client.IsConnected) await client.DisconnectAsync(cancellationToken: cancellationToken);
            var options = new MqttClientOptionsBuilder()
                .WithClientId(clientId)
                .WithTcpServer(environment.Require("MQTT_HOST"), environment.Int("MQTT_PORT", 1883))
                .WithCredentials(username, password)
                .WithCleanSession()
                .Build();
            await client.ConnectAsync(options, cancellationToken);
            connectionIdentity = identity;
        }
        finally
        {
            connectionLock.Release();
        }
    }

    public async Task PublishAsync(string topic, string payload, int qos, bool retain, string? username, string? password, string? clientId, CancellationToken cancellationToken)
    {
        await ConnectAsync(username, password, clientId, cancellationToken);
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel(ToQos(qos))
            .WithRetainFlag(retain)
            .Build();
        await client.PublishAsync(message, cancellationToken);
    }

    public async Task<MqttReceivedMessage> SubscribeAsync(string topic, int qos, TimeSpan timeout, string? username, string? password, string? clientId, CancellationToken cancellationToken)
    {
        await ConnectAsync(username, password, clientId, cancellationToken);
        pendingMessage = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await client.SubscribeAsync(new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter(filter => filter.WithTopic(topic).WithQualityOfServiceLevel(ToQos(qos)))
            .Build(), cancellationToken);
        try
        {
            return await pendingMessage.Task.WaitAsync(timeout, cancellationToken);
        }
        finally
        {
            pendingMessage = null;
            if (client.IsConnected) await client.UnsubscribeAsync(topic, cancellationToken);
        }
    }

    public async Task<MqttReceivedMessage> RoundtripAsync(string topic, string payload, int qos, bool retain, TimeSpan timeout, string? username, string? password, string? clientId, CancellationToken cancellationToken)
    {
        await ConnectAsync(username, password, clientId, cancellationToken);
        pendingMessage = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await client.SubscribeAsync(new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter(filter => filter.WithTopic(topic).WithQualityOfServiceLevel(ToQos(qos)))
            .Build(), cancellationToken);
        try
        {
            await PublishAsync(topic, payload, qos, retain, username, password, clientId, cancellationToken);
            return await pendingMessage.Task.WaitAsync(timeout, cancellationToken);
        }
        finally
        {
            pendingMessage = null;
            if (client.IsConnected) await client.UnsubscribeAsync(topic, cancellationToken);
        }
    }

    private static MqttQualityOfServiceLevel ToQos(int qos) => qos switch
    {
        0 => MqttQualityOfServiceLevel.AtMostOnce,
        1 => MqttQualityOfServiceLevel.AtLeastOnce,
        2 => MqttQualityOfServiceLevel.ExactlyOnce,
        _ => throw new InvalidOperationException("MQTT QoS chỉ hỗ trợ các giá trị 0, 1 hoặc 2.")
    };

    public async ValueTask DisposeAsync()
    {
        if (client.IsConnected) await client.DisconnectAsync();
        client.Dispose();
        connectionLock.Dispose();
    }
}
