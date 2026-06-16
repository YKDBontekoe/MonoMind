using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Autonocraft.Domain.Village;
using Autonocraft.World;

namespace Autonocraft.Core.Agent;

internal static class AgentActionTimeouts
{
    public const int QueuedActionWaitMs = 10000;
}

internal abstract class AgentActionBase : IAgentAction
{
    public abstract string Command { get; }

    public abstract AgentActionResponseDto Execute(IGameAgentBridge bridge, HttpListenerRequest request);

    protected static AgentActionResponseDto Ok(string message) =>
        new(true, message);

    protected static AgentActionResponseDto Fail(string message) =>
        new(false, message);
}

internal sealed class KeyDownAction : AgentActionBase
{
    public override string Command => "key_down";

    public override AgentActionResponseDto Execute(IGameAgentBridge bridge, HttpListenerRequest request)
    {
        string? keyStrDown = request.QueryString["key"];
        if (AgentHttpServer.TryParseKeyInternal(keyStrDown, out var keyValDown))
        {
            bridge.EnqueueAction(() => bridge.SimulatedKeys.Add(keyValDown), runImmediatelyInTests: false);
            return Ok($"Key {keyValDown} pressed");
        }

        return Fail($"Invalid key: {keyStrDown}");
    }
}

internal sealed class KeyUpAction : AgentActionBase
{
    public override string Command => "key_up";

    public override AgentActionResponseDto Execute(IGameAgentBridge bridge, HttpListenerRequest request)
    {
        string? keyStrUp = request.QueryString["key"];
        if (AgentHttpServer.TryParseKeyInternal(keyStrUp, out var keyValUp))
        {
            bridge.EnqueueAction(() => bridge.SimulatedKeys.Remove(keyValUp), runImmediatelyInTests: false);
            return Ok($"Key {keyValUp} released");
        }

        return Fail($"Invalid key: {keyStrUp}");
    }
}

internal sealed class ReleaseKeysAction : AgentActionBase
{
    public override string Command => "release_keys";

    public override AgentActionResponseDto Execute(IGameAgentBridge bridge, HttpListenerRequest request)
    {
        bridge.ReleaseSimulatedKeys();
        return Ok("All simulated keys released");
    }
}

internal sealed class ClickAction : AgentActionBase
{
    public override string Command => "click";

    public override AgentActionResponseDto Execute(IGameAgentBridge bridge, HttpListenerRequest request)
    {
        string? btnStr = request.QueryString["button"];
        if (btnStr?.ToLower() == "left")
        {
            bridge.EnqueueAction(() => bridge.SimulateClick(MouseButton.Left), runImmediatelyInTests: false);
            return Ok("Left click simulated");
        }

        if (btnStr?.ToLower() == "right")
        {
            bridge.EnqueueAction(() => bridge.SimulateClick(MouseButton.Right), runImmediatelyInTests: false);
            return Ok("Right click simulated");
        }

        return Fail("Invalid or missing 'button' parameter (must be 'left' or 'right')");
    }
}

internal sealed class SetLookAction : AgentActionBase
{
    public override string Command => "set_look";

    public override AgentActionResponseDto Execute(IGameAgentBridge bridge, HttpListenerRequest request)
    {
        string? yawStr = request.QueryString["yaw"];
        string? pitchStr = request.QueryString["pitch"];
        if (float.TryParse(yawStr, out float yaw) && float.TryParse(pitchStr, out float pitch))
        {
            bridge.EnqueueAction(() =>
            {
                var p = bridge.Host.Session.Player;
                p.Yaw = yaw;
                p.Pitch = Math.Clamp(pitch, -89f, 89f);
                bridge.SyncCameraFromPlayer();
            }, runImmediatelyInTests: false);

            return Ok($"Look direction set to yaw={yaw}, pitch={pitch}");
        }

        return Fail("Invalid or missing 'yaw' or 'pitch' parameters");
    }
}

internal sealed class LookAction : AgentActionBase
{
    public override string Command => "look";

    public override AgentActionResponseDto Execute(IGameAgentBridge bridge, HttpListenerRequest request)
    {
        string? dxStr = request.QueryString["dx"];
        string? dyStr = request.QueryString["dy"];
        if (float.TryParse(dxStr, out float dx) && float.TryParse(dyStr, out float dy))
        {
            bridge.EnqueueAction(() =>
            {
                var p = bridge.Host.Session.Player;
                p.Yaw += dx * 0.15f;
                p.Pitch = Math.Clamp(p.Pitch - dy * 0.15f, -89f, 89f);
                bridge.SyncCameraFromPlayer();
            }, runImmediatelyInTests: false);

            return Ok($"Rotated yaw by {dx}, pitch by {-dy}");
        }

        return Fail("Invalid or missing 'dx' or 'dy' parameters");
    }
}

internal sealed class TeleportAction : AgentActionBase
{
    public override string Command => "teleport";

    public override AgentActionResponseDto Execute(IGameAgentBridge bridge, HttpListenerRequest request)
    {
        string? xStr = request.QueryString["x"];
        string? yStr = request.QueryString["y"];
        string? zStr = request.QueryString["z"];
        if (float.TryParse(xStr, out float tx) &&
            float.TryParse(yStr, out float ty) &&
            float.TryParse(zStr, out float tz))
        {
            bridge.EnqueueAction(() =>
            {
                var p = bridge.Host.Session.Player;
                p.Position = new System.Numerics.Vector3(tx, ty, tz);
                p.Velocity = System.Numerics.Vector3.Zero;
                p.ForceAirborne();
                bridge.SyncCameraFromPlayer();
            }, runImmediatelyInTests: false);

            return Ok($"Teleported player to ({tx}, {ty}, {tz})");
        }

        return Fail("Invalid or missing 'x', 'y', or 'z' parameters");
    }
}

internal sealed class SetCreativeAction : AgentActionBase
{
    public override string Command => "set_creative";

    public override AgentActionResponseDto Execute(IGameAgentBridge bridge, HttpListenerRequest request)
    {
        string? creativeStr = request.QueryString["creative"] ?? request.QueryString["flying"];
        if (bool.TryParse(creativeStr, out bool creative))
        {
            bridge.EnqueueAction(() =>
            {
                var p = bridge.Host.Session.Player;
                p.CreativeMode = creative;
                p.Velocity = System.Numerics.Vector3.Zero;
                if (!creative)
                {
                    p.ForceAirborne();
                }
            }, runImmediatelyInTests: false);

            return Ok($"Set creative mode to {creative}");
        }

        return Fail("Invalid or missing 'creative' parameter (must be true or false)");
    }
}

internal sealed class SelectSlotAction : AgentActionBase
{
    public override string Command => "select_slot";

    public override AgentActionResponseDto Execute(IGameAgentBridge bridge, HttpListenerRequest request)
    {
        string? slotStr = request.QueryString["slot"];
        if (int.TryParse(slotStr, out int slot) && slot is >= 0 and < 9)
        {
            bridge.EnqueueAction(() =>
            {
                bridge.Host.Session.Player.SelectedSlot = slot;
            }, runImmediatelyInTests: false);

            return Ok($"Selected inventory slot {slot}");
        }

        return Fail("Invalid or missing 'slot' parameter (must be 0-8)");
    }
}

internal sealed class ShutdownAction : AgentActionBase
{
    public override string Command => "shutdown";

    public override AgentActionResponseDto Execute(IGameAgentBridge bridge, HttpListenerRequest request)
    {
        bridge.EnqueueAction(bridge.RequestExit, runImmediatelyInTests: false);
        return Ok("Shutdown command received");
    }
}

internal sealed class SetTimeAction : AgentActionBase
{
    public override string Command => "set_time";

    public override AgentActionResponseDto Execute(IGameAgentBridge bridge, HttpListenerRequest request)
    {
        string? timeStr = request.QueryString["value"];
        if (float.TryParse(timeStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float timeVal))
        {
            bridge.EnqueueAction(() => bridge.SetTimeOfDay(timeVal), runImmediatelyInTests: false);
            return Ok($"Time set to {timeVal}");
        }

        return Fail("Invalid or missing 'value' parameter (0-1)");
    }
}

internal sealed class SetTimeScaleAction : AgentActionBase
{
    public override string Command => "set_time_scale";

    public override AgentActionResponseDto Execute(IGameAgentBridge bridge, HttpListenerRequest request)
    {
        string? scaleStr = request.QueryString["value"];
        if (float.TryParse(scaleStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float scaleVal))
        {
            bridge.EnqueueAction(() =>
            {
                bridge.Host.TimeScale = Math.Max(0f, scaleVal);
                bridge.Host.TimePaused = scaleVal <= 0f;
                bridge.SyncTimeFromHost();
            }, runImmediatelyInTests: false);

            return Ok($"Time scale set to {scaleVal}");
        }

        return Fail("Invalid or missing 'value' parameter");
    }
}

internal sealed class OpenCrucibleAction : AgentActionBase
{
    public override string Command => "open_crucible";

    public override AgentActionResponseDto Execute(IGameAgentBridge bridge, HttpListenerRequest request)
    {
        var openTcs = new TaskCompletionSource<bool>();
        bridge.EnqueueAction(() =>
        {
            var interaction = bridge.Host.Session.BlockInteraction;
            if (interaction.TargetBlockPos.HasValue && interaction.TargetBlockType.IsStation())
            {
                var pos = interaction.TargetBlockPos.Value;
                bridge.OpenCrucibleAt(
                    (int)pos.X,
                    (int)pos.Y,
                    (int)pos.Z,
                    interaction.TargetBlockType);
                openTcs.SetResult(true);
            }
            else
            {
                openTcs.SetResult(false);
            }
        }, runImmediatelyInTests: false);

        if (openTcs.Task.Wait(AgentActionTimeouts.QueuedActionWaitMs) && openTcs.Task.Result)
        {
            return Ok("Station UI opened for targeted station");
        }

        return Fail("No crafting station targeted");
    }
}

internal sealed class DevConsoleAction : AgentActionBase
{
    public override string Command => "dev";

    public override AgentActionResponseDto Execute(IGameAgentBridge bridge, HttpListenerRequest request)
    {
        string? devCmd = request.QueryString["cmd_line"];
        if (string.IsNullOrWhiteSpace(devCmd))
        {
            return Fail("Missing 'cmd_line' parameter");
        }

        var tcs = new TaskCompletionSource<string>();
        bridge.EnqueueAction(() =>
        {
            tcs.SetResult(bridge.ExecuteDevCommand(devCmd));
            bridge.SyncTimeFromHost();
        }, runImmediatelyInTests: false);

        if (tcs.Task.Wait(AgentActionTimeouts.QueuedActionWaitMs))
        {
            string message = tcs.Task.Result;
            if (string.IsNullOrEmpty(message))
            {
                message = "OK";
            }

            return Ok(message);
        }

        return Fail("Dev command timed out");
    }
}

internal sealed class RecruitVillagerAction : AgentActionBase
{
    public override string Command => "recruit_villager";

    public override AgentActionResponseDto Execute(IGameAgentBridge bridge, HttpListenerRequest request)
    {
        var recruitTcs = new TaskCompletionSource<bool>();
        bridge.EnqueueAction(() =>
        {
            var v = bridge.Host.Session.Villages.GetActiveVillage(bridge.Host.Session.Player.Position);
            recruitTcs.SetResult(v != null && bridge.Host.Session.Villages.TryRecruit(v, bridge.Host.Session.Grid));
        }, runImmediatelyInTests: false);

        bool success = recruitTcs.Task.Wait(AgentActionTimeouts.QueuedActionWaitMs) && recruitTcs.Task.Result;
        return success ? Ok("Recruited villager") : Fail("Recruit failed");
    }
}

internal sealed class AssignJobAction : AgentActionBase
{
    public override string Command => "assign_job";

    public override AgentActionResponseDto Execute(IGameAgentBridge bridge, HttpListenerRequest request)
    {
        if (!int.TryParse(request.QueryString["villager_id"], out int vid) ||
            !Enum.TryParse<JobType>(request.QueryString["job"], true, out var jobType))
        {
            return Fail("Need villager_id and job params");
        }

        float? tgx = float.TryParse(request.QueryString["target_x"], out float tfx) ? tfx : null;
        float? tgy = float.TryParse(request.QueryString["target_y"], out float tfy) ? tfy : null;
        float? tgz = float.TryParse(request.QueryString["target_z"], out float tfz) ? tfz : null;
        System.Numerics.Vector3? target = tgx.HasValue && tgy.HasValue && tgz.HasValue
            ? new System.Numerics.Vector3(tgx.Value, tgy.Value, tgz.Value)
            : null;

        var assignTcs = new TaskCompletionSource<bool>();
        bridge.EnqueueAction(() =>
        {
            var session = bridge.Host.Session;
            var village = session.Villages.GetActiveVillage(session.Player.Position);
            if (village == null || !session.Villagers.TryGet(vid, out var villager))
            {
                assignTcs.SetResult(false);
                return;
            }

            assignTcs.SetResult(session.Villages.TryAssignJob(village, villager, jobType, target));
        }, runImmediatelyInTests: false);

        bool success = assignTcs.Task.Wait(AgentActionTimeouts.QueuedActionWaitMs) && assignTcs.Task.Result;
        return success ? Ok($"Assigned {jobType}") : Fail("Assign failed");
    }
}

internal sealed class QueueBuildAction : AgentActionBase
{
    public override string Command => "queue_build";

    public override AgentActionResponseDto Execute(IGameAgentBridge bridge, HttpListenerRequest request)
    {
        string? blueprintId = request.QueryString["blueprint_id"];
        if (string.IsNullOrWhiteSpace(blueprintId) ||
            !int.TryParse(request.QueryString["anchor_x"], out int anchorX) ||
            !int.TryParse(request.QueryString["anchor_z"], out int anchorZ))
        {
            return Fail("Need blueprint_id, anchor_x, and anchor_z params");
        }

        int? anchorY = int.TryParse(request.QueryString["anchor_y"], out int parsedAnchorY)
            ? parsedAnchorY
            : null;
        var queueTcs = new TaskCompletionSource<bool>();
        bridge.EnqueueAction(() =>
        {
            var session = bridge.Host.Session;
            var village = session.Villages.GetActiveVillage(session.Player.Position);
            if (village == null)
            {
                queueTcs.SetResult(false);
                return;
            }

            session.Villages.CreativeMode = session.Player.CreativeMode;
            queueTcs.SetResult(session.Villages.TryQueueBlueprint(
                session.Grid,
                village,
                blueprintId,
                anchorX,
                anchorZ,
                village.Storage,
                anchorY ?? -1));
        }, runImmediatelyInTests: false);

        bool success = queueTcs.Task.Wait(AgentActionTimeouts.QueuedActionWaitMs) && queueTcs.Task.Result;
        return success
            ? Ok($"Queued {blueprintId}")
            : Fail($"Queue build failed for {blueprintId}");
    }
}

internal sealed class OpenVillageAction : AgentActionBase
{
    public override string Command => "open_village";

    public override AgentActionResponseDto Execute(IGameAgentBridge bridge, HttpListenerRequest request)
    {
        var openTcs = new TaskCompletionSource<bool>();
        bridge.EnqueueAction(() =>
        {
            bridge.RequestOpenVillageUi();
            openTcs.SetResult(true);
        }, runImmediatelyInTests: false);

        bool success = openTcs.Task.Wait(AgentActionTimeouts.QueuedActionWaitMs) && openTcs.Task.Result;
        return success ? Ok("Village UI opened") : Fail("Failed to open village UI");
    }
}

internal sealed class CloseVillageAction : AgentActionBase
{
    public override string Command => "close_village";

    public override AgentActionResponseDto Execute(IGameAgentBridge bridge, HttpListenerRequest request)
    {
        var closeTcs = new TaskCompletionSource<bool>();
        bridge.EnqueueAction(() =>
        {
            bridge.RequestCloseVillageUi();
            closeTcs.SetResult(true);
        }, runImmediatelyInTests: false);

        bool success = closeTcs.Task.Wait(AgentActionTimeouts.QueuedActionWaitMs) && closeTcs.Task.Result;
        return success ? Ok("Village UI closed") : Fail("Failed to close village UI");
    }
}

internal sealed class CloseVillageUiAliasAction : AgentActionBase
{
    private static readonly CloseVillageAction CloseVillage = new();

    public override string Command => "close_village_ui";

    public override AgentActionResponseDto Execute(IGameAgentBridge bridge, HttpListenerRequest request)
        => CloseVillage.Execute(bridge, request);
}

internal sealed class SummonSettlersAction : AgentActionBase
{
    public override string Command => "summon_settlers";

    public override AgentActionResponseDto Execute(IGameAgentBridge bridge, HttpListenerRequest request)
    {
        var repairTcs = new TaskCompletionSource<bool>();
        bridge.EnqueueAction(() =>
        {
            var session = bridge.Host.Session;
            var village = session.Villages.GetActiveVillage(session.Player.Position);
            repairTcs.SetResult(village != null && session.Villages.RepairVillageCitizens(village, session.Grid));
        }, runImmediatelyInTests: false);

        bool success = repairTcs.Task.Wait(AgentActionTimeouts.QueuedActionWaitMs) && repairTcs.Task.Result;
        return success ? Ok("Settlers summoned") : Fail("Summon failed (stand near Town Heart)");
    }
}

