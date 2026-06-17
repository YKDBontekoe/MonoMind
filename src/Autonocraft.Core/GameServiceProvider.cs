using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Autonocraft.Core
{
    /// <summary>Composition root for services wired at startup.</summary>
    public static class GameServiceProvider
    {
        public static ServiceProvider Build(bool enableConsoleLogging = false)
        {
            var services = new ServiceCollection();

            if (enableConsoleLogging)
            {
                services.AddLogging(builder => builder.AddConsole());
            }
            else
            {
                services.AddLogging();
            }

            return services.BuildServiceProvider();
        }
    }
}
