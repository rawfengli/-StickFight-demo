using System;
using UnityEngine;

public interface INetMessage { }
public struct TimeSnapshotMessage : INetMessage { }
public struct ReadyMessage : INetMessage { }
public struct AddPlayerMessage : INetMessage { }
 
public struct ObjectSpawnStartedMessage : INetMessage { }

public struct ObjectSpawnMessage : INetMessage
{
    public uint netID;
    public ulong sceneID;
    public uint assetID;
    public bool isOwner;

    public Vector3 position;
    public Quaternion rotation;
    public Vector3 scale;
    public ArraySegment<byte> payload;
}

public struct ObjectSpawnFinishedMessage : INetMessage { }

public struct ObjectUnspawnMessage : INetMessage
{
    public uint netID;
}
public struct ObjectHideMessage : INetMessage
{
    public uint netID;
}
public struct PingMessage : INetMessage
{
    public double localTime;
    public double predictedTime;
}

public struct PongMessage : INetMessage
{
    public double localTime;

    public double predictionError;
    public double rawError;
}
public struct CommandToServerMessage : INetMessage
{
    public uint netID;
    public byte netBehaviourID;
    public ushort functionHash;

    public ArraySegment<byte> payload;
}
public struct CommandToClientMessage : INetMessage
{
    public uint netID;
    public byte netBehaviourID;
    public ushort functionHash;

    public ArraySegment<byte> payload;
}

//SyncBehaviourMessage 不用将BehaviourID 写入，因为这里写入的所有的Behaviour Sync Data
//[dirty mask][Behaviour_0 Sync Data][Behaviour_2 Sync Data]... as dirty mask:101...
public struct SyncBehavioursMessage : INetMessage
{
    public uint netID;
    public ArraySegment<byte> payload;
}
public struct SyncBehavioursUnreliableBaselineMessage : INetMessage
{
    public byte baselineTick;
    public uint netID;
    public ArraySegment<byte> payload;
}
public struct SyncBehavioursUnreliableDeltaMessage : INetMessage
{
    public byte baselineTick;
    public uint netID;
    public ArraySegment<byte> payload;
}

public delegate void NetworkMessageDelegate(NetConnection conn, NetReader reader, int channelID);
public static class NetMessages
{
    public delegate void OnNetMessageDelegate(NetConnection conn, NetReader reader, int channel);

    public const int HASH_HEADER_SIZE = sizeof(ushort);

    public static bool registered = false;
    public static void RegisterAllMessage()
    {
        if (registered)
            return;
        Utils.RegisterDataParseDelegate<TimeSnapshotMessage>(
            (writer, msg) => { },
            (reader) => new TimeSnapshotMessage()
        );

        Utils.RegisterDataParseDelegate<ReadyMessage>(
            (writer, msg) => { },
            (reader) => new ReadyMessage()
        );

        Utils.RegisterDataParseDelegate<AddPlayerMessage>(
            (writer, msg) => { },
            (reader) => new AddPlayerMessage()
        );

        Utils.RegisterDataParseDelegate<ObjectSpawnStartedMessage>(
            (writer, msg) => { },
            (reader) => new ObjectSpawnStartedMessage()
        );

        Utils.RegisterDataParseDelegate<ObjectSpawnMessage>(
            (writer, msg) =>
            {
                writer.Write<uint>(msg.netID);
                writer.Write<ulong>(msg.sceneID);
                writer.Write<uint>(msg.assetID);
                writer.Write<bool>(msg.isOwner);

                writer.Write<Vector3>(msg.position);
                writer.Write<Quaternion>(msg.rotation);
                writer.Write<Vector3>(msg.scale);

                //ArraySegment也是struct类型
                writer.Write<ArraySegment<byte>>(msg.payload);

            },
            (reader) =>
            {
                ObjectSpawnMessage msg = new();

                msg.netID = reader.Read<uint>();
                msg.sceneID = reader.Read<ulong>();
                msg.assetID = reader.Read<uint>();
                msg.isOwner = reader.Read<bool>();

                msg.position = reader.Read<Vector3>();
                msg.rotation = reader.Read<Quaternion>();
                msg.scale = reader.Read<Vector3>();

                msg.payload = reader.Read<ArraySegment<byte>>();
                return msg;
            }
        );

        Utils.RegisterDataParseDelegate<ObjectSpawnFinishedMessage>(
            (writer, msg) => { },
            (reader) => new ObjectSpawnFinishedMessage()
        );


        Utils.RegisterDataParseDelegate<ObjectUnspawnMessage>(
            (writer, msg) => 
            {
                writer.Write<uint>(msg.netID);
            },
            (reader) =>
            {
                ObjectUnspawnMessage msg = new();
                msg.netID = reader.Read<uint>();
                return msg;
            }
        );

        Utils.RegisterDataParseDelegate<ObjectHideMessage>(
            (writer, msg) =>
            {
                writer.Write<uint>(msg.netID);
            },
            (reader) =>
            {
                ObjectHideMessage msg = new();
                msg.netID = reader.Read<uint>();
                return msg;
            }
        );

        Utils.RegisterDataParseDelegate<PingMessage>(
            (writer, msg) =>
            {
                writer.Write<double>(msg.localTime);
                writer.Write<double>(msg.predictedTime);
            },
            (reader) => 
            {
                PingMessage msg = new();
                msg.localTime = reader.Read<double>();
                msg.predictedTime = reader.Read<double>();
                return msg;
            }
        );

        Utils.RegisterDataParseDelegate<PongMessage>(
            (writer, msg) =>
            {
                writer.Write<double>(msg.localTime);
                writer.Write<double>(msg.predictionError);
                writer.Write<double>(msg.rawError);
            },
            (reader) =>
            {
                PongMessage msg = new();
                msg.localTime = reader.Read<double>();
                msg.predictionError = reader.Read<double>();
                msg.rawError = reader.Read<double>();
                return msg;
            }
        );

        Utils.RegisterDataParseDelegate<CommandToServerMessage>(
            (writer, msg) =>
            {
                writer.Write<uint>(msg.netID);
                writer.Write<byte>(msg.netBehaviourID);
                writer.Write<ushort>(msg.functionHash);

                writer.Write<ArraySegment<byte>>(msg.payload);
            },
            (reader) =>
            {
                CommandToServerMessage msg = new();
                msg.netID = reader.Read<uint>();
                msg.netBehaviourID = reader.Read<byte>();
                msg.functionHash = reader.Read<ushort>();

                msg.payload = reader.Read<ArraySegment<byte>>();
                return msg;
            }
        );

        Utils.RegisterDataParseDelegate<CommandToClientMessage>(
            (writer, msg) =>
            {
                writer.Write<uint>(msg.netID);
                writer.Write<byte>(msg.netBehaviourID);
                writer.Write<ushort>(msg.functionHash);

                writer.Write<ArraySegment<byte>>(msg.payload);
            },
            (reader) =>
            {
                CommandToClientMessage msg = new();
                msg.netID = reader.Read<uint>();
                msg.netBehaviourID = reader.Read<byte>();
                msg.functionHash = reader.Read<ushort>();

                msg.payload = reader.Read<ArraySegment<byte>>();
                return msg;
            }
        );


        Utils.RegisterDataParseDelegate<SyncBehavioursMessage>(
            (writer, msg) =>
            {
                writer.Write(msg.netID);
                writer.Write<ArraySegment<byte>>(msg.payload);
            },
            (reader) =>
            {
                SyncBehavioursMessage msg = new();
                msg.netID = reader.Read<uint>();
                msg.payload = reader.Read<ArraySegment<byte>>();

                return msg;

            }
        );

        Utils.RegisterDataParseDelegate<SyncBehavioursUnreliableBaselineMessage>(
            (writer, msg) =>
            {
                writer.Write(msg.baselineTick);
                writer.Write(msg.netID);
                writer.Write<ArraySegment<byte>>(msg.payload);
            },
            (reader) =>
            {
                SyncBehavioursUnreliableBaselineMessage msg = new();
                msg.baselineTick = reader.Read<byte>();
                msg.netID = reader.Read<uint>();
                msg.payload = reader.Read<ArraySegment<byte>>();

                return msg;

            }
        );
        Utils.RegisterDataParseDelegate<SyncBehavioursUnreliableDeltaMessage>(
            (writer, msg) =>
            {
                writer.Write(msg.baselineTick);
                writer.Write(msg.netID);
                writer.Write<ArraySegment<byte>>(msg.payload);
            },
            (reader) =>
            {
                SyncBehavioursUnreliableDeltaMessage msg = new();
                msg.baselineTick = reader.Read<byte>();
                msg.netID = reader.Read<uint>();
                msg.payload = reader.Read<ArraySegment<byte>>();

                return msg;

            }
        );
    }

    #region msg header pack
    public static int MaxContentSize(int channel)
    {
        int transportMax = Transport.instance.GetMaxPacketSize(channel);
        return transportMax - HASH_HEADER_SIZE - Batcher.MaxMessageOverhead(transportMax);
    }
    public static int MaxMessageSize(int channel)
        => MaxContentSize(channel) + HASH_HEADER_SIZE;

    public static ushort GetMessageHash<T>()
        where T : struct, INetMessage
    {
        string fullName = typeof(T).FullName;
        int hash = Utils.GetStableHashCode(fullName);
        return (ushort)((hash >> 16) ^ hash);
    }

    public static void Pack<T>(T msg, NetWriter writer)
        where T : struct, INetMessage
    {
        NetWriter.WriteUShort(writer, GetMessageHash<T>());//显示调用应该更快一些
        writer.Write(msg);//得为每条信息msg写write
    }
    public static bool UnpackID(NetReader reader, out ushort messageID)
    {
        try
        {
            messageID = reader.Read<ushort>();
            return true;
        }
        catch (System.IO.EndOfStreamException)
        {
            messageID = 0;
            return false;
        }
    }
    #endregion

    // msg register

    //主要是为了做合法性校验和读msg
    public static NetworkMessageDelegate WrapHandler<T, C>(Action<C, T, int> handler, bool requireAuthentication, bool exceptionsDisconnect)
        where T : struct, INetMessage
        where C : NetConnection
        //T: NetMessage, C:connection, int:channel
    {
        return (connection, reader, channelID) =>
        {
            T message = default;

            try
            {
                //---------------------------------------------------
                requireAuthentication = false;//之后再改
                if (requireAuthentication && connection.isAuthenticated == false)
                {
                    Debug.LogWarning($"Received message {typeof(T)} that required authentication, but the user has not authenticated yet");
                    connection.Disconnect();
                    return;
                }
                message = reader.Read<T>();
            }
            catch (Exception exception)
            {
                // should we disconnect on exceptions?
                if (exceptionsDisconnect)
                {
                    Debug.LogError($"because reading a message of type {typeof(T)} caused an Exception. Reason: {exception}, Disconnect");
                    connection.Disconnect();
                    return;
                }
                else
                {
                    Debug.LogError($"because reading a message of type {typeof(T)} caused an Exception. Reason: {exception}");
                    return;
                }
            }

            try
            {
                handler((C)connection, message, channelID);
            }
            catch(Exception e)
            {
                if (exceptionsDisconnect)
                {
                    Debug.LogError($"handling a message of type {typeof(T)} caused an Exception. Reason: {e}, Disconnect");
                    connection.Disconnect();
                }
                // otherwise log it but allow the connection to keep playing
                else
                {
                    Debug.LogError($"handling a message of type {typeof(T)} caused an Exception. Reason: {e}");
                }
            }
        };
    }

    public static NetworkMessageDelegate WrapHandler<T, C>(Action<C, T> handler, bool requireAuthentication, bool exceptionsDisconnect)
        where T : struct, INetMessage
        where C : NetConnection
    {
        void Wrapped(C conn, T msg, int _)//'_'补位
            => handler(conn, msg);
        return WrapHandler((Action<C, T, int>)Wrapped, requireAuthentication, exceptionsDisconnect);
    }
}
