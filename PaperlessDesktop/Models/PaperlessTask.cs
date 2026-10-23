using System.Text.Json.Serialization;

namespace PaperlessDesktop.Models;

/// <summary>A background job on the server (e.g. processing an uploaded document).</summary>
public class PaperlessTask
{
    public string TaskId { get; set; } = "";

    /// <summary>PENDING, STARTED, SUCCESS, FAILURE, REVOKED</summary>
    public string? Status { get; set; }

    /// <summary>Server's message, e.g. "Success. New document id 123 created" or an error.</summary>
    public string? Result { get; set; }

    // The server may send this as a number or as text - accept both
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int? RelatedDocument { get; set; }

    public string? TaskFileName { get; set; }

    public bool IsFinished => Status is "SUCCESS" or "FAILURE" or "REVOKED";
    public bool IsSuccess => Status == "SUCCESS";
}