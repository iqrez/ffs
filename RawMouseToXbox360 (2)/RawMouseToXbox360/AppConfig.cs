
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace RawMouseToXbox360
{
    public class Config
    {
        public Dictionary<string, string> KeyBindings { get; set; } = new();
        public int MouseSensitivity { get; set; } = 100;
        public string ProfileName { get; set; } = "default";
    }

    public static class AppConfig
    {
        public static Config Current { get; private set; }

        private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

        public static void Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    Current = JsonSerializer.Deserialize<Config>(json);
                }
                else
                {
                    Current = new Config();
                    Save();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to load config: " + ex);
                Current = new Config();
            }
        }

        public static void Save()
        {
            try
            {
                string json = JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to save config: " + ex);
            }
        }
    }
}
