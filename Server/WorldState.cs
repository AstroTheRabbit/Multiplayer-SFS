using System;
using System.Collections.Generic;
using System.Linq;
using SFS.IO;
using SFS.Parts;
using SFS.World;

namespace MultiplayerSFS.Server
{
    public static class WorldState
    {
        public static Random idGenerator = new Random();
        public static FolderPath savePath = null;
        public static double worldTime = 0;
        public static Dictionary<int, RocketState> rockets = new Dictionary<int, RocketState>();
        public static Dictionary<int, PartState> parts = new Dictionary<int, PartState>();

        public static void LoadFromSave(string path)
        {
            savePath = new FolderPath(path);
            // TODO: Loading worlds from saves.
        }

        public static int AddRocket(RocketState state)
        {
            int id;
            do
            {
                id = idGenerator.Next();
            }
            while (rockets.ContainsKey(id));
            rockets.Add(id, state);
            return id;
        }

        public static int AddPart(PartState state)
        {
            int id;
            do
            {
                id = idGenerator.Next();
            }
            while (parts.ContainsKey(id));
            parts.Add(id, state);
            return id;
        }
    }

    public class RocketState
    {
        public string rocketName;
        public WorldSave.LocationData location;
        public float rotation;
        public float angularVelocity;
        public bool throttleOn;
        public float throttlePercent;
        public bool RCS;
        public List<int> partIDs;
        public List<JointState> joints;
        public List<StageState> stages;

        public RocketState(RocketSave save)
        {
            rocketName = save.rocketName;
            location = save.location;
            rotation = save.rotation;
            angularVelocity = save.angularVelocity;
            throttleOn = save.throttleOn;
            throttlePercent = save.throttlePercent;
            RCS = save.RCS;

            Dictionary<int, int> partIndexToID = new Dictionary<int, int>(save.parts.Length);
            partIDs = new List<int>(save.parts.Length);

            for (int i = 0; i < save.parts.Length; i++)
            {
                PartState part = new PartState(save.parts[i]);
                int id = WorldState.AddPart(part);
                partIndexToID.Add(i, id);
                partIDs.Add(id);
            }

            joints = save.joints.Select((JointSave joint) => new JointState(joint, partIndexToID)).ToList();
            stages = save.stages.Select((StageSave stage) => new StageState(stage, partIndexToID)).ToList();
        }
    }

    public class PartState
    {
        public PartSave part;

        public PartState(PartSave save)
        {
            part = save;
        }
    }

    public class JointState
    {
        public int id_A;
        public int id_B;

        public JointState(JointSave save, Dictionary<int, int> partIndexToID)
        {
            id_A = partIndexToID[save.partIndex_A];
            id_B = partIndexToID[save.partIndex_B];
        }
    }

    public class StageState
    {
        public int stageID;
        public List<int> partIDs;

        public StageState(StageSave save, Dictionary<int, int> partIndexToID)
        {
            stageID = save.stageId;
            partIDs = save.partIndexes.Select((int idx) => partIndexToID[idx]).ToList();
        }
    }
}