using System.Numerics;

namespace Rasa.Packets
{
    using Memory;

    /// <summary>
    /// Which of the three forms an <see cref="ActionTarget"/> arrived in.
    /// </summary>
    public enum ActionTargetKind
    {
        /// <summary>The client sent None: no target. Area tools and blind shots do this.</summary>
        None = 0,

        /// <summary>An entity id.</summary>
        Entity = 1,

        /// <summary>A point on the ground.</summary>
        Location = 2
    }

    /// <summary>
    /// The target element of an action request.
    ///
    /// Every one of the client's three action senders builds it the same way -
    /// <c>actions/tools/basetoolaction.py</c>, <c>actions/weapons/baseweaponattack.py</c> and
    /// <c>actions/abilities/baseactorability.py</c> all end in:
    ///
    /// <code>
    /// if self.targetType == TARGET_LOCATION:
    ///     target = self._targetLocation
    /// else:
    ///     target = self.targetId
    /// </code>
    ///
    /// so one slot carries three different things: an entity id, None when there is no target,
    /// or a position when the action is aimed at the ground. A reader that assumes one of those
    /// throws on the others, and a throw out of <c>Read</c> is caught in <c>Client.Update</c> as
    /// a malformed packet and closes the connection - so the tolerance here is what keeps a
    /// legitimate request from disconnecting the player who made it.
    ///
    /// Of the six tool actions none uses TARGET_LOCATION today (an area tool sets TARGET_NONE and
    /// sends None instead), so tools only ever send two of the three. Abilities do use it -
    /// turret, trap, airstrike, fire support and the thrown consumables all set TARGET_LOCATION.
    /// </summary>
    public readonly struct ActionTarget
    {
        public ActionTargetKind Kind { get; }

        /// <summary>The target entity, or 0 when <see cref="Kind"/> is not Entity.</summary>
        public ulong EntityId { get; }

        /// <summary>The aimed-at point, or zero when <see cref="Kind"/> is not Location.</summary>
        public Vector3 Location { get; }

        private ActionTarget(ActionTargetKind kind, ulong entityId, Vector3 location)
        {
            Kind = kind;
            EntityId = entityId;
            Location = location;
        }

        public bool HasEntity => Kind == ActionTargetKind.Entity;

        public static ActionTarget Read(PythonReader pr)
        {
            switch (pr.PeekType())
            {
                case PythonType.Long:
                    return new ActionTarget(ActionTargetKind.Entity, pr.ReadULong(), Vector3.Zero);

                // Entity ids marshal as longs, but a small one can arrive in the int form, and
                // reading it as a long would throw on the type check.
                case PythonType.Int:
                    return new ActionTarget(ActionTargetKind.Entity, pr.ReadUInt(), Vector3.Zero);

                case PythonType.Tuple:
                case PythonType.List:
                    return ReadLocation(pr);

                default:
                    // None, and the Zero and True structs that share its type nibble. Consumes
                    // exactly the marker byte, whichever of the three it is.
                    pr.ReadUnkStruct();
                    return new ActionTarget(ActionTargetKind.None, 0, Vector3.Zero);
            }
        }

        private static ActionTarget ReadLocation(PythonReader pr)
        {
            var count = pr.PeekType() == PythonType.Tuple ? pr.ReadTuple() : pr.ReadList();
            var values = new double[3];

            // Every element is consumed whatever the count, so the reader is left on the byte
            // after the sequence and the terminator check still lands where it should.
            for (var i = 0; i < count; i++)
            {
                var value = pr.ReadNumber();

                if (i < values.Length)
                    values[i] = value;
            }

            if (count < 3)
                return new ActionTarget(ActionTargetKind.None, 0, Vector3.Zero);

            return new ActionTarget(ActionTargetKind.Location, 0,
                new Vector3((float)values[0], (float)values[1], (float)values[2]));
        }
    }
}
