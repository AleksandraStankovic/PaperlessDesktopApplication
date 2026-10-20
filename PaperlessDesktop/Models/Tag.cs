using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PaperlessDesktop.Models
{
    public class Tag
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Color { get; set; } = "#a6cee3";     
        public string? TextColor { get; set; }
        public bool IsInboxTag { get; set; }
        public int DocumentCount { get; set; }
    }
}
