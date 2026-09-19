using System;
using System.Numerics;

namespace Rasa.Structures
{
    using Data;
    using Game;
    using Managers;

    public class Dropship : DynamicObject
    {
        public long PhaseTimeleft { get; set; }
        public byte Phase { get; set; }
        public SpawnPool SpawnPool { get; set; }
        public DropshipType DropshipType { get; set; }

        /// <summary>Departure or arrival; meaningful for a teleporter dropship, see <see cref="DropshipRole"/>.</summary>
        public DropshipRole Role { get; set; }

        internal Client Client { get; set; }
        internal Vector3 Destination { get; set; }
        internal uint DestinationMapId { get; set; }
        internal double DestinationRotation { get; set; }

        /// <summary>A departure whose destination pad is on the map it leaves from: no map change, a flight and a move.</summary>
        internal bool StaysOnMap => Role == DropshipRole.Departure && DestinationMapId == MapContextId;

        public Dropship(Factions faction, DropshipType dropshipType, SpawnPool spawnPool = null)
        {
            EntityId = EntityManager.Instance.GetEntityId;
            EntityClassId = faction == Factions.AFS ? Data.EntityClasses.UsableCrSpawnerHumDropshipV01 : Data.EntityClasses.UsableCrSpawnerBaneDropshipV01;
            Faction = faction;
            StateId = UseObjectState.CsStateBegin;
            PhaseTimeleft = 5000;
            Phase = 0;
            DropshipType = dropshipType;
            SpawnPool = spawnPool;
            MapContextId = spawnPool.MapContextId;
            Position = new Vector3(
                spawnPool.Position.X + (2.0f - (new Random().Next() % 100) * 0.04f),
                spawnPool.Position.Y,
                spawnPool.Position.Z + (2.0f - (new Random().Next() % 100) * 0.04f)
                );
            Rotation = (new Random().Next() % 640) * 0.01f;
        }
        
        /// <summary>
        /// A teleporter dropship for one player. A departure is built where the player stands,
        /// with the pad they chose as its destination; an arrival is built where they have just
        /// been put down, with no destination.
        /// </summary>
        public Dropship(Factions faction, DropshipType dropshipType, Client client, DropshipRole role, Vector3 destination = new Vector3(), uint destinationMapId = 0)
        {
            Role = role;
            EntityId = EntityManager.Instance.GetEntityId;
            EntityClassId = faction == Factions.AFS ? Data.EntityClasses.UsableCrSpawnerHumDropshipV01 : Data.EntityClasses.UsableCrSpawnerBaneDropshipV01;
            Faction = faction;
            StateId = UseObjectState.CsStateBegin;
            PhaseTimeleft = 5000;
            Phase = 0;
            DropshipType = dropshipType;
            Client = client;

            if (client.State == ClientState.Teleporting)
                MapContextId = client.LoadingMap;
            else
                MapContextId = client.Player.MapContextId;

            Position = client.Player.Position;
            Rotation = (new Random().Next() % 640) * 0.01f;
            DynamicObjectType = DynamicObjectType.DropshipTeleporter;
            Destination = destination;
            DestinationMapId = destinationMapId;
        }
    }
}
