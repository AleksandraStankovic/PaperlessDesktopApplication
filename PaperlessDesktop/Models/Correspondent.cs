using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PaperlessDesktop.Models
{
    public class Correspondent
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int DocumentCount { get; set; }
    }
}
