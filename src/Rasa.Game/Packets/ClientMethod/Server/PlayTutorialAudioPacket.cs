namespace Rasa.Packets.ClientMethod.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// client/clientmethod.py:260 - Recv_PlayTutorialAudio(audioSetId). Posts
    /// UI_PLAY_TUTORIAL_AUDIO, which tutorialwindow turns into
    /// backgroundaudiomgr.PlayAudioSet(audioSetId) - a voice-over played on its own, with no
    /// window and no text.
    ///
    /// **This is the only way tutorial audio can be played at all.** The tutorial window does
    /// have its own audio field - tutorialdata rows are
    /// (name, priority, audioSetId, nameId, bodyId, alwaysDisplay), and Show() feeds index 2
    /// straight to _PlayAudio - but that column is None in all 56 rows the client shipped with.
    /// Nothing else in the client posts the event either, so showing a tutorial never makes a
    /// sound, and no voice-over plays unless a server sends this.
    ///
    /// A null audioSetId is not a no-op: _PlayAudio stops whatever it is already playing and
    /// only then plays something new, so None is how a voice-over is cut short.
    ///
    /// The id is an audio set, not a sound - 3,109 of them in generated.client.audiosetdata,
    /// each naming its sounds, category and selection rule. The client resolves it natively, so
    /// an id it does not know is silence rather than an error.
    /// </summary>
    public class PlayTutorialAudioPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.PlayTutorialAudio;

        /// <summary>The audio set to play, or null to stop whatever is playing.</summary>
        public uint? AudioSetId { get; set; }

        internal PlayTutorialAudioPacket(uint? audioSetId)
        {
            AudioSetId = audioSetId;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(1);

            if (AudioSetId.HasValue)
                pw.WriteUInt(AudioSetId.Value);
            else
                pw.WriteNoneStruct();
        }
    }
}
