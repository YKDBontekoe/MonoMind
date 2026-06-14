using Autonocraft.Core;
using Autonocraft.World;

namespace Autonocraft.Tests;

public sealed class TestHost : IDisposable
{
    public string SavesDirectory { get; }
    public string SettingsDirectory { get; }

    public TestHost()
    {
        SavesDirectory = Path.Combine(Path.GetTempPath(), "autonocraft-test-saves-" + Guid.NewGuid().ToString("N"));
        SettingsDirectory = Path.Combine(Path.GetTempPath(), "autonocraft-test-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(SavesDirectory);
        Directory.CreateDirectory(SettingsDirectory);
        WorldSaveManager.SetSavesDirectoryForTests(SavesDirectory);
        GameSettingsManager.SetSettingsDirectoryForTests(SettingsDirectory);
    }

    public void Dispose()
    {
        WorldSaveManager.SetSavesDirectoryForTests(null);
        GameSettingsManager.SetSettingsDirectoryForTests(null);

        if (Directory.Exists(SavesDirectory))
        {
            Directory.Delete(SavesDirectory, recursive: true);
        }

        if (Directory.Exists(SettingsDirectory))
        {
            Directory.Delete(SettingsDirectory, recursive: true);
        }
    }
}
