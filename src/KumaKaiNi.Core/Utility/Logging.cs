using KumaKaiNi.Core.Database;
using KumaKaiNi.Core.Database.Entities;
using KumaKaiNi.Core.Models;
using Serilog;

namespace KumaKaiNi.Core.Utility;

public static class Logging
{
    private const string KumaKaiNiUsername = "KumaKaiNi";
    
    /// <summary>
    /// Logs the incoming request to the ChatLogs database.
    /// </summary>
    /// <param name="kumaRequest">The request to log.</param>
    public static async Task LogRequestToDatabaseAsync(KumaRequest kumaRequest)
    {
        if (string.IsNullOrEmpty(kumaRequest.Message)) return;
        
        await using KumaKaiNiDbContext db = new();
        
        ChatLog requestChatLog = new(
            kumaRequest.Timestamp, 
            kumaRequest.SourceSystem, 
            kumaRequest.Message, 
            kumaRequest.MessageId,
            kumaRequest.Username, 
            kumaRequest.ChannelId, 
            kumaRequest.ChannelIsPrivate);
        
        db.ChatLogs.Add(requestChatLog);
        LogMessage(kumaRequest.SourceSystem, kumaRequest.Username, kumaRequest.Message);

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Log the outgoing response to the ChatLogs database.
    /// </summary>
    /// <param name="kumaRequest">The incoming request.</param>
    /// <param name="kumaResponse">The outgoing response.</param>
    public static async Task LogResponseToDatabaseAsync(KumaRequest kumaRequest, KumaResponse kumaResponse)
    {
        await using KumaKaiNiDbContext db = new();

        string? responseString = kumaResponse.Message;
        if (string.IsNullOrEmpty(responseString) && kumaResponse.Image != null) 
            responseString = $"{kumaResponse.Image.Referrer}\n{kumaResponse.Image.Description}\n{kumaResponse.Image.Url}\n{kumaResponse.Image.Source}";

        if (!string.IsNullOrEmpty(responseString))
        {
            ChatLog responseChatLog = new(
                kumaResponse.Timestamp, 
                kumaResponse.SourceSystem, 
                responseString,
                null,
                KumaKaiNiUsername, 
                kumaResponse.ChannelId, 
                kumaRequest.ChannelIsPrivate);
            
            db.ChatLogs.Add(responseChatLog);
            LogMessage(kumaRequest.SourceSystem, KumaKaiNiUsername, responseString);
        }

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Synchronously log an exception to the ErrorLogs database.
    /// </summary>
    /// <param name="ex">The exception to log.</param>
    /// <param name="messageTemplate">The Serilog message template.</param>
    /// <param name="propertyValues">The Serilog properties for the message template.</param>
    public static void LogExceptionToDatabase(Exception ex, string messageTemplate, params object?[]? propertyValues)
    {
        Log.Error(ex, messageTemplate, propertyValues);
        
        ErrorLog errorLog = new(ex);

        using KumaKaiNiDbContext db = new();
        db.ErrorLogs.Add(errorLog);
        db.SaveChanges();
    }

    /// <summary>
    /// Log an exception to the ErrorLogs database.
    /// </summary>
    /// <param name="ex">The exception to log.</param>
    /// <param name="messageTemplate">The Serilog message template.</param>
    /// <param name="propertyValues">The Serilog properties for the message template.</param>
    public static async Task LogExceptionToDatabaseAsync(Exception ex, string messageTemplate, params object?[]? propertyValues)
    {
        Log.Error(ex, messageTemplate, propertyValues);
        
        ErrorLog errorLog = new(ex);

        await using KumaKaiNiDbContext db = new();
        await db.ErrorLogs.AddAsync(errorLog);
        await db.SaveChangesAsync();
    }

    private static void LogMessage(SourceSystem sourceSystem, string username, string? message)
    {
        Log.Information("[{SourceSystem}] {Username}: {Message}", 
            sourceSystem, username, message);
    }
}