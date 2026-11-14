using System.Text.Json.Serialization;

namespace PaperlessDesktop.Models;

public class PaperlessTask
{
    public string TaskId { get; set; } = "";

    public string? Status { get; set; }

    public string? Result { get; set; }

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int? RelatedDocument { get; set; }

    public string? TaskFileName { get; set; }

    public bool IsFinished => Status is "SUCCESS" or "FAILURE" or "REVOKED";
    public bool IsSuccess => Status == "SUCCESS";
}