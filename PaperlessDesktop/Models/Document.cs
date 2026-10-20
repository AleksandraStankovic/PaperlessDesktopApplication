using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;


namespace PaperlessDesktop.Models
{
    public class Document
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string? Content { get; set; }
        public DateTime? Created { get; set; }
        public DateTime? Added { get; set; }
        public DateTime? Modified { get; set; }

        
        [JsonPropertyName("correspondent")]
        public int? CorrespondentId { get; set; }

        [JsonPropertyName("document_type")]
        public int? DocumentTypeId { get; set; }

        [JsonPropertyName("tags")]
        public List<int> TagIds { get; set; } = new();

        public string? OriginalFileName { get; set; }
        public int? ArchiveSerialNumber { get; set; }
        public int? PageCount { get; set; }

    }
}
