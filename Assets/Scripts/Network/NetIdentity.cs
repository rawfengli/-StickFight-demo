using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;



public enum Visibility 
{ 
    Default, 
    ForceHidden, 
    ForceShown 
}
[DefaultExecutionOrder(-1)]
public class NetIdentity : MonoBehaviour
{
    public bool spawnOnServerOnly;

    //这两个用于区分server和client的逻辑
    //identity是一个在客户端生成的对象
    [HideInInspector]
    public bool isClient;

    [HideInInspector]
    //identity是一个在服务端生成的对象
    public bool isServer;

    [HideInInspector] public bool isOwned;//client，该identity是否被当前client所持有
    public NetConnectionToServer connectionToServer;

    public NetConnectionToClient connectionToClient;

    public Visibility visibility;

    //这个是给服务器记录使用的,not for client
    //表示这个identity被哪些客户端连接所感知
    public readonly Dictionary<int, NetConnectionToClient> observers = new();

    [HideInInspector] public double lastUnreliableStateTime;

    [HideInInspector] public byte lastUnreliableBaselineSent;
    [HideInInspector] public byte lastUnreliableBaselineReceived;

    [HideInInspector]
    public NetBehaviour[] behaviours;
    
    public const int MAX_NET_BEHAVIOURS = 64;



    #region ID
    [HideInInspector]
    public uint netID;

    [HideInInspector]
    public ulong sceneID;

    private static readonly Dictionary<ulong, NetIdentity> sceneIDs = new();

    [HideInInspector][SerializeField] 
    private uint _assetID;

    public string selfAssetName;
    public uint assetID
    {
        get
        {
#if UNITY_EDITOR
            if (_assetID == 0)
                SetID();
#endif
            return _assetID;
        }
        set
        {
            if (value == 0)
            {
                Debug.LogError($"Can not set AssetId to empty guid on NetworkIdentity '{name}'");
                return;
            }
            _assetID = value;
        }
    }

    public const uint COMMON_ID = 1U << 8;
    private static uint nextNetworkID = COMMON_ID;
    public static void ResetNextNetworkID()
        => nextNetworkID = COMMON_ID;
    public static uint GetNextNetworkID()
    {
        uint id = nextNetworkID;
        nextNetworkID++;
        return id;  
    }
    public void SetNetManagerID(NetManagerType type, bool onServer)
    {
        sceneID = (uint)type;
        netID = (uint)type;

        if (onServer)
            NetServer.RegisterNetManager(this, type);
        else
            NetClient.RegisterNetManager(this, type);
    }
    public void RemoveNetManagerID(NetManagerType type, bool onServer)
    {
        sceneID = (uint)type;
        netID = (uint)type;

        if (onServer)
            NetServer.UnregisterNetManager(this, type);
        else
            NetClient.UnregisterNetManager(this, type);
    }
#if UNITY_EDITOR
    private void SetAssetID(GameObject prefab)
    {
        SetAssetID(AssetDatabase.GetAssetPath(prefab));
    }
    private void SetAssetID(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath) == false)
        {
            //如果不调用，有些情况下AssetID不会被写入磁盘，在editor里看上去没有问题，但是在build之后会存在隐患
            Undo.RecordObject(this, "Assigned AssetID");

            assetPath.GetHashCode();
            if (string.IsNullOrWhiteSpace(selfAssetName))
                assetID = (uint)assetPath.GetHashCode();

            else
                assetID = (uint)selfAssetName.GetHashCode();
        }
    }

    private void SetSceneID()
    {
        if (Application.isPlaying)
            return;

        bool otherExist = false;
        if (sceneIDs.TryGetValue(sceneID, out NetIdentity otherIdentity))
        {
            if (otherIdentity != null && otherIdentity != this)
                otherExist = false;
        }

        if (sceneID == 0 || otherExist)
        {
            sceneID = 0;
            Undo.RecordObject(this, "Generated SceneID");

            uint randomID;

            while (true)
            {
                randomID = Utils.GetRandomUInt();
                if(randomID >= COMMON_ID)
                    break;

            }

            if (sceneIDs.TryGetValue(randomID, out NetIdentity existing) && existing != null)
            {
                Debug.LogError("Generated duplicate random sceneID, this is accidental, please try again");
                return;
            }
            sceneID = randomID;
        }

        sceneIDs[sceneID] = this;
    }

    private void SetID()
    {
        if(Utils.IsPrefab(gameObject))
        {
            sceneID = 0;
            SetAssetID(gameObject);
            return;
        }

        //有待研究PrefabStageUtility
        if (UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            if(UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(gameObject))
            {
                sceneID = 0;
                string path = UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(gameObject).assetPath;
                SetAssetID(path);
            }
            return;
        }
        //是否这个gameobject源自于一个prefab
        if (Utils.IsSceneObjectWithPrefabParent(gameObject, out GameObject prefab))
        {
            SetSceneID();
            SetAssetID(prefab);
            return;
        }

        SetSceneID();
        if (EditorApplication.isPlaying == false)
        {
            _assetID = 0;
        }
    }

#endif

    #endregion
    private void InitializeNetworkBehaviours()
    {
        behaviours = GetComponentsInChildren<NetBehaviour>(true);
        if(behaviours == null || behaviours.Length > MAX_NET_BEHAVIOURS)
        {
            Debug.LogError("Invaild behaviours: array of behaviours is null or count of behaviours is exceed the limit");
        }
        for (int i = 0; i < behaviours.Length; ++i)
        {
            NetBehaviour component = behaviours[i];
            component.identity = this;
            component.netBehaviourID = (byte)i;
        }
    }
    private void Awake()
    {
        InitializeNetworkBehaviours();
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        //DisallowChildNetworkIdentities();
        SetID();
#endif

    }

    public void HandleRemoteCall(
        byte behaviourID, 
        ushort functionHash, 
        RemoteCallType callType, 
        NetReader reader, 
        NetConnectionToClient conn = null)
    {

        if (behaviourID >= behaviours.Length)
        {
            Debug.LogWarning($"Component [{behaviourID}] not found");
            return;
        }

        NetBehaviour behaviour = behaviours[behaviourID];
        if (RemoteProcedureCalls.Invoke(functionHash, callType, reader, behaviour, conn) == false)
        {
            Debug.LogError("Not find function or behaviour is not an instance of the expected type");
        }
    }

    public void BindConnectionToClient(NetConnectionToClient conn)
    {
        if (connectionToClient != null && conn != connectionToClient)
        {
            Debug.LogWarning("This object is already bound to a different connection.");
            return;
        }
        connectionToClient = conn;
    }

    public bool ValidateComponents()
    {
        if (behaviours == null)
        {
            Debug.LogError($"NetworkIdentity {name} has null NetworkBehaviours array!");
            return false;
        }
        if (behaviours.Length > MAX_NET_BEHAVIOURS)
        {
            Debug.LogError($"The count of NetworkBehaviour components on identity {name} exceeds the maximum allowed.");
            return false;
        }
        return true;
    }

    //添加观察这个identity的 客户端连接观察者
    public void AddObserver(NetConnectionToClient conn)
    {
        if (observers.ContainsKey(conn.connectionID))
            return;

        observers.Add(conn.connectionID, conn);
        conn.AddIdentityToObserving(this);
    }
    public void RemoveObserver(NetConnectionToClient conn)
    {
        if (!observers.ContainsKey(conn.connectionID))
            return;

        observers.Remove(conn.connectionID);
        conn.RemoveIdentityToObserving(this);
    }


    #region Serialize
    #region Dirty Mask
    public bool IsDirty(ulong mask, int bit)
    {
        return (mask & (1UL << bit)) != 0;
    }

    public void ClientDirtyMasks(
        out ulong dirtyMaskReliable,
        out ulong dirtyMaskUnreliableBaseline,
        out ulong dirtyMaskUnreliableDelta)
    {
        dirtyMaskReliable = 0;
        dirtyMaskUnreliableBaseline = 0;
        dirtyMaskUnreliableDelta = 0;

        for(int i = 0; i < behaviours.Length; i++)
        {
            NetBehaviour behaviour = behaviours[i];

            ulong bit = (1ul << i);

            if (isOwned && behaviour.syncDirection == SyncDirection.ClientToServer)
            {
                if (behaviour.syncChannel == SyncChannel.Reliable)
                {
                    if (behaviour.IsDirty()) 
                        dirtyMaskReliable |= bit;
                }
                else 
                {
                    if (behaviour.IsDirty_BitsOnly()) 
                        dirtyMaskUnreliableBaseline |= bit;

                    if (behaviour.IsDirty()) 
                        dirtyMaskUnreliableDelta |= bit;
                }
            }
        }

    }
    private void ServerDirtyMasks_Broadcast(
        out ulong ownerMaskReliable, out ulong observerMaskReliable,
        out ulong ownerMaskUnreliableBaseline, out ulong observerMaskUnreliableBaseline,
        out ulong ownerMaskUnreliableDelta, out ulong observerMaskUnreliableDelta)
    {
        ownerMaskReliable = 0;
        observerMaskReliable = 0;

        ownerMaskUnreliableBaseline = 0;
        observerMaskUnreliableBaseline = 0;

        ownerMaskUnreliableDelta = 0;
        observerMaskUnreliableDelta = 0;

        for (int i = 0; i < behaviours.Length; i++)
        {
            ulong bit = (1ul << i);

            if (behaviours[i].syncChannel == SyncChannel.Reliable)
            {
                bool dirty = behaviours[i].IsDirty();
                //只有从server到client的方向才可以被广播
                if (behaviours[i].syncDirection == SyncDirection.ServerToClient && dirty)
                    ownerMaskReliable |= bit;

                if (behaviours[i].syncMode == SyncMode.Observers && dirty)
                    observerMaskReliable |= bit;

            }
            else
            {
                bool dirty;

                //delta
                dirty = behaviours[i].IsDirty();

                if (behaviours[i].syncDirection == SyncDirection.ServerToClient && dirty)
                    ownerMaskUnreliableDelta |= bit;

                if (behaviours[i].syncMode == SyncMode.Observers && dirty)
                    observerMaskUnreliableDelta |= bit;

                //baseline
                dirty = behaviours[i].IsDirty_BitsOnly();

                if (behaviours[i].syncDirection == SyncDirection.ServerToClient && dirty)
                    ownerMaskUnreliableBaseline |= bit;

                if (behaviours[i].syncMode == SyncMode.Observers && dirty)
                    observerMaskUnreliableBaseline |= bit;

            }
        }


    }
    void ServerDirtyMasks_SpawnObject(out ulong ownerMask, out ulong observerMask)//全打上脏标记
    {
        ownerMask = 0;
        observerMask = 0;

        for (int i = 0; i < behaviours.Length; i++)
        {
            ulong dirtyBit = (1ul << i);
            ownerMask |= dirtyBit;//这里owned也在observer里存

            if (behaviours[i].syncMode == SyncMode.Observers)
            {
                observerMask |= dirtyBit;
            }
        }
    }

    #endregion
    public void SerializeServer_SpawnObject(NetWriterPooled ownerWriter, NetWriterPooled observersWriter)
    {
        if(ValidateComponents() == false)
            return;

        ServerDirtyMasks_SpawnObject(out ulong ownerMask, out ulong observerMask);

        if (ownerMask != 0) 
            Compression.CompressVarUInt(ownerWriter, ownerMask);
        if (observerMask != 0) 
            Compression.CompressVarUInt(observersWriter, observerMask);
        
        if(ownerMask != 0 || observerMask != 0)
        {
            for (int i = 0; i < behaviours.Length; i++)
            {
                bool ownerDirty = IsDirty(ownerMask, i);
                bool observerDirty = IsDirty(observerMask, i);

                if (ownerDirty || observerDirty)
                {
                    using (NetWriterPooled temp = NetWriterPool.Get())
                    {
                        behaviours[i].Serialize(temp, true);
                        ArraySegment<byte> segment = temp.ToArraySegment();
                        if (ownerDirty) 
                            ownerWriter.WriteBytes(segment.Array, segment.Offset, segment.Count);
                        if (observerDirty) 
                            observersWriter.WriteBytes(segment.Array, segment.Offset, segment.Count);
                    }
                }
            }
        }
    }


    int lastSerializationTick = 0;
    private void SerializeServer_Broadcast(
        NetWriter ownerWriterReliable,              NetWriter observersWriterReliable,
        NetWriter ownerWriterUnreliableBaseline,    NetWriter observersWriterUnreliableBaseline,
        NetWriter ownerWriterUnreliableDelta,       NetWriter observersWriterUnreliableDelta,
        bool unreliableBaseline)
    {
        ValidateComponents();

        ServerDirtyMasks_Broadcast(
            out ulong ownerMaskReliable, out ulong observerMaskReliable,
            out ulong ownerMaskUnreliableBaseline, out ulong observerMaskUnreliableBaseline,
            out ulong ownerMaskUnreliableDelta, out ulong observerMaskUnreliableDelta
        );

        if (ownerMaskReliable != 0) 
            Compression.CompressVarUInt(ownerWriterReliable, ownerMaskReliable);
        if (observerMaskReliable != 0) 
            Compression.CompressVarUInt(observersWriterReliable, observerMaskReliable);

        if (ownerMaskUnreliableBaseline != 0 && unreliableBaseline)
            Compression.CompressVarUInt(ownerWriterUnreliableBaseline, ownerMaskUnreliableBaseline);
        if (observerMaskUnreliableBaseline != 0 && unreliableBaseline)
            Compression.CompressVarUInt(observersWriterUnreliableBaseline, observerMaskUnreliableBaseline);

        if (ownerMaskUnreliableDelta != 0) 
            Compression.CompressVarUInt(ownerWriterUnreliableDelta, ownerMaskUnreliableDelta);
        if (observerMaskUnreliableDelta != 0) 
            Compression.CompressVarUInt(observersWriterUnreliableDelta, observerMaskUnreliableDelta);

        bool dirtyMaskReliable = (ownerMaskReliable | observerMaskReliable) != 0;
        bool dirtyMaskUnreliableBaseline = (ownerMaskUnreliableBaseline | observerMaskUnreliableBaseline) != 0;
        bool dirtyMaskUnreliableDelta = (ownerMaskUnreliableDelta | observerMaskUnreliableDelta) != 0;

        if(dirtyMaskReliable || dirtyMaskUnreliableBaseline || dirtyMaskUnreliableDelta)
        {
            for(int i = 0; i < behaviours.Length; i++)
            {
                bool ownerDirtyReliable = IsDirty(ownerMaskReliable, i);
                bool observersDirtyReliable = IsDirty(observerMaskReliable, i);

                bool ownerDirtyUnreliableBaseline = IsDirty(ownerMaskUnreliableBaseline, i);
                bool observersDirtyUnreliableBaseline = IsDirty(observerMaskUnreliableBaseline, i);

                bool ownerDirtyUnreliableDelta = IsDirty(ownerMaskUnreliableDelta, i);
                bool observersDirtyUnreliableDelta = IsDirty(observerMaskUnreliableDelta, i);

                if(ownerDirtyReliable || observersDirtyReliable)
                {
                    using (NetWriterPooled writer = NetWriterPool.Get())
                    {
                        behaviours[i].Serialize(writer, false);
                        ArraySegment<byte> data = writer.ToArraySegment();
                        //对于owner和observers他们的数据相同，但是处理逻辑可能不同
                        if (ownerDirtyReliable)
                            ownerWriterReliable.WriteBytes(data.Array, data.Offset, data.Count);
                        if (observersDirtyReliable) 
                            observersWriterReliable.WriteBytes(data.Array, data.Offset, data.Count);
                    }
                    behaviours[i].ClearAllDirtyMask();
                }

                if (ownerDirtyUnreliableDelta || observersDirtyUnreliableDelta)
                {
                    using (NetWriterPooled writer = NetWriterPool.Get())
                    {
                        behaviours[i].Serialize(writer, false);
                        ArraySegment<byte> data = writer.ToArraySegment();
                        //对于owner和observers他们的数据相同，但是处理逻辑可能不同
                        if (ownerDirtyUnreliableDelta)
                            ownerWriterUnreliableDelta.WriteBytes(data.Array, data.Offset, data.Count);
                        if (observersDirtyUnreliableDelta)
                            observersWriterUnreliableDelta.WriteBytes(data.Array, data.Offset, data.Count);
                        behaviours[i].lastSyncTime = NetTime.localTime;
                    }
                    behaviours[i].ClearAllDirtyMask();
                }

                if ((ownerDirtyUnreliableBaseline || observersDirtyUnreliableBaseline) && unreliableBaseline)
                {
                    using (NetWriterPooled writer = NetWriterPool.Get())
                    {
                        behaviours[i].Serialize(writer, true);
                        ArraySegment<byte> data = writer.ToArraySegment();
                        //对于owner和observers他们的数据相同，但是处理逻辑可能不同
                        if (ownerDirtyUnreliableBaseline) 
                            ownerWriterUnreliableBaseline.WriteBytes(data.Array, data.Offset, data.Count);
                        if (observersDirtyUnreliableBaseline) 
                            observersWriterUnreliableBaseline.WriteBytes(data.Array, data.Offset, data.Count);
                    }

                    behaviours[i].ClearAllDirtyMask();
                }
            }
        }
    }
    private NetWriter lastTickOwnerWriterReliable = new();
    private NetWriter lastTickObserversWriterReliable = new();
    private NetWriter lastTickOwnerWriterUnreliableBaseline = new();
    private NetWriter lastTickObserversWriterUnreliableBaseline = new();
    private NetWriter lastTickOwnerWriterUnreliableDelta = new();
    private NetWriter lastTickObserversWriterUnreliableDelta = new();

    public void SerializeServer(
        int tick, bool unreliableBaselineElapsed,
        out NetWriter ownerWriterReliable, out NetWriter observersWriterReliable,
        out NetWriter ownerWriterUnreliableBaseline, out NetWriter observersWriterUnreliableBaseline,
        out NetWriter ownerWriterUnreliableDelta, out NetWriter observersWriterUnreliableDelta)
    {
        ownerWriterReliable = lastTickOwnerWriterReliable;
        observersWriterReliable = lastTickObserversWriterReliable;
        ownerWriterUnreliableBaseline = lastTickOwnerWriterUnreliableBaseline;
        observersWriterUnreliableBaseline = lastTickObserversWriterUnreliableBaseline;
        ownerWriterUnreliableDelta = lastTickOwnerWriterUnreliableDelta;
        observersWriterUnreliableDelta = lastTickObserversWriterUnreliableDelta;

        if (lastSerializationTick == tick)
            return;

        lastSerializationTick = tick;

        ResetWriters();
        SerializeServer_Broadcast(
            ownerWriterReliable,            observersWriterReliable,
            ownerWriterUnreliableBaseline,  observersWriterUnreliableBaseline,
            ownerWriterUnreliableDelta,     observersWriterUnreliableDelta,
            unreliableBaselineElapsed);


    }


    public bool DeserializeServer(NetReader reader, bool initialState)
    {
        ValidateComponents();

        ulong mask = Compression.DecompressVarUInt(reader);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (IsDirty(mask, i))
            {
                if (behaviours[i].syncDirection == SyncDirection.ClientToServer)
                {
                    if (behaviours[i].Deserialize(reader, initialState) == false)
                    {
                        return false;
                    }

                    //later Broadcast to all clients(observers)
                    behaviours[i].SetAllDirty();
                }
            }
        }
        return true;
    }

    public void SerializeClient(
        NetWriter writerReliable,
        NetWriter writerUnreliableBaseline,
        NetWriter writerUnreliableDelta,
        bool unreliableBaseline)
    {
        ValidateComponents();

        ClientDirtyMasks(
            out ulong dirtyMaskReliable, 
            out ulong dirtyMaskUnreliableBaseline, 
            out ulong dirtyMaskUnreliableDelta);

        if (dirtyMaskReliable != 0) 
            Compression.CompressVarUInt(writerReliable, dirtyMaskReliable);
        if (dirtyMaskUnreliableDelta != 0) 
            Compression.CompressVarUInt(writerUnreliableDelta, dirtyMaskUnreliableDelta);
        if (dirtyMaskUnreliableBaseline != 0) 
            Compression.CompressVarUInt(writerUnreliableBaseline, dirtyMaskUnreliableBaseline);
        
        if (dirtyMaskReliable != 0 || 
            dirtyMaskUnreliableDelta != 0 || 
            dirtyMaskUnreliableBaseline != 0)
        {
            for (int i = 0; i < behaviours.Length; i++)
            {
                if(IsDirty(dirtyMaskReliable, i))
                {
                    behaviours[i].Serialize(writerReliable, false);
                    behaviours[i].ClearAllDirtyMask();
                }

                if (IsDirty(dirtyMaskUnreliableDelta, i))
                {
                    behaviours[i].Serialize(writerUnreliableDelta, false);
                    behaviours[i].lastSyncTime = NetTime.localTime;
                }

                if (unreliableBaseline && IsDirty(dirtyMaskUnreliableBaseline, i))
                {
                    behaviours[i].Serialize(writerUnreliableBaseline, false);
                    behaviours[i].ClearAllDirtyMask();
                }

            }
        }


    }
    public void DeserializeClient(NetReader reader, bool initialState)
    {
        if (ValidateComponents() == false)
            return;

        ulong mask = Compression.DecompressVarUInt(reader);

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (IsDirty(mask, i))
            {
                NetBehaviour comp = behaviours[i];
                comp.Deserialize(reader, initialState);
            }
        }
    }



    #endregion

    #region Callbacks

    public void OnStartServer()
    {
        foreach (NetBehaviour comp in behaviours)
        {
            try
            {
                comp.OnStartServer();
            }
            catch (Exception e)
            {
                Debug.LogException(e, comp);
            }
        }
    }
    public void OnStopServer()
    {
        foreach (NetBehaviour comp in behaviours)
        {
            try
            {
                comp.OnStopServer();
            }
            catch (Exception e)
            {
                Debug.LogException(e, comp);
            }
        }
    }

    public virtual void OnStartClient()
    {
        foreach (NetBehaviour comp in behaviours)
        {
            try
            {
                comp.OnStartClient();
            }
            catch (Exception e)
            {
                Debug.LogException(e, comp);
            }
        }
    }

    public virtual void OnStopClient()
    {
        foreach (NetBehaviour comp in behaviours)
        {
            try
            {
                comp.OnStopClient();
            }
            catch (Exception e)
            {
                Debug.LogException(e, comp);
            }
        }
    }
    #endregion

    #region Reset
    public void ResetWriters()
    {
        lastTickOwnerWriterReliable.Position = 0;
        lastTickObserversWriterReliable.Position = 0;
        lastTickOwnerWriterUnreliableBaseline.Position = 0;
        lastTickObserversWriterUnreliableBaseline.Position = 0;
        lastTickOwnerWriterUnreliableDelta.Position = 0;
        lastTickObserversWriterUnreliableDelta.Position = 0;
    }
    public void ResetState()
    {
        connectionToServer = null;
        connectionToClient = null;

        lastUnreliableBaselineReceived = 0;
        lastUnreliableBaselineSent = 0;
        isOwned = false;

        foreach (NetBehaviour behaviour in behaviours)
        {
            behaviour.ResetState();
        }
    }

    #endregion
}
