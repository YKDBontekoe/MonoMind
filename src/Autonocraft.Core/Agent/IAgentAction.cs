using System.Net;

namespace Autonocraft.Core.Agent;

/// <summary>
/// Command pattern interface for /action agent commands.
/// </summary>
internal interface IAgentAction
{
    /// <summary>
    /// The canonical lower-case command name (e.g. "key_down").
    /// </summary>
    string Command { get; }

    /// <summary>
    /// Execute the action for the given HTTP request.
    /// The implementation must not write to the HTTP response.
    /// </summary>
    AgentActionResponseDto Execute(IGameAgentBridge bridge, HttpListenerRequest request);
}

