namespace Rasa.Data
{
    /// <summary>Matches map_link.kind and the client's map-marker types for links.</summary>
    public enum MapLinkKind : byte
    {
        /// <summary>A pass between two zones (client marker BATTLEFIELD_ENTRANCE).</summary>
        Border = 0,

        /// <summary>A door into an instance map, or the way back out: either end is a mission-context map.</summary>
        Instance = 1
    }
}
