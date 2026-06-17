using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Autonocraft.Core
{
    public static class GameSettingsManager
    {
        private const string SettingsFileName = "settings.json";

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public static void SetSettingsDirectoryForTests(string? directory) =>
            GamePaths.SetSettingsDirectoryForTests(directory);

        public static string GetSettingsDirectory() => GamePaths.GetSettingsDirectory();

        public static string GetSettingsFilePath()
        {
            return Path.Combine(GetSettingsDirectory(), SettingsFileName);
        }

        public static GameSettings Load()
        {
            string path = GetSettingsFilePath();
            if (!File.Exists(path))
            {
                return CreateDefault();
            }

            try
            {
                string json = File.ReadAllText(path);
                var settings = JsonSerializer.Deserialize<GameSettings>(json, JsonOptions) ?? CreateDefault();
                settings.Clamp();
                return settings;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Settings] Failed to load settings from '{path}': {ex.Message}. Using defaults.");
                return CreateDefault();
            }
        }

        public static void Save(GameSettings settings)
        {
            settings.Clamp();
            string directory = GetSettingsDirectory();
            Directory.CreateDirectory(directory);
            string json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(GetSettingsFilePath(), json);
        }

        private static GameSettings CreateDefault()
        {
            return new GameSettings();
        }
    }
}
