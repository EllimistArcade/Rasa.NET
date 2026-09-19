namespace Rasa.Data
{
    /// <summary>
    /// generated/client/notification.py. Recv_Notification posts the id and its arguments as a
    /// GAME_CONTEXT_NOTIFICATION client event; only the ids marked below are picked up by a
    /// handler in this client build (client/gameui.py, client/objectanimationmgr.py,
    /// client/backgroundaudiomgr.py). The rest are accepted and dropped.
    /// </summary>
    public enum NotificationId
    {
        BigMessage              = 1,    // reaches gameui, which does nothing with it
        InfoMessage             = 2,
        ObjectiveMet            = 6,
        Victory                 = 7,
        LocationAudio           = 8,    // handled: (targetEntityId, audioSpecId)
        BackgroundAudio         = 9,    // handled: (audioSpecId,)
        ObjectiveHint           = 10,
        UseObject               = 11,
        ObjectiveFailed         = 12,
        StopLocationAudio       = 13,   // handled: (targetEntityId,)
        ObjectAnimation         = 14,   // handled: (targetEntityId, animationSpecId)
        StopObjectAnimation     = 15,   // handled: (targetEntityId,)
        DisplayCounter          = 16,
        StopCounterDisplay      = 17,
        DisplayTimer            = 18,   // handled: (timerType, currentValue, isRunning, countdown)
        StopTimerDisplay        = 19,   // handled: no arguments
        DisplayInteraction      = 20,
        HideInteraction         = 21
    }
}
