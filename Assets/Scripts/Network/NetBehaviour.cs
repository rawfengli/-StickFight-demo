using System;
using UnityEngine;
using UnityEngine.UI;

public enum SyncChannel
{ 
    Reliable, 
    Hybrid 
}
public enum SyncMode
{
    Observers,
    Owner
}
public enum SyncDirection
{
    ServerToClient, 
    ClientToServer 
}

public abstract class NetBehaviour : MonoBehaviour
{

    [HideInInspector][SerializeField] 
    public NetIdentity identity;

    [Header("Sync Setting")]
    public SyncMode syncMode = SyncMode.Observers;
    public SyncDirection syncDirection = SyncDirection.ServerToClient;
    public SyncChannel syncChannel = SyncChannel.Hybrid;
    public bool isServer => identity.isServer;
    public bool isClient => identity.isClient;
    public bool isOwned => identity.isOwned;
    public uint netID => identity.netID;

    public bool authority
    {
        get
        {
            if(isServer)
                return syncDirection == SyncDirection.ServerToClient;
            else
                return syncDirection == SyncDirection.ClientToServer && isOwned;
        }
    }
    //sync
    [HideInInspector] 
    public float syncInterval = 0;
    [NonSerialized]
    public double lastSyncTime;
    
    private ulong syncVarDirtyBits;

    public NetConnectionToClient connectionToClient => identity.connectionToClient;
    
    [HideInInspector]
    public byte netBehaviourID;
    protected void SendCommandToServer<T>(Type componentType, string functionName, T cmdData, int channelID, bool requiresAuthority = true)
        where T : struct, INetTransportData<T>
    {
        requiresAuthority = false;//later change

        string functionFullName = Utils.FullFunctionName(componentType, functionName);
        ushort functionHash = Utils.GetFunctionHashCode(functionFullName);

        if (NetClient.ready == false)
        {
            Debug.LogError("Failed to send command to server: Client is not ready");
            return;
        }
        
        if(requiresAuthority && isOwned == false)
        {
            Debug.LogWarning("Failed to send command to server:Send Command on a NetBehaviour is not owned without authority");
            return;
        }

        if (NetClient.connection == null)
        {
            Debug.LogError("Failed to send command to server:connection is null");
            return;
        }
        using (NetWriterPooled writer = NetWriterPool.Get())
        {
            NetTransportData.Write(writer, cmdData);
            CommandToServerMessage message = new CommandToServerMessage
            {
                netID = netID,
                netBehaviourID = netBehaviourID,
                functionHash = functionHash,
                payload = writer.ToArraySegment(),
            };
            NetClient.Send(message, channelID);
        }
    }
    protected void SendCommandToAllClient<T>(Type componentType, string functionName, T cmdData, int channelID, bool includeOwner)
        where T : struct, INetTransportData<T>
    {
        string functionFullName = Utils.FullFunctionName(componentType, functionName);
        ushort functionHash = Utils.GetFunctionHashCode(functionFullName);

        if (NetServer.active == false)
        {
            Debug.LogError("Failed to send command to all client: Server is not ready");
            return;
        }
        if (isServer == false)
        {
            Debug.LogWarning("Failed to send command to all client: NetBehaviour is un-spawned object");
            return;
        }
        using (NetWriterPooled writer = NetWriterPool.Get())
        {
            NetTransportData.Write(writer, cmdData);

            CommandToClientMessage message = new CommandToClientMessage
            {
                netID = this.netID,
                netBehaviourID = netBehaviourID,
                functionHash = functionHash,

                payload = writer.ToArraySegment()
            };
            if (identity.observers == null || identity.observers.Count == 0)
                return;

            using (NetWriterPooled serialized = NetWriterPool.Get())
            {
                serialized.Write(message);

                foreach (NetConnectionToClient conn in identity.observers.Values)
                {
                    bool isOwner = conn == identity.connectionToClient;
                    if ((!isOwner || includeOwner) && conn.ready)
                    {
                        conn.Send(message, channelID);
                    }
                }
            }
        }
    }
    protected void SendCommandToTargetClient<T>(NetConnectionToClient conn, Type componentType, string functionName, T cmdData, int channelID)
        where T : struct, INetTransportData<T>
    {
        string functionFullName = Utils.FullFunctionName(componentType, functionName);

        ushort functionHash = Utils.GetFunctionHashCode(functionFullName);

        if (NetServer.active == false)
        {
            Debug.LogError($"Failed to send command to client(Connection:{conn.connectionID}): Server is not ready");
            return;
        }
        if (isServer == false)
        {
            Debug.LogWarning($"Failed to send command to client(Connection:{conn.connectionID}): NetBehaviour is un-spawned object");
            return;
        }
        if (conn is null)
        {
            Debug.LogError("Failed to send command to client:connection is null");
            return;
        }
        using (NetWriterPooled writer = NetWriterPool.Get())
        {
            NetTransportData.Write(writer, cmdData);
            CommandToClientMessage message = new CommandToClientMessage
            {
                netID = this.netID,
                netBehaviourID = netBehaviourID,
                functionHash = functionHash,

                payload = writer.ToArraySegment()
            };
            conn.Send(message, channelID);
        }
    }

    #region Serialize And Deserialize Methods
    #region Dirty Mask
    public bool IsDirty()
        => syncVarDirtyBits != 0UL && NetTime.localTime - lastSyncTime >= syncInterval;
    public bool IsDirty_BitsOnly()
        => syncVarDirtyBits != 0UL;
    public void ClearAllDirtyMask()
    {
        lastSyncTime = NetTime.localTime;
        syncVarDirtyBits = 0L;
    }
    public void SetDirty(ulong dirtyBit)
    {
        syncVarDirtyBits |= dirtyBit;
    }
    public void SetAllDirty()
        => SetDirty(ulong.MaxValue);

    #endregion
    public void Serialize(NetWriter writer, bool init)
    {
        byte safetyHash = 0;
        //这里的头部不是信息的头部，是别的已经序列化的信息的结尾位置
        int headerEndPosition = writer.Position;
        writer.WriteByte(0);
        int contentStartPosition = writer.Position;

        try
        {
            OnSerialize(writer, init);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Exception during serialization of {netID} identity And {netBehaviourID} Behaviour: {ex}");
        }

        int contentEndPosition = writer.Position;
        int contentLength = contentEndPosition - contentStartPosition;

        safetyHash = (byte)(contentLength & 0xFF);

        writer.Position = headerEndPosition;
        writer.WriteByte(safetyHash);
        writer.Position = contentEndPosition;
    }
    public bool Deserialize(NetReader reader, bool init)
    {
        bool result = true;
        byte safetyHash = reader.ReadByte();
        int contentStartPosition = reader.Position;
        try
        {
            OnDeserialize(reader, init);
        }
        catch (Exception ex)
        {
            Debug.Log("Here False");
            Debug.LogError($"Exception during deserialization of {netID} identity And {netBehaviourID} Behaviour: {ex}");
            result = false;
        }
        int contentEndPosition = reader.Position;
        int length = contentEndPosition - contentStartPosition;
        byte checkHash = (byte)(length & 0xFF);
        if(checkHash != safetyHash)
        {
            Debug.Log(safetyHash + " " + checkHash);
            Debug.LogWarning($"{name} (netId={netID}):there is an imbalance in the received and sent information during deserialization");
            result = false;
        }

        return result;
    }

    #endregion

    #region callbacks

    //注意，这里的init同时也是基线序列化标志
    public virtual void OnSerialize(NetWriter writer, bool init) { }
    public virtual void OnDeserialize(NetReader reader, bool init) { }
    public virtual void OnStartServer() { }
    public virtual void OnStopServer() { }
    public virtual void OnStartClient() { }
    public virtual void OnStopClient() { }

    #endregion
    #region Reset

    public virtual void ResetState() { }

    #endregion
}
