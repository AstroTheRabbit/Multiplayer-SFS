using System;
using System.Numerics;
using System.Collections.Generic;
using Lidgren.Network;
using SFS.Parts.Modules;
using MultiplayerSFS.Common.Packets;

namespace MultiplayerSFS.Common
{
    public class RocketState : IPacket
    {
        public string name;
        public RocketPositionState position;
        public bool throttleOn;
        public float throttlePercent;
        public bool RCS;
        public List<int> parts;
        public List<JointState> joints;
        public List<StageState> stages;

        public void Deserialize(NetIncomingMessage msg)
        {
            name = msg.ReadString();
            position = new RocketPositionState(); position.Deserialize(msg);
            throttleOn = msg.ReadBoolean();
            throttlePercent = msg.ReadFloat();
            RCS = msg.ReadBoolean();

            int partsLength = msg.ReadInt32();
            parts = new List<int>(partsLength);
            for (int i = 0; i < partsLength; i++)
            {
                parts.Add(msg.ReadInt32());
            }

            int jointsLength = msg.ReadInt32();
            joints = new List<JointState>(jointsLength);
            for (int i = 0; i < jointsLength; i++)
            {
                JointState joint = new JointState();
                joint.Deserialize(msg);
                joints.Add(joint);
            }

            int stagesLength = msg.ReadInt32();
            stages = new List<StageState>(stagesLength);
            for (int i = 0; i < stagesLength; i++)
            {
                StageState stage = new StageState();
                stage.Deserialize(msg);
                stages.Add(stage);
            }
        }

        public void Serialize(NetOutgoingMessage msg)
        {
            throw new NotImplementedException();
        }
    }

    public class RocketPositionState : IPacket
    {
        public string planet;
        public Double2 position;
        public Double2 velocity;
        public float rotation;
        public float angularVelocity;

        public void Deserialize(NetIncomingMessage msg)
        {
            planet = msg.ReadString();
            position = new Double2(msg.ReadDouble(), msg.ReadDouble());
            velocity = new Double2(msg.ReadDouble(), msg.ReadDouble());
            rotation = msg.ReadFloat();
            angularVelocity = msg.ReadFloat();
        }

        public void Serialize(NetOutgoingMessage msg)
        {
            msg.Write(planet);
            msg.Write(position.x); msg.Write(position.y);
            msg.Write(velocity.x); msg.Write(velocity.y);
            msg.Write(rotation);
            msg.Write(angularVelocity);
        }
    }

    public class PartState : IPacket
    {
        public string name;
        public Vector2 position;
        public Orientation orientation;
        public float temperature;
        public Dictionary<string, double> numberVariables;
        public Dictionary<string, bool> toggleVariables;
        public Dictionary<string, string> textVariables;

        public void Deserialize(NetIncomingMessage msg)
        {
            name = msg.ReadString();
            position = new Vector2(msg.ReadFloat(), msg.ReadFloat());
            orientation = new Orientation(msg.ReadFloat(), msg.ReadFloat(), msg.ReadFloat());
            temperature = msg.ReadFloat();

            int numberVariablesLength = msg.ReadInt32();
            numberVariables = new Dictionary<string, double>(numberVariablesLength);
            for (int i = 0; i < numberVariablesLength; i++)
            {
                numberVariables.Add(msg.ReadString(), msg.ReadDouble());
            }

            int toggleVariablesLength = msg.ReadInt32();
            toggleVariables = new Dictionary<string, bool>(toggleVariablesLength);
            for (int i = 0; i < toggleVariablesLength; i++)
            {
                toggleVariables.Add(msg.ReadString(), msg.ReadBoolean());
            }

            int textVariablesLength = msg.ReadInt32();
            textVariables = new Dictionary<string, string>(textVariablesLength);
            for (int i = 0; i < textVariablesLength; i++)
            {
                textVariables.Add(msg.ReadString(), msg.ReadString());
            }
        }

        public void Serialize(NetOutgoingMessage msg)
        {
            msg.Write(name);
            msg.Write(position.X); msg.Write(position.Y);
            msg.Write(orientation.x); msg.Write(orientation.y); msg.Write(orientation.z);
            msg.Write(temperature);

            msg.Write(numberVariables.Count);
            foreach (KeyValuePair<string, double> kvp in numberVariables)
            {
                msg.Write(kvp.Key);
                msg.Write(kvp.Value);
            }

            msg.Write(toggleVariables.Count);
            foreach (KeyValuePair<string, bool> kvp in toggleVariables)
            {
                msg.Write(kvp.Key);
                msg.Write(kvp.Value);
            }

            msg.Write(textVariables.Count);
            foreach (KeyValuePair<string, string> kvp in textVariables)
            {
                msg.Write(kvp.Key);
                msg.Write(kvp.Value);
            }
        }
    }

    public class JointState : IPacket
    {
        public int partID_A;
        public int partID_B;

        public void Deserialize(NetIncomingMessage msg)
        {
            partID_A = msg.ReadInt32();
            partID_B = msg.ReadInt32();
        }

        public void Serialize(NetOutgoingMessage msg)
        {
            msg.Write(partID_A);
            msg.Write(partID_B);
            
        }
    }

    public class StageState : IPacket
    {
        public int stageID;
        public List<int> parts;

        public void Deserialize(NetIncomingMessage msg)
        {
            stageID = msg.ReadInt32();
            
            int partsLength = msg.ReadInt32();
            parts = new List<int>(partsLength);
            for (int i = 0; i < partsLength; i++)
            {
                parts.Add(msg.ReadInt32());
            }
        }

        public void Serialize(NetOutgoingMessage msg)
        {
            msg.Write(stageID);
            
            msg.Write(parts.Count);
            foreach (int part in parts)
            {
                msg.Write(part);
            }
        }
    }
}