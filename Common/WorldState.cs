using System;
using System.Linq;
using System.Collections.Generic;
using Lidgren.Network;
using UnityEngine;
using SFS.IO;
using SFS.Parts;
using SFS.World;
using SFS.WorldBase;
using SFS.Parsers.Json;
using Random = System.Random;
using static SFS.World.WorldSave;

namespace MultiplayerSFS.Common
{
    public static class IDExtensions
    {
        static readonly Random generator = new Random();
        public static int InsertNew<T>(this Dictionary<int, T> dict, T item)
        {
            int id; do
            {
                id = generator.Next();
            }
            while (dict.ContainsKey(id));
            dict.Add(id, item);
            return id;
        }

        public static int InsertNew(this HashSet<int> set)
        {
            int id; do
            {
                id = generator.Next();
            }
            while (set.Contains(id));
            set.Add(id);
            return id;
        }
    }

    public class WorldState
    {
        public double worldTime;
        public Difficulty.DifficultyType difficulty;
        public Dictionary<int, RocketState> rockets;

        public WorldState()
        {
            worldTime = 0;
            difficulty = Difficulty.DifficultyType.Normal;
            rockets = new Dictionary<int, RocketState>();
        }

        public WorldState(string path)
        {
            FolderPath folder = new FolderPath(path);
            FolderPath persistent = folder.CloneAndExtend("Persistent");
            if (!folder.FolderExists())
                throw new Exception("Save folder cannot be found or does not exist.");
            if (!persistent.FolderExists())
                throw new Exception("'Persistent' folder cannot be found or does not exist.");

            if (!JsonWrapper.TryLoadJson(folder.ExtendToFile("WorldSettings.txt"), out WorldSettings settings))
                throw new Exception("'WorldSettings.txt' file cannot be found or could not be loaded.");

            if (!JsonWrapper.TryLoadJson(persistent.ExtendToFile("WorldState.txt"), out WorldSave.WorldState state))
                throw new Exception("'WorldState.txt' file cannot be found or could not be loaded.");

            if (!JsonWrapper.TryLoadJson(persistent.ExtendToFile("Rockets.txt"), out List<RocketSave> rocketSaves))
                throw new Exception("'Rockets.txt' file cannot be found or could not be loaded.");

            worldTime = state.worldTime;
            difficulty = settings.difficulty.difficulty;
            rockets = new Dictionary<int, RocketState>();
            foreach (RocketSave save in rocketSaves)
            {
                rockets.InsertNew(new RocketState(save));
            }
        }
    }

    public class RocketState : INetData
    {
        public string rocketName;
        public LocationData location;
        public float rotation;
        public float angularVelocity;
        public bool throttleOn;
        public float throttlePercent;
        public bool RCS;

        public float input_Turn;
        public Vector2 input_Raw;
        public Vector2 input_Horizontal;
        public Vector2 input_Vertical;

        public Dictionary<int, PartState> parts;
        public List<JointState> joints;
        public List<StageState> stages;

        public RocketState() {}

        public RocketState(RocketSave save)
        {
            rocketName = save.rocketName;
            location = save.location;
            rotation = save.rotation;
            angularVelocity = save.angularVelocity;
            throttleOn = save.throttleOn;
            throttlePercent = save.throttlePercent;
            RCS = save.RCS;

            input_Turn = 0;
            input_Raw = Vector2.zero;
            input_Horizontal = Vector2.zero;
            input_Vertical = Vector2.zero;

            Dictionary<int, int> partIndexToID = new Dictionary<int, int>(save.parts.Length);
            parts = new Dictionary<int, PartState>(save.parts.Length);

            for (int i = 0; i < save.parts.Length; i++)
            {
                PartState part = new PartState(save.parts[i]);
                int id = parts.InsertNew(part);
                partIndexToID.Add(i, id);
            }

            joints = save.joints.Select((JointSave joint) => new JointState(joint, partIndexToID)).ToList();
            stages = save.stages.Select((StageSave stage) => new StageState(stage, partIndexToID)).ToList();
        }

