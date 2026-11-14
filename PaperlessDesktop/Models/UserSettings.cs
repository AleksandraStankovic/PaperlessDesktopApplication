using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PaperlessDesktop.Models
{
    public class UserSettings
    {
        public const string DefaultLanguage = "sr";
        public const string DefaultPrimaryColor = "#6D28D9";

        public string Language { get; set; } = DefaultLanguage;
        public bool IsDarkTheme { get; set; }
        public string PrimaryColor { get; set; } = DefaultPrimaryColor;
    }
}
