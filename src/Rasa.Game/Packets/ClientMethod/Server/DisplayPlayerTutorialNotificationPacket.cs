namespace Rasa.Packets.ClientMethod.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// client/clientmethod.py:255 - Recv_DisplayPlayerTutorialNotification(tutorialId).
    ///
    /// One id and nothing else: the client owns the tutorial's text and artwork, and all the
    /// server does is decide when to raise it. The client also raises several of these itself -
    /// the duel one from wargame.py, the waypoint one from the waypoint window - so the server
    /// is one of two sources rather than the only one.
    ///
    /// It does not own the audio, despite the window having a field for it: the audioSetId
    /// column is None in all 56 tutorialdata rows, so raising a tutorial plays nothing. Voice
    ///-over needs PlayTutorialAudio alongside this one.
    /// </summary>
    public class DisplayPlayerTutorialNotificationPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.DisplayPlayerTutorialNotification;

        public TutorialId TutorialId { get; set; }

        internal DisplayPlayerTutorialNotificationPacket(TutorialId tutorialId)
        {
            TutorialId = tutorialId;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(1);
            pw.WriteUInt((uint)TutorialId);
        }
    }
}
