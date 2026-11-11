using System;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace Rasa.Structures
{
    using Repositories;

    internal sealed class GameplayRejectionException : InvalidOperationException
    {
        internal GameplayRejectionException(string message) : base(message)
        {
        }

        /// <summary>A rejection the player is told of, with the client's own message for it.</summary>
        internal GameplayRejectionException(string message, Rasa.Data.PlayerMessage playerMessage) : base(message)
        {
            PlayerMessage = playerMessage;
        }

        internal GameplayRejectionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>What the player is shown for it, if anything (CommunicatorManager.ShowPlayerMessage).</summary>
        internal Rasa.Data.PlayerMessage? PlayerMessage { get; }

        /// <summary>The player message an expected rejection carries, if it carries one.</summary>
        internal static Rasa.Data.PlayerMessage? PlayerMessageOf(Exception error) =>
            (error as GameplayRejectionException)?.PlayerMessage;

        internal static bool IsExpected(Exception error) =>
            error is GameplayRejectionException or global::Rasa.Missions.Runtime.MissionRuleException or EntityNotFoundException or
                DbException or DbUpdateException;
    }
}
