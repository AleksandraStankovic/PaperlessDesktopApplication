using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;

namespace PaperlessDesktop.Services
{
    public class AppConfig
    {
        public string ServerUrl { get; set; } = "";
        public string ApiToken { get; set; } = "";

        public static AppConfig Load()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

            if (!File.Exists(path))
                throw new InvalidOperationException($"Configuration file not found: {path}");

            var config = JsonSerializer.Deserialize<AppConfig>(
                File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (config is null || string.IsNullOrWhiteSpace(config.ServerUrl) || string.IsNullOrWhiteSpace(config.ApiToken))
                throw new InvalidOperationException("appsettings.json must contain ServerUrl and ApiToken.");

            return config;
        }
    }
}
