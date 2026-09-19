using System;

namespace Rasa.Packets.MapChannel.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// client/clientmethod.py Recv_Notification(notificationId, notificationArgs), called on the
    /// client method entity. The arguments are a different shape for every notification, so each
    /// one this client acts on has its own factory below; the argument layouts come from the
    /// handlers in client/gameui.py, client/objectanimationmgr.py and client/backgroundaudiomgr.py.
    /// </summary>
    public class NotificationPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.Notification;

        public NotificationId NotificationId { get; }

        private readonly Action<PythonWriter> _writeArgs;

        private NotificationPacket(NotificationId notificationId, Action<PythonWriter> writeArgs)
        {
            NotificationId = notificationId;
            _writeArgs = writeArgs;
        }

        /// <summary>
        /// Puts up the on-screen timer. currentValue is what the timer starts from, in seconds;
        /// countdown makes it run down rather than up. isRunning false leaves the timer in place
        /// but stops it.
        /// </summary>
        public static NotificationPacket DisplayTimer(TimerType timerType, int currentValue, bool isRunning, bool countdown)
        {
            return new NotificationPacket(NotificationId.DisplayTimer, pw =>
            {
                pw.WriteTuple(4);
                pw.WriteUInt((uint)timerType);
                pw.WriteInt(currentValue);
                pw.WriteBool(isRunning);
                pw.WriteBool(countdown);
            });
        }

        /// <summary>Stops the on-screen timer. gameui ignores the arguments.</summary>
        public static NotificationPacket StopTimer()
        {
            return new NotificationPacket(NotificationId.StopTimerDisplay, pw => pw.WriteNoneStruct());
        }

        /// <summary>Plays an object animation (generated/client/animationdata.py objectAnimationSpecification) on an entity.</summary>
        public static NotificationPacket ObjectAnimation(ulong targetEntityId, uint animationSpecId)
        {
            return new NotificationPacket(NotificationId.ObjectAnimation, pw =>
            {
                pw.WriteTuple(2);
                pw.WriteULong(targetEntityId);
                pw.WriteUInt(animationSpecId);
            });
        }

        public static NotificationPacket StopObjectAnimation(ulong targetEntityId)
        {
            return new NotificationPacket(NotificationId.StopObjectAnimation, pw =>
            {
                pw.WriteTuple(1);
                pw.WriteULong(targetEntityId);
            });
        }

        /// <summary>Starts a zone's background audio (generated/client/audiodata.py audioSpecification).</summary>
        public static NotificationPacket BackgroundAudio(uint audioSpecId)
        {
            return new NotificationPacket(NotificationId.BackgroundAudio, pw =>
            {
                pw.WriteTuple(1);
                pw.WriteUInt(audioSpecId);
            });
        }

        /// <summary>Plays audio positioned on an entity; the client drops it if it cannot see that entity.</summary>
        public static NotificationPacket LocationAudio(ulong targetEntityId, uint audioSpecId)
        {
            return new NotificationPacket(NotificationId.LocationAudio, pw =>
            {
                pw.WriteTuple(2);
                pw.WriteULong(targetEntityId);
                pw.WriteUInt(audioSpecId);
            });
        }

        public static NotificationPacket StopLocationAudio(ulong targetEntityId)
        {
            return new NotificationPacket(NotificationId.StopLocationAudio, pw =>
            {
                pw.WriteTuple(1);
                pw.WriteULong(targetEntityId);
            });
        }

        /// <summary>
        /// Any notification, with a list of plain integers as its arguments. For ids this client
        /// has no handler for, and for trying argument shapes from the console.
        /// </summary>
        public static NotificationPacket Raw(NotificationId notificationId, params long[] args)
        {
            return new NotificationPacket(notificationId, pw =>
            {
                pw.WriteTuple(args.Length);

                foreach (var arg in args)
                    if (arg >= int.MinValue && arg <= int.MaxValue)
                        pw.WriteInt((int)arg);
                    else
                        pw.WriteLong(arg);
            });
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(2);
            pw.WriteUInt((uint)NotificationId);
            _writeArgs(pw);
        }
    }
}