        public void Serialize(NetOutgoingMessage msg)
        {
            msg.Write(rocketName);
            msg.Write(location);
            msg.Write(rotation);
            msg.Write(angularVelocity);
            msg.Write(throttleOn);
            msg.Write(throttlePercent);
            msg.Write(RCS);
            msg.WriteCollection
            (
                parts,
                (KeyValuePair<int, PartState> kvp) =>
                {
                    msg.Write(kvp.Key);
                    msg.Write(kvp.Value);
                }
            );
            msg.WriteCollection(joints, msg.Write);
            msg.WriteCollection(stages, msg.Write);
        }
        public void Deserialize(NetIncomingMessage msg)
        {
            rocketName = msg.ReadString();
            location = msg.ReadLocation();
            rotation = msg.ReadFloat();
            angularVelocity = msg.ReadFloat();
            throttleOn = msg.ReadBoolean();
            throttlePercent = msg.ReadFloat();
            RCS = msg.ReadBoolean();

            parts = msg.ReadCollection
            (
                (int count) => new Dictionary<int, PartState>(),
                () => new KeyValuePair<int, PartState>(msg.ReadInt32(), msg.Read<PartState>())
            );
            joints = msg.ReadCollection((int count) => new List<JointState>(count), () => msg.Read<JointState>());
            stages = msg.ReadCollection((int count) => new List<StageState>(count), () => msg.Read<StageState>());
        }
    }

    public class PartState : INetData
    {
        public PartSave part;

        public PartState() {}
        public PartState(PartSave save)
        {
            part = save;
        }

        public void Serialize(NetOutgoingMessage msg)
        {
            msg.Write(part.name);
            msg.Write(part.position);
            msg.Write(part.orientation);
            msg.Write(part.temperature);
            msg.WriteCollection
            (
                part.NUMBER_VARIABLES,
                (KeyValuePair<string, double> kvp) =>
                {
                    msg.Write(kvp.Key);
                    msg.Write(kvp.Value);
                }
            );
            msg.WriteCollection
            (
                part.TOGGLE_VARIABLES,
                (KeyValuePair<string, bool> kvp) =>
                {
                    msg.Write(kvp.Key);
                    msg.Write(kvp.Value);
                }
            );
            msg.WriteCollection
            (
                part.TEXT_VARIABLES,
                (KeyValuePair<string, string> kvp) =>
                {
                    msg.Write(kvp.Key);
                    msg.Write(kvp.Value);
                }
            );
            msg.Write(part.burns);
        }
        public void Deserialize(NetIncomingMessage msg)
        {
            part = new PartSave
            {
                name = msg.ReadString(),
                position = msg.ReadVector2(),
                orientation = msg.ReadOrientation(),
                temperature = msg.ReadFloat(),
                NUMBER_VARIABLES = msg.ReadCollection
                (
                    (int count) => new Dictionary<string, double>(count),
                    () => new KeyValuePair<string, double>(msg.ReadString(), msg.ReadDouble())
                ),
                TOGGLE_VARIABLES = msg.ReadCollection
                (
                    (int count) => new Dictionary<string, bool>(count),
                    () => new KeyValuePair<string, bool>(msg.ReadString(), msg.ReadBoolean())
                ),
                TEXT_VARIABLES = msg.ReadCollection
                (
                    (int count) => new Dictionary<string, string>(count),
                    () => new KeyValuePair<string, string>(msg.ReadString(), msg.ReadString())
                ),
                burns = msg.ReadBurnSave()
            };
        }
    }

    public class JointState : INetData
    {
        public int id_A;
        public int id_B;

        public JointState() {}
        public JointState(int id_A, int id_B)
        {
            this.id_A = id_A;
            this.id_B = id_B;
        }
        public JointState(JointSave save, Dictionary<int, int> partIndexToID)
        {
            id_A = partIndexToID[save.partIndex_A];
            id_B = partIndexToID[save.partIndex_B];
        }

        public void Serialize(NetOutgoingMessage msg)
        {
            msg.Write(id_A);
            msg.Write(id_B);
        }
        public void Deserialize(NetIncomingMessage msg)
        {
            id_A = msg.ReadInt32();
            id_B = msg.ReadInt32();
        }
    }

    public class StageState : INetData
    {
        public int stageID;
        public List<int> partIDs;

        public StageState() {}
        public StageState(StageSave save, Dictionary<int, int> partIndexToID, HashSet<int> onlyInclude = null)
        {
            stageID = save.stageId;
            IEnumerable<int> unfiltered = save.partIndexes.Select((int idx) => partIndexToID[idx]);
            if (onlyInclude != null)
            {
                unfiltered = unfiltered.Where((int id) => onlyInclude.Contains(id));
            }
            partIDs = unfiltered.ToList();
        }

        public void Serialize(NetOutgoingMessage msg)
        {
            msg.Write(stageID);
            msg.WriteCollection(partIDs, msg.Write);
        }
        public void Deserialize(NetIncomingMessage msg)
        {
            stageID = msg.ReadInt32();
            partIDs = msg.ReadCollection((int count) => new List<int>(), msg.ReadInt32);
        }
    }
}