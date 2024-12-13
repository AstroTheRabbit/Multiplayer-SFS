using System;
using System.Collections.Generic;
using Lidgren.Network;
using SFS.Parts.Modules;
using SFS.World;
using UnityEngine;

namespace MultiplayerSFS.Common
{
    public interface INetData
    {
        void Serialize(NetOutgoingMessage msg);
        void Deserialize(NetIncomingMessage msg);
    }

    public static class NetDataExtensions
    {
        public static void Write(this NetOutgoingMessage msg, INetData data)
        {
            data.Serialize(msg);
        }
        public static D Read<D>(this NetIncomingMessage msg) where D: INetData, new()
        {
            D data = new D();
            data.Deserialize(msg);
            return data;
        }

        public static void WriteCollection<T>(this NetOutgoingMessage msg, ICollection<T> collection, Action<T> writeFunc)
        {
            msg.Write(collection.Count);
            foreach (T item in collection)
            {
                writeFunc(item);
            }
        }
        public static C ReadCollection<C, T>(this NetIncomingMessage msg, Func<int, C> initFunc, Func<T> readFunc) where C : ICollection<T>
        {
            int count = msg.ReadInt32();
            C collection = initFunc(count);
            for (int i = 0; i < count; i++)
            {
                collection.Add(readFunc());
            }
            return collection;
        }
    }

    public static class ExistingTypeExtensions
    {
        public static void Write(this NetOutgoingMessage msg, Double2 double2)
        {
            msg.Write(double2.x);
            msg.Write(double2.y);
        }
        public static Double2 ReadDouble2(this NetIncomingMessage msg)
        {
            return new Double2
            (
                msg.ReadDouble(),
                msg.ReadDouble()
            );
        }

        public static void Write(this NetOutgoingMessage msg, Vector2 vector2)
        {
            msg.Write(vector2.x);
            msg.Write(vector2.y);
        }
        public static Vector2 ReadVector2(this NetIncomingMessage msg)
        {
            return new Vector2
            (
                msg.ReadFloat(),
                msg.ReadFloat()
            );
        }

        public static void Write(this NetOutgoingMessage msg, WorldSave.LocationData location)
        {
            msg.Write(location.address);
            msg.Write(location.position);
            msg.Write(location.velocity);
        }
        public static WorldSave.LocationData ReadLocation(this NetIncomingMessage msg)
        {
            return new WorldSave.LocationData()
            {
                address = msg.ReadString(),
                position = msg.ReadDouble2(),
                velocity = msg.ReadDouble2(),
            };
        }

        public static void Write(this NetOutgoingMessage msg, Orientation orientation)
        {
            msg.Write(orientation.x);
            msg.Write(orientation.y);
            msg.Write(orientation.z);
        }
        public static Orientation ReadOrientation(this NetIncomingMessage msg)
        {
            return new Orientation
            (
                msg.ReadFloat(),
                msg.ReadFloat(),
                msg.ReadFloat()
            );
        }

        public static void Write(this NetOutgoingMessage msg, BurnMark.BurnSave burnSave)
        {
            msg.Write(burnSave == null);
            if (burnSave == null)
                return;
                
            msg.Write(burnSave.angle);
            msg.Write(burnSave.intensity);
            msg.Write(burnSave.x);
            msg.Write(burnSave.top);
            msg.Write(burnSave.bottom);
        }
        public static BurnMark.BurnSave ReadBurnSave(this NetIncomingMessage msg)
        {
            if (msg.ReadBoolean())
                return null;
            
            return new BurnMark.BurnSave()
            {
                angle = msg.ReadFloat(),
                intensity = msg.ReadFloat(),
                x = msg.ReadFloat(),
                top = msg.ReadString(),
                bottom = msg.ReadString(),
            };
        }
    }
}