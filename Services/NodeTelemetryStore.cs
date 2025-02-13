﻿using System.Collections.Concurrent;
using ESPresense.Models;
using MQTTnet;
using MQTTnet.Extensions.ManagedClient;
using Newtonsoft.Json;

namespace ESPresense.Services;

public class NodeTelemetryStore : BackgroundService
{
    private readonly MqttConnectionFactory _mqttConnectionFactory;

    private readonly ConcurrentDictionary<string, NodeTelemetry> _storeById = new();
    private readonly ConcurrentDictionary<string, bool> _onlineById = new();

    private IManagedMqttClient? _mc;

    public NodeTelemetryStore(MqttConnectionFactory mqttConnectionFactory)
    {
        _mqttConnectionFactory = mqttConnectionFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var mc = _mc = await _mqttConnectionFactory.GetClient(false);
        await mc.SubscribeAsync("espresense/rooms/+/telemetry");
        await mc.SubscribeAsync("espresense/rooms/+/status");

        mc.ApplicationMessageReceivedAsync += arg =>
        {
            var parts = arg.ApplicationMessage.Topic.Split('/');
            switch (parts)
            {
                case [_, _, _, "telemetry"]:
                {
                    var ds = JsonConvert.DeserializeObject<NodeTelemetry>(arg.ApplicationMessage.ConvertPayloadToString() ?? "");
                    if (ds == null) return Task.CompletedTask;
                    _storeById.AddOrUpdate(parts[2], _ => ds, (_, _) => ds);
                    return Task.CompletedTask;
                }
                case [_, _, _, "status"]:
                {
                    var online = arg.ApplicationMessage.ConvertPayloadToString() == "online";
                    _onlineById.AddOrUpdate(parts[2], _ => online, (_, _) => online);
                    break;
                }
            }
            return Task.CompletedTask;
        };

        await Task.Delay(-1, stoppingToken);
    }

    public NodeTelemetry? Get(string id)
    {
        _storeById.TryGetValue(id, out var ds);
        return ds;
    }

    public bool Online(string id)
    {
        return _onlineById.TryGetValue(id, out var online) && online;
    }
}