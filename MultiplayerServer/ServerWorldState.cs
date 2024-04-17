

using System;
using System.Collections.Generic;
using System.Diagnostics;
using MultiplayerSFS.Common;
using MultiplayerSFS.Common.Packets;
using SFS.WorldBase;

namespace MultiplayerSFS.Server
{
    public class ServerWorldState
    {
        readonly Random idGenerator = new Random();

        public double worldTime;
        readonly Stopwatch worldTimeUpdate = new Stopwatch();

        public Difficulty.DifficultyType difficulty;
        public Dictionary<int, RocketState> rockets;
        public Dictionary<int, PartState> parts;

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

            rockets = new Dictionary<int, RocketState>();
            parts = new Dictionary<int, PartState>();
        }

        public ServerWorldState(string savePath)
        {
            // TODO: Actually load data from save.

            throw new NotImplementedException();
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
            while (!rockets.ContainsKey(id));
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
            while (!parts.ContainsKey(id));
            parts.Add(id, part);
            return id;
        }
    }
}