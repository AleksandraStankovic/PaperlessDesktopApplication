using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PaperlessDesktop.Models
{
    internal class DocumentQuery
    {

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
        public string? SearchText { get; set; }
        public List<int> TagIds { get; set; } = new();
        public int? CorrespondentId { get; set; }
        public int? DocumentTypeId { get; set; }
        public string Ordering { get; set; } = "-created";   

        public string ToQueryString()
        {
            var parts = new List<string>
        {
            $"page={Page}",
            $"page_size={PageSize}",
            $"ordering={Uri.EscapeDataString(Ordering)}",
            "truncate_content=true"          
        };

            if (!string.IsNullOrWhiteSpace(SearchText))
                parts.Add($"title_content={Uri.EscapeDataString(SearchText.Trim())}");

            if (TagIds.Count > 0)
                parts.Add($"tags__id__all={string.Join(",", TagIds)}");

            if (CorrespondentId is int correspondentId)
                parts.Add($"correspondent__id={correspondentId}");

            if (DocumentTypeId is int documentTypeId)
                parts.Add($"document_type__id={documentTypeId}");

            return "?" + string.Join("&", parts);
        }

    }
}
