namespace Autonocraft.Domain.Core
{
    public static class GamePaths
    {
        private static string? _overrideSettingsDirectory;

        public static void SetSettingsDirectoryForTests(string? directory) =>
            _overrideSettingsDirectory = directory;

        public static string GetSettingsDirectory()
        {
            if (_overrideSettingsDirectory != null)
            {
                return _overrideSettingsDirectory;
            }

            string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(root, "Autonocraft");
        }
    }
}
