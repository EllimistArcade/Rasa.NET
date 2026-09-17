using System.Collections.Generic;

namespace Rasa.Structures
{
    using Data;

    /// <summary>
    /// What keeps a usable shut, and what opens it. The eight fields are exactly the arguments of
    /// the client's <c>Recv_LockInfo</c>, because a lock the server does not send is a lock the
    /// client cannot see: with no LockInfo every usable reads cipher level 0, and
    /// <c>usable.py</c>'s IsCipherable() is <c>cipherLevel &gt; 0</c>, so the cipher tool can
    /// never legitimately be aimed at anything.
    ///
    /// A <see cref="DynamicObject"/> with no lock carries null here rather than a lock with
    /// everything zeroed, so "not locked" and "locked by nothing in particular" cannot be
    /// confused for one another.
    ///
    /// Nothing in the world carries one yet. The placements that would - locked doors, Bane
    /// crates, the 68 TreasureDispenser classes - came from server world data that did not
    /// survive; the client's own .map files hold scenery and not one augmented entity. So this is
    /// the mechanism with no subjects, which is the honest half to build: inventing lock levels
    /// for objects the game never locked would be inventing the content, not restoring it.
    /// </summary>
    public class UsableLock
    {
        /// <summary>
        /// Once open, stays open. <c>IsLocked()</c> answers false the moment this is set,
        /// whatever state the object is in, so a ciphered lock cannot re-arm itself by the object
        /// cycling back through its locked state.
        /// </summary>
        public bool Unlocked { get; set; }

        /// <summary>
        /// The state in which this object counts as locked. <c>IsLocked()</c> is
        /// <c>_curStateId == lockStateId</c>, so a door is locked only while it is closed - and a
        /// zero here means the object is never locked at all, which is how the client reads it.
        /// </summary>
        public UseObjectState LockStateId { get; set; }

        /// <summary>The state the object is put into when the lock opens.</summary>
        public UseObjectState UnlockedStateId { get; set; }

        /// <summary>A completed mission that opens it, or 0.</summary>
        public uint MissionId { get; set; }

        /// <summary>Logos stones that between them open it. Every one is needed, not any one.</summary>
        public List<uint> LogosIds { get; } = new List<uint>();

        /// <summary>An item that opens it by being carried, or 0.</summary>
        public uint KeyItemTemplateId { get; set; }

        /// <summary>
        /// The Specialist Tools level needed to cipher it. Zero means not cipherable at all, and
        /// that is the field the whole cipher path hangs off: usable.py checks
        /// <c>skillLevel &gt;= cipherLevel</c> and refuses with PM_CIPHER_FAIL_SKILL_TOO_LOW.
        /// </summary>
        public int CipherLevel { get; set; }

        /// <summary>Player flags any one of which opens it.</summary>
        public List<uint> PlayerFlagReqs { get; } = new List<uint>();

        /// <summary>Player flags any one of which bars it, checked before the requirements.</summary>
        public List<uint> PlayerFlagExs { get; } = new List<uint>();

        /// <summary>
        /// Decode attempts left on this lock, successes and failures alike. Set from
        /// <see cref="Cipher.AttemptsPerLock"/> when the lock is built.
        /// </summary>
        public int CipherAttemptsLeft { get; set; } = Cipher.AttemptsPerLock;

        /// <summary>
        /// Whether a cipher may be attempted on this at all - it has a level, it has not already
        /// been opened, and the object is sitting in its locked state.
        /// </summary>
        public bool IsCipherableIn(UseObjectState state)
        {
            return CipherLevel > 0 && IsLockedIn(state);
        }

        /// <summary>usable.py's IsLocked(), which a state of 0 answers false for.</summary>
        public bool IsLockedIn(UseObjectState state)
        {
            if (Unlocked)
                return false;

            if (LockStateId == 0)
                return false;

            return state == LockStateId;
        }
    }
}
