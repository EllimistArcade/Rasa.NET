using System;
using System.Collections.Generic;
using System.Numerics;
using DotRecast.Core;
using DotRecast.Core.Numerics;
using DotRecast.Detour;

namespace Rasa.Navigation
{
    /// <summary>
    /// The handful of Detour queries the game needs, on one map's navmesh: a walkable path between
    /// two points, a random walkable point near another, the ground under a point, and whether a
    /// straight walk is clear. Positions are game coordinates (metres, y up), the same frame the
    /// navmesh was built in.
    ///
    /// <see cref="DtNavMeshQuery"/> is not thread safe; each map worker uses its own instance.
    /// </summary>
    public sealed class NavMeshQuery
    {
        /// <summary>How far from a point to look for the polygon under it: a few metres sideways, well above and below.</summary>
        public static readonly RcVec3f SearchExtents = new RcVec3f(4f, 8f, 4f);

        private const int MaxPolys = 4096;
        private const int MaxStraightPath = 256;

        private readonly DtNavMesh _navMesh;
        private readonly DtNavMeshQuery _query;
        private readonly IDtQueryFilter _filter = new DtQueryDefaultFilter();
        private readonly IRcRand _random = new RcRand();
        private readonly long[] _polys = new long[MaxPolys];
        private readonly DtStraightPath[] _straight = new DtStraightPath[MaxStraightPath];

        public NavMeshQuery(DtNavMesh navMesh)
        {
            _navMesh = navMesh;
            _query = new DtNavMeshQuery(navMesh);
        }

        public DtNavMesh NavMesh => _navMesh;

        /// <summary>The nearest walkable point to <paramref name="position"/>, or null when nothing is within the search extents.</summary>
        public Vector3? Nearest(Vector3 position)
        {
            return FindPoly(position, out var point) != 0 ? ToVector(point) : (Vector3?)null;
        }

        /// <summary>Whether there is walkable surface within the search extents of the point.</summary>
        public bool IsOnMesh(Vector3 position)
        {
            return FindPoly(position, out _) != 0;
        }

        /// <summary>
        /// Whether the walkable surface nearest the point was built under the terrain heightmap
        /// (<see cref="NavMeshFlags.Underground"/>): inside a cave or tunnel rather than on the
        /// surface above it. False off the mesh, and everywhere on a mesh built without the flag.
        /// </summary>
        public bool IsUnderground(Vector3 position)
        {
            var poly = FindPoly(position, out _);

            if (poly == 0)
                return false;

            return _navMesh.GetTileAndPolyByRef(poly, out _, out var p).Succeeded() && (p.flags & NavMeshFlags.Underground) != 0;
        }

        /// <summary>The height of the walkable surface under (x, z) nearest to the point's y, or null.</summary>
        public float? GroundHeight(Vector3 position)
        {
            var poly = FindPoly(position, out var point);

            if (poly == 0)
                return null;

            return _query.GetPolyHeight(poly, point, out var height).Succeeded() ? height : point.Y;
        }

        /// <summary>
        /// A walkable route from <paramref name="start"/> to <paramref name="end"/>: the corner points
        /// to walk through in order, ending at the reachable point nearest <paramref name="end"/>.
        /// The start itself is not included. Null when either point is off the mesh.
        /// <paramref name="complete"/> is false when the end could not be reached and the path stops short.
        /// </summary>
        public List<Vector3> FindPath(Vector3 start, Vector3 end, out bool complete)
        {
            complete = false;

            var startRef = FindPoly(start, out var startPoint);
            var endRef = FindPoly(end, out var endPoint);

            if (startRef == 0 || endRef == 0)
                return null;

            var status = _query.FindPath(startRef, endRef, startPoint, endPoint, _filter, _polys, out var polyCount, MaxPolys);

            if (status.Failed() || polyCount == 0)
                return null;

            // A partial path ends on the last polygon found; walk to the point on it nearest the goal.
            var target = endPoint;

            if (_polys[polyCount - 1] != endRef)
                _query.ClosestPointOnPoly(_polys[polyCount - 1], endPoint, out target, out _);
            else
                complete = true;

            status = _query.FindStraightPath(startPoint, target, _polys, polyCount, _straight, out var straightCount, MaxStraightPath, 0);

            if (status.Failed() || straightCount == 0)
                return null;

            var result = new List<Vector3>(straightCount);

            // straight[0] is the start position itself.
            for (var i = 1; i < straightCount; i++)
                result.Add(ToVector(_straight[i].pos));

            if (result.Count == 0)
                result.Add(ToVector(target));

            return result;
        }

        /// <summary>A random walkable point within <paramref name="radius"/> of <paramref name="centre"/>, or null.</summary>
        public Vector3? RandomPointAround(Vector3 centre, float radius)
        {
            var centreRef = FindPoly(centre, out var centrePoint);

            if (centreRef == 0)
                return null;

            var status = _query.FindRandomPointAroundCircle(centreRef, centrePoint, radius, _filter, _random, out _, out var point);

            return status.Succeeded() ? ToVector(point) : (Vector3?)null;
        }

        /// <summary>
        /// Whether a straight walk from <paramref name="start"/> to <paramref name="end"/> stays on the
        /// mesh (no wall, no drop). False when the start is off the mesh.
        /// </summary>
        public bool IsWalkClear(Vector3 start, Vector3 end)
        {
            var startRef = FindPoly(start, out var startPoint);

            if (startRef == 0)
                return false;

            var status = _query.Raycast(startRef, startPoint, ToRc(end), _filter, out var t, out _, _polys, out _, MaxPolys);

            return status.Succeeded() && t >= float.MaxValue; // t == MAX means nothing was hit
        }

        // The netstandard build of DotRecast has no Vector3 conversions.
        private static RcVec3f ToRc(Vector3 v) => new RcVec3f(v.X, v.Y, v.Z);
        private static Vector3 ToVector(RcVec3f v) => new Vector3(v.X, v.Y, v.Z);

        private long FindPoly(Vector3 position, out RcVec3f point)
        {
            var status = _query.FindNearestPoly(ToRc(position), SearchExtents, _filter, out var poly, out point, out _);

            return status.Succeeded() ? poly : 0;
        }
    }
}
