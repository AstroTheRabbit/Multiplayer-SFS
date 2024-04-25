using System;
using System.Linq;
using System.Diagnostics;
using Random = System.Random;
using System.Collections.Generic;
using UnityEngine;
using SFS.IO;
using SFS.Parts;
using SFS.World;
using SFS.WorldBase;
using SFS.Parsers.Json;
using SFS.Parts.Modules;
using MultiplayerSFS.Common;
using MultiplayerSFS.Common.Packets;

namespace MultiplayerSFS.Server
{
    public class ServerWorldState
    {
        readonly Random idGenerator = new Random();

        public double worldTime;
        readonly Stopwatch worldTimeUpdate = new Stopwatch();

        public Difficulty.DifficultyType difficulty;
        public Dictionary<int, RocketState> rockets = new Dictionary<int, RocketState>();
        public Dictionary<int, PartState> parts = new Dictionary<int, PartState>();

        public LoadWorldPacket ToPacket()
        {
            return new LoadWorldPacket()
            {
                worldTime = worldTime,
                difficulty = difficulty,
                rockets = rockets,
                parts = parts,
            };
        }

        public ServerWorldState()
        {
            worldTime = 0;
            worldTimeUpdate.Start();
        }

        public ServerWorldState(string worldSavePath)
        {
            try
            {
                FolderPath path = new FolderPath(worldSavePath);

                if (!JsonWrapper.TryLoadJson<WorldSettings>(path.ExtendToFile("WorldSettings.txt"), out var worldSettings) || worldSettings == null)
                    throw new Exception($"'{path.ExtendToFile("WorldSettings.txt")}' could not be loaded.");

                path.Extend("Persistent");
                if (!JsonWrapper.TryLoadJson<WorldSave.WorldState>(path.ExtendToFile("WorldState.txt"), out var worldState) || worldState == null)
                    throw new Exception($"'{path.ExtendToFile("WorldState.txt")}' could not be loaded.");

                if (!JsonWrapper.TryLoadJson<RocketSave[]>(path.ExtendToFile("Rockets.txt"), out var rocketSaves) || rocketSaves == null)
                    throw new Exception($"'{path.ExtendToFile("Rockets.txt")}' could not be loaded.");

                worldTime = worldState.worldTime;
                difficulty = worldSettings.difficulty.difficulty;

                foreach (RocketSave rocketSave in rocketSaves)
                {
                    Dictionary<int, int> partIndicesToIDs = new Dictionary<int, int>();
                    for (int i = 0; i < rocketSave.parts.Length; i++)
                    {
                        PartSave partSave = rocketSave.parts[i];
                        PartState part = new PartState()
                        {
                            name = partSave.name,
                            position = partSave.position,
                            orientation = partSave.orientation,
                            temperature = partSave.temperature,
                            numberVariables = partSave.NUMBER_VARIABLES,
                            toggleVariables = partSave.TOGGLE_VARIABLES,
                            textVariables = partSave.TEXT_VARIABLES,
                        };
                        partIndicesToIDs.Add(i, AddPart(part));
                    }

                    RocketState rocket = new RocketState()
                    {
                        name = rocketSave.rocketName,
                        position = new RocketPositionState()
                        {
                            planet = rocketSave.location.address,
                            position = rocketSave.location.position,
                            velocity = rocketSave.location.velocity,
                            rotation = rocketSave.rotation,
                            angularVelocity = rocketSave.angularVelocity,
                        },
                        throttleOn = rocketSave.throttleOn,
                        throttlePercent = rocketSave.throttlePercent,
                        RCS = rocketSave.RCS,
                        parts = partIndicesToIDs.Values.ToList(),
                        joints = rocketSave.joints.Select((JointSave js) => new JointState()
                        {
                            partID_A = partIndicesToIDs[js.partIndex_A],
                            partID_B = partIndicesToIDs[js.partIndex_B],
                        }).ToList(),
                        stages = rocketSave.stages.Select((StageSave ss) => new StageState()
                        {
                            stageID = ss.stageId,
                            partIDs = ss.partIndexes.Select((int idx) => partIndicesToIDs[idx]).ToList(),
                        }).ToList(),
                    };
                    AddRocket(rocket);
                }

                worldTimeUpdate.Start();
            }
            catch (Exception e)
            {
                throw new Exception("new ServerWorldState(): Encountered an error!", e);
            }
        }

        public void Update()
        {
            worldTime += worldTimeUpdate.Elapsed.TotalSeconds;
            worldTimeUpdate.Restart();
        }

        public int AddRocket(RocketState rocket)
        {
            int id;
            do
            {
                id = idGenerator.Next();
            }
            while (rockets.ContainsKey(id));
            rockets.Add(id, rocket);
            return id;
        }

        public int AddPart(PartState part)
        {
            int id;
            do
            {
                id = idGenerator.Next();
            }
            while (parts.ContainsKey(id));
            parts.Add(id, part);
            return id;
        }
    }
}