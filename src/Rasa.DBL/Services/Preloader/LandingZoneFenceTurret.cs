using System.Collections.Generic;

namespace Rasa.Services.Preloader
{
    /// <summary>
    /// The Landing Zone's turret pair on the east wall top over nothing (Landing_zone_fence_turret).
    ///
    /// Add_control_point_turrets stood the pair at (254.74, 172.4, -110.69), on the top of the
    /// solid wall: the ArchHumOutpostFenceStraightEndcapV01 at (254, 163, -109), at the end of the
    /// Straight48V01 that runs south from the CornerV01 at (254, -75). Neither piece has a gun
    /// base (no Human_base_turret bone in its .geo), so the turret stood on the wall's thin edge.
    /// Fence_turret_bases left the Landing Zone's four wall-top pairs as they were. The other three
    /// stand on the tops of CornerV01 corners, where the wall is wide enough to hold one.
    ///
    /// Up takes out both pools (AFS 551131, Bane 551132) and their control_point_link rows, each
    /// only where it still stands as the creature it was given. Down puts back what is missing,
    /// as it was (FenceTurretBases.Insert).
    /// </summary>
    public static class LandingZoneFenceTurret
    {
        public const uint Point = 4, AfsPool = 551131, BanePool = 551132, Map = 1220;

        public static readonly FenceTurretBases.Turret Turret = new(Point, AfsPool, BanePool, Map, 550018, 551001,
            new(254.74, 172.4, -110.69, -0.7895), new(254.74, 172.4, -110.69, -0.7895));

        public static IEnumerable<string> Up() => FenceTurretBases.Remove(Turret);

        public static IEnumerable<string> Down() => FenceTurretBases.Insert(Turret);
    }
}
