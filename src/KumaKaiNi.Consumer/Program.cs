﻿using System.Text.Json;
using KumaKaiNi.Core;
using KumaKaiNi.Core.Models;
using KumaKaiNi.Core.Utility;
using Serilog;
using StackExchange.Redis;

namespace KumaKaiNi.Consumer;

public static class Program
{
    private static KumaClient? _kuma;
    private static RedisStreamConsumer? _streamConsumer;
    private static CancellationTokenSource? _cts;
    
    private static async Task Main()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Console()
            .CreateLogger();
        
        _cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            _cts.Cancel();
            eventArgs.Cancel = true;
            _streamConsumer?.Stop();

            Environment.Exit(0);
        };
        
        _kuma = new KumaClient();
        _kuma.Responded += OnKumaResponse;
        
        _streamConsumer = new RedisStreamConsumer(
            Redis.KumaConsumerStreamName,
            cancellationToken: _cts.Token);

        _streamConsumer.StreamEntriesReceived += OnStreamEntriesReceived;
        await _streamConsumer.StartAsync();
        
        await Task.Delay(-1, _cts.Token);
    }

    private static async void OnKumaResponse(KumaResponse kumaResponse)
    {
        string destinationStream = Redis.GetStreamNameForSourceSystem(kumaResponse.SourceSystem);
        string serializedRequest = JsonSerializer.Serialize(kumaResponse);
                
        IDatabase? db = Redis.Database;
        if (db == null) return;

        await db.StreamAddAsync(
            destinationStream,
            [new NameValueEntry("response", serializedRequest)],
            maxLength: Redis.StreamMaxLength,
            useApproximateMaxLength: true);
    }

    private static void OnStreamEntriesReceived(StreamEntry[] streamEntries)
    {
        foreach (StreamEntry streamEntry in streamEntries)
        {
            foreach (NameValueEntry entry in streamEntry.Values)
            {
                if (entry.Value.IsNullOrEmpty) continue;
        
                KumaRequest? response = JsonSerializer.Deserialize<KumaRequest>(entry.Value!);
                if (response == null) continue;
                
                _ = _kuma?.ProcessRequest(response);
            }
        }
    }
}