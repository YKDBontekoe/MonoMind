using System;
using System.Collections.Generic;

namespace Autonocraft.Core.DevCommands
{
    /// <summary>
    /// Represents a single developer console command.
    /// </summary>
    internal interface IDevCommand
    {
        /// <summary>
        /// Primary name of the command (e.g. "time").
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Optional aliases (e.g. "tp", "teleport").
        /// </summary>
        IEnumerable<string> Aliases { get; }

        /// <summary>
        /// Executes the command against the given host context.
        /// The <paramref name="args"/> span contains the raw text
        /// following the command name, without the command itself.
        /// </summary>
        string Execute(GameHostContext host, ReadOnlySpan<char> args);
    }
}

