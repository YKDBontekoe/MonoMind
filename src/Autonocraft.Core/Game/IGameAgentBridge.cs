using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Autonocraft.World;

namespace Autonocraft.Core
{
    /// <summary>
    /// Bridge for HTTP agent actions — decouples AgentHttpServer from AutonocraftGame type.
    /// </summary>
    public interface IGameAgentBridge
    {
        GameHostContext Host { get; }
        GameState CurrentGameState { get; }
        ConcurrentQueue<Action> PendingActions { get; }
        HashSet<Key> SimulatedKeys { get; }
        void ReleaseSimulatedKeys();
        void EnqueueAction(Action action, bool runImmediatelyInTests);
        void OpenCrucibleAt(int x, int y, int z, BlockType stationType);
        string ExecuteDevCommand(string input);
        void SyncTimeFromHost();
        void SyncCameraFromPlayer();
        Task<byte[]> RequestScreenshotAsync(string? savePath = null);
        void SimulateClick(MouseButton button);
        void RequestExit();
        void RequestOpenVillageUi();
        void RequestCloseVillageUi();
        void SetTimeOfDay(float value);
        void SetTimeScale(float scale);
        void RequestLoadStructureGallery();
        bool IsStructureGalleryWorld { get; }
        WorldType CurrentWorldType { get; }
    }
}
