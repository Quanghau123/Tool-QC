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

    public async Task<MqttReceivedMessage> LastWillAsync(
        string topic,
        string payload,
        int qos,
        bool retain,
        TimeSpan timeout,
        string? username,
        string? password,
        string? clientId,
        Action<string, string> reportStage,
        CancellationToken cancellationToken)
    {
        IMqttClient? observer = null;
        IMqttClient? device = null;
        try
        {
            var factory = new MqttFactory();
            observer = factory.CreateMqttClient();
            device = factory.CreateMqttClient();
            var received = new TaskCompletionSource<MqttReceivedMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            observer.ApplicationMessageReceivedAsync += args =>
            {
                received.TrySetResult(new(args.ApplicationMessage.Topic, args.ApplicationMessage.ConvertPayloadToString()));
                return Task.CompletedTask;
            };
            string observerClientId = environment.Get("MQTT_CLIENT_ID") ?? "tool-qc";
            var observerOptions = new MqttClientOptionsBuilder()
                .WithClientId(observerClientId)
                .WithTcpServer(environment.Require("MQTT_HOST"), environment.Int("MQTT_PORT", 1883))
                .WithCredentials(environment.Get("MQTT_USERNAME"), environment.Get("MQTT_PASSWORD"))
                .WithCleanSession()
                .Build();
            try
            {
                await observer.ConnectAsync(observerOptions, cancellationToken);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "Client quan sát MQTT không thể kết nối. Hãy kiểm tra MQTT_USERNAME, MQTT_PASSWORD và MQTT_CLIENT_ID của tài khoản có quyền subscribe.",
                    exception);
            }
            await observer.SubscribeAsync(new MqttClientSubscribeOptionsBuilder()
                .WithTopicFilter(filter => filter.WithTopic(topic).WithQualityOfServiceLevel(ToQos(qos)))
                .Build(), cancellationToken);
            reportStage(
                "Client quan sát kết nối và đăng ký nhận topic Last Will",
                $"Đã subscribe topic {topic} với QoS {qos}.");

            username ??= environment.Get("MQTT_USERNAME");
            password ??= environment.Get("MQTT_PASSWORD");
            clientId ??= environment.Get("MQTT_CLIENT_ID") ?? "tool-qc";
            var deviceOptions = new MqttClientOptionsBuilder()
                .WithClientId(clientId)
                .WithTcpServer(environment.Require("MQTT_HOST"), environment.Int("MQTT_PORT", 1883))
                .WithCredentials(username, password)
                .WithCleanSession()
                .WithWillTopic(topic)
                .WithWillPayload(payload)
                .WithWillQualityOfServiceLevel(ToQos(qos))
                .WithWillRetain(retain)
                .Build();
            try
            {
                await device.ConnectAsync(deviceOptions, cancellationToken);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "Client thiết bị MQTT không thể kết nối bằng tài khoản động được cung cấp cho action lastwill.",
                    exception);
            }
            reportStage(
                "Thiết bị CONNECT và đăng ký Last Will với broker",
                $"Broker đã chấp nhận CONNECT kèm Will; QoS {qos}, retain {retain.ToString().ToLowerInvariant()}.");
            device.Dispose();
            device = null;
            reportStage(
                "Đóng kết nối thiết bị bất thường mà không gửi DISCONNECT",
                "Transport của client thiết bị đã bị đóng trực tiếp; không gọi MQTT DisconnectAsync.");
            return await received.Task.WaitAsync(timeout, cancellationToken);
        }
        finally
        {
            if (device is not null) device.Dispose();
            if (observer is not null)
            {
                if (observer.IsConnected)
                {
                    try { await observer.DisconnectAsync(cancellationToken: CancellationToken.None); } catch { }
                }
                observer.Dispose();
            }
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
