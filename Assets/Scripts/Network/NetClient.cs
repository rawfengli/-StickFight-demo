using JetBrains.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public enum ClientConnectState
{
    None,
    Connecting,
    Connected,
    Disconnecting,
    Disconnected,
}
public delegate GameObject ClientSpawnHandlerDelegate(ObjectSpawnMessage msg);
public delegate GameObject ClientUnspawnHandlerDelegate(GameObject go);
//NetClient disconnect的shutdown的事件注册栈:
//shutdown -> Netmanager.ClientDisconnect -> OnDisconnectedEvent -> Transport.Disconnect -> Invoke
//NetClient disconnect的shutdown的事件触发栈:
//主动调用disconnect的情况: NetManager -> disconnect -> connection.disconnect -> transport.disconect -> Invoke
//连接异常的disconnect的情况:transport.disconect -> Invoke
//值得注意的是，主动调用disconnect中间这些节点不能存在任何相较于连接异常的disconnect的情况下 多余的逻辑上修改
//可以存在一些无关紧要的状态变化
public static class NetClient
{
    public static Action OnConnectedEvent;
    public static Action OnDisconnectedEvent;
    public static Action<TransportError, string> OnErrorEvent;
    public static Action<Exception> OnTransportExceptionEvent;

    private static Unbatcher unbatcher = new Unbatcher();
    private static readonly Dictionary<ushort, NetworkMessageDelegate> handlers = new();

    private static bool isLoadingScene;

    public static NetConnectionToServer connection;

    public static readonly Dictionary<uint, NetIdentity> spawned = new();//存储已经被同步的物体

    #region Connection State
    public static ClientConnectState connectState;
    public static bool ready;//after client rec hello form server
    public static bool active => 
        connectState == ClientConnectState.Connecting || 
        connectState == ClientConnectState.Connected;
    #endregion

    #region Statistics
    //common send interval also delta send
    public static double lastSendTime;
    public static int sendRate => NetServer.sendRate;
    public static float sendInterval => NetServer.sendInterval;

    //baseline end
    private static double lastUnreliableBaselineTime;
    public static int unreliableBaselineRate => NetServer.unreliableBaselineRate;
    public static float unreliableBaselineInterval => NetServer.unreliableBaselineInterval;

    public static event Action<ConnectionQuality, ConnectionQuality> OnConnectionQualityChanged;
    public static ConnectionQuality lastConnectionQuality = ConnectionQuality.Offline;
    public static ConnectionQuality connectionQuality = ConnectionQuality.Offline;
    public static float connectionQualityCheckInterval = 1f;
    private static double lastConnectionQualityCheck;

    #endregion

    public static bool exceptionsDisconnect = true; // 在一些情况下输出报错信息

    //-----------------------------------------------------------------------------------
    #region common
    public static void Init()
    {
        unbatcher = new Unbatcher();

        RegisterAllTransportHandlers();
        RegisterMessageHandlers();
        Transport.instance.enabled = true;
        InitTimeInterpolation();

    }
    public static bool SetReady()
    {
        if (ready)
        {
            Debug.Log("NetworkClient is already ready.");
            return false;
        }
        if (connection == null)
        {
            Debug.LogError("Connection is null while set client ready");
            return false;
        }

        lastConnectionQualityCheck = NetTime.localTime;

        ready = true;

        Send(new ReadyMessage());
        return true;
    }
    private static void SetNotReady()
    {
        ready = false;
    }
    public static void Connect(string address)
    {
        Init();
        connectState = ClientConnectState.Connecting;

        //here transport will send hello to server,then server will send back
        connection = new();
        Transport.instance.ClientConnect(address);
        //最好还是通知connection而非直接调用Transport
    }
    public static void Disconnect()//这里是指主动的断开链接
    {
        if (connectState != ClientConnectState.Connecting &&
            connectState != ClientConnectState.Connected)
            return;

        connectState = ClientConnectState.Disconnecting;
        ready = false;

        connection?.Disconnect();
    }

    public static void NetworkEarlyUpdate()
    {
        if (active == false)
            return;

        if(Transport.instance != null)
            Transport.instance.ClientEarlyUpdate();

        UpdateTimeInterpolation();
    }
    public static void NetworkLateUpdate()
    {
        if (active == false)
            return;

        bool sendIntervalElapsed = Utils.AccurateIntervalElapsed(NetTime.localTime, sendInterval, ref lastSendTime);
        bool unreliableBaselineElapsed = Utils.AccurateIntervalElapsed(NetTime.localTime, unreliableBaselineInterval, ref lastUnreliableBaselineTime);

        if (Application.isPlaying == true && sendIntervalElapsed)
        {
            Broadcast(unreliableBaselineElapsed);
        }

        if (connectionQualityCheckInterval > 0 && NetTime.localTime > lastConnectionQualityCheck + connectionQualityCheckInterval)
        {
            lastConnectionQualityCheck = NetTime.localTime;

            connectionQuality =  ConnectionQualityHeuristics.Simple(NetTime.rtt, NetTime.rttVariance);

            if (lastConnectionQuality != connectionQuality)
            {
                OnConnectionQualityChanged?.Invoke(lastConnectionQuality, connectionQuality);
                lastConnectionQuality = connectionQuality;
            }
        }

        //at least in mirror
        //[connectState == ClientConnectState.Connected] because if state is disconnecting,then ready is still false
        if (ready && connectState == ClientConnectState.Connected)
        {
            if (connection == null)
            {
                Debug.LogError("Client Connection has been destroyed for unknown reason");
                return;
            }
            NetTime.UpdateClient();//ping
            connection.Update();
        }

        //必须最后再调用传输协议的api，真正的socket send 在这个地方被调用 
        if (Transport.instance != null)
            Transport.instance.ClientLateUpdate();
    }

    private static void Broadcast(bool unreliableBaselineElapsed)
    {
        if (ready == false) 
            return;

        Send(new TimeSnapshotMessage(), (int)Channels.Unreliable);
        foreach (NetIdentity identity in connection.owned)
        {
            if (identity != null)
            {
                using (NetWriterPooled writerReliable = NetWriterPool.Get(),
                                        writerUnreliableDelta = NetWriterPool.Get(),
                                        writerUnreliableBaseline = NetWriterPool.Get())
                {
                    identity.SerializeClient(
                        writerReliable,
                        writerUnreliableBaseline,
                        writerUnreliableDelta,
                        unreliableBaselineElapsed);

                    if (writerReliable.Position > 0)
                    {
                        SyncBehavioursMessage message = new SyncBehavioursMessage
                        {
                            netID = identity.netID,
                            payload = writerReliable.ToArraySegment()
                        };
                        Send(message);
                    }

                    if (writerUnreliableDelta.Position > 0)
                    {
                        SyncBehavioursUnreliableDeltaMessage message = new SyncBehavioursUnreliableDeltaMessage
                        {
                            baselineTick = identity.lastUnreliableBaselineSent,
                            netID = identity.netID,
                            payload = writerUnreliableDelta.ToArraySegment()
                        };
                        Send(message, (int)Channels.Unreliable);
                    }

                    if (unreliableBaselineElapsed && writerUnreliableBaseline.Position > 0)
                    {
                        identity.lastUnreliableBaselineSent = (byte)Time.frameCount;

                        SyncBehavioursUnreliableBaselineMessage message = new SyncBehavioursUnreliableBaselineMessage
                        {
                            baselineTick = identity.lastUnreliableBaselineSent,
                            netID = identity.netID,
                            payload = writerUnreliableBaseline.ToArraySegment()
                        };
                        Send(message, (int)Channels.Reliable);
                    }
                }
            }
            else
                Debug.LogWarning("During the process of client serialization, an empty object was discovered");
        }
    }

    public static void Send<T>(T message, int channel = (int)Channels.Reliable)
        where T : struct, INetMessage
    {
        if (connection != null)
        {
            if (connectState == ClientConnectState.Connected)
            {
                connection.Send(message, channel);
            }
            else Debug.LogError("NetworkClient Send when not connected to a server");
        }
        else Debug.LogError("NetworkClient Send with no connection");
    }

    public static void ShutDown()
    {
        List<uint> netIDs = new();
        foreach(var id in spawned.Keys)
        {
            if (id >= NetIdentity.COMMON_ID)
                netIDs.Add(id);
        }
        foreach (var id in netIDs)
            spawned.Remove(id);
        spawnableObjects.Clear();

        connection?.owned.Clear();
        handlers.Clear();

        if (Transport.instance != null)
            Transport.instance.ClientDisconnect();

        connectState = ClientConnectState.None;
        connection = null;
        ready = false;
        isSpawnFinished = false;

        lastSendTime = 0;
        unbatcher = new Unbatcher();

        OnConnectedEvent = null;
        OnDisconnectedEvent = null;
        OnErrorEvent = null;
        OnTransportExceptionEvent = null;
    }

    #endregion

    #region Transport Callback
    private static void RegisterAllTransportHandlers()
    {
        UnregisterAllTransportHandlers();

        Transport.instance.OnClientConnected += OnTransportConnected;
        Transport.instance.OnClientDataReceived += OnTransportData;
        Transport.instance.OnClientDisconnected += OnTransportDisconnected;
        Transport.instance.OnClientError += OnTransportError;
        Transport.instance.OnClientTransportException += OnTransportException;


    }
    private static void UnregisterAllTransportHandlers()
    {
        Transport.instance.OnClientConnected -= OnTransportConnected;
        Transport.instance.OnClientDataReceived -= OnTransportData;
        Transport.instance.OnClientDisconnected -= OnTransportDisconnected;
        Transport.instance.OnClientError -= OnTransportError;
        Transport.instance.OnClientTransportException -= OnTransportException;

    }
    private static void OnTransportConnected()//on client receive server' hello msg
    {
        if (connection != null)
        {
            connectState = ClientConnectState.Connected;
            NetTime.ResetStatics();
            OnConnectedEvent?.Invoke();
            SetReady();
        }
        else
        {
            Debug.LogError("Connection missed, it may be destroyed during client send hello to server");
        }
    }

    private static bool UnpackAndInvoke(NetReader reader, int channelID)
    {
        if (NetMessages.UnpackID(reader, out ushort msgType))
        {
            if (handlers.TryGetValue(msgType, out NetworkMessageDelegate handler))
            {
                handler.Invoke(connection, reader, channelID);

                if (connection != null)
                    connection.lastActiveMessageTime = Time.time;//meaningless for client

                return true;
            }
            else
            {
                Debug.LogWarning($"Unknown message id: {msgType} for Client.");
                return false;
            }
        }
        else
        {
            Debug.LogWarning($"Invalid message header for Client.");
            return false;
        }
    }

    private static void OnTransportData(ArraySegment<byte> data, int channelID)
    {
        if (connection != null)
        {
            if (!unbatcher.AddBatch(data))
            {
                if (exceptionsDisconnect)
                {
                    Debug.LogError($"NetworkClient: failed to add batch, disconnecting.");
                    connection.Disconnect();
                }
                else
                    Debug.LogWarning($"NetworkClient: failed to add batch.");

                return;
            }

            try
            {
                while (isLoadingScene == false && unbatcher.ReceiveNextMessage(out ArraySegment<byte> message, out double remoteTS))
                {
                    using (NetReaderPooled reader = NetReaderPool.Get(message))
                    {
                        if (reader.Remaining >= NetMessages.HASH_HEADER_SIZE)
                        {
                            connection.remoteTS = remoteTS;

                            if (UnpackAndInvoke(reader, channelID) == false)
                            {
                                if (exceptionsDisconnect)
                                {
                                    Debug.LogError($"NetworkClient: failed to unpack and invoke message. Disconnecting.");
                                    connection.Disconnect();
                                }
                                else
                                    Debug.LogWarning($"NetworkClient: failed to unpack and invoke message.");

                                return;
                            }
                        }
                        else
                        {
                            if (exceptionsDisconnect)
                            {
                                Debug.LogError($"NetworkClient: received Message was too short. Disconnecting.");
                                connection.Disconnect();
                            }
                            else
                                Debug.LogWarning("NetworkClient: received Message was too short");
                            return;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"NetworkClient: failed to parse batch: {e.Message}. Disconnect.");
                connection.Disconnect();
                return;
            }
            if (!isLoadingScene && unbatcher.BatchesCount > 0)
            {
                Debug.LogError($"Still had {unbatcher.BatchesCount} batches remaining after processing,Invaild Case, Disconnect.");

                unbatcher.Clear();
                connection.Disconnect();
            }
        }
        else
            Debug.LogError("Skipped Data message handling because connection is null.");
    }
    private static void OnTransportDisconnected()//only Invoke on transport disconnect
    {
        if (connectState == ClientConnectState.Disconnected)
            return;

        connectState = ClientConnectState.Disconnected;

        //InitTimeInterpolation(); EndTimeInterpolation : localTimeline = 0;

        SetNotReady();
        connection?.Clean();
        connection = null;
        UnregisterAllTransportHandlers();

        OnDisconnectedEvent?.Invoke();
    }

    private static void OnTransportError(TransportError error, string reason)
    {
        Debug.LogWarning($"Client Transport Error: {error}: {reason}.");
        OnErrorEvent?.Invoke(error, reason);
    }
    static void OnTransportException(Exception exception)
    {
        Debug.LogWarning($"Client Transport Exception: {exception}.");
        OnTransportExceptionEvent?.Invoke(exception);
    }
    #endregion

    #region Message Handler
    private static void RegisterMessageHandlers()
    {
        RegisterHandler<PongMessage>(NetTime.OnClientPong, false);
        RegisterHandler<PingMessage>(NetTime.OnClientPing, false);
        RegisterHandler<TimeSnapshotMessage>(OnTimeSnapshotMessage, false);
        RegisterHandler<CommandToClientMessage>(OnCommandMessage, true);

        RegisterHandler<ObjectSpawnMessage>(OnObjectSpawn);
        RegisterHandler<ObjectSpawnStartedMessage>(OnObjectSpawnStarted);
        RegisterHandler<ObjectSpawnFinishedMessage>(OnObjectSpawnFinished);
        RegisterHandler<ObjectUnspawnMessage>(OnObjectUnspawn);

        RegisterHandler<SyncBehavioursMessage>(OnSyncBehavioursMessage, true);
        RegisterHandler<SyncBehavioursUnreliableBaselineMessage>(OnSyncBehavioursUnreliableBaselineMessage, true);
        RegisterHandler<SyncBehavioursUnreliableDeltaMessage>(OnSyncBehavioursUnreliableDeltaMessage, true);
    }
    private static void UnregisterMessageHandlers()
    {
        UnregisterHandler<PingMessage>();
        UnregisterHandler<PongMessage>();
    }

    public static void RegisterHandler<T>(Action<T> handler, bool requireAuthentication = true)
        where T : struct, INetMessage
    {
        ushort msgType = NetMessages.GetMessageHash<T>();
        if (handlers.ContainsKey(msgType))
        {
            Debug.LogWarning($"RegisterHandler replacing handler for {typeof(T).FullName}, id={msgType}. replacement Happen.");
        }
        //NetworkMessages.Lookup[msgType] = typeof(T);mirror use this to debug
        void HandlerWrapped(NetConnection _, T value) => handler(value);

        handlers[msgType] = NetMessages.WrapHandler((Action<NetConnection, T>)HandlerWrapped, requireAuthentication, exceptionsDisconnect);
    }

    public static void RegisterHandler<T>(Action<T, int> handler, bool requireAuthentication = true)
        where T : struct, INetMessage
    {
        ushort msgType = NetMessages.GetMessageHash<T>();
        if (handlers.ContainsKey(msgType))
        {
            Debug.LogWarning($"RegisterHandler replacing handler for {typeof(T).FullName}, id={msgType}. replacement Happen.");
        }
        void HandlerWrapped(NetConnection _, T value, int channelID) => handler(value, channelID);

        handlers[msgType] = NetMessages.WrapHandler((Action<NetConnection, T, int>)HandlerWrapped, requireAuthentication, exceptionsDisconnect);
    }
    public static void UnregisterHandler<T>()
        where T : struct, INetMessage
    {
        ushort msgType = NetMessages.GetMessageHash<T>();
        handlers.Remove(msgType);
    }

    private static void OnCommandMessage(CommandToClientMessage message, int channelID)
    {
        if(ready == false)
        {
            Debug.LogWarning($"Client received CommandToClientMessage but client is not ready. Ignoring message.");
            return;
        }

        if (spawned.TryGetValue(message.netID, out NetIdentity identity))
        {
            using (NetReaderPooled reader = NetReaderPool.Get(message.payload))
                identity.HandleRemoteCall(message.netBehaviourID, message.functionHash, RemoteCallType.CommandFromServerToClient, reader);
        }
        else
        {
            Debug.LogWarning($"Client received CommandToClientMessage for netID {message.netID} but no identity was found. Ignoring message.");
            return;
        }
    }
    private static void OnSyncBehavioursMessage(SyncBehavioursMessage message)
    {
        if (spawned.TryGetValue(message.netID, out NetIdentity identity) && identity != null)
        {
            using (NetReaderPooled reader = NetReaderPool.Get(message.payload))
                identity.DeserializeClient(reader, false);
        }
        else 
            Debug.LogWarning($"Did not find target for sync message for {message.netID}.");

    }

    private static void OnSyncBehavioursUnreliableBaselineMessage(SyncBehavioursUnreliableBaselineMessage message, int channel)
    {
        if (channel != (int)Channels.Reliable)
        {
            Debug.LogError($"Client OnEntityStateMessageUnreliableBaseline arrived on channel {channel} instead of Reliable. This should never happen!");
            return;
        }

        if (spawned.TryGetValue(message.netID, out NetIdentity identity) && identity != null)
        {
            identity.lastUnreliableBaselineReceived = message.baselineTick;

            using (NetReaderPooled reader = NetReaderPool.Get(message.payload))
            {
                identity.DeserializeClient(reader, true);
            }
        }
        //如果存在一个udp的销毁信息被发送并提前kcp到达，这种情况完全可能
    }
    private static void OnSyncBehavioursUnreliableDeltaMessage(SyncBehavioursUnreliableDeltaMessage message)
    {
        if (spawned.TryGetValue(message.netID, out NetIdentity identity) && identity != null)
        {
            if (connection.remoteTS <= identity.lastUnreliableStateTime)//这表明udp发送的消息滞后了
            {
                Debug.Log($"Server caught out of order Delayed Unreliable state message for {identity.name}.");
                return;
            }

            if (message.baselineTick != identity.lastUnreliableBaselineReceived)
            {
                Debug.Log($"Server caught Unreliable state message for old baseline for {identity} with baselineTick={identity.lastUnreliableBaselineReceived} messageBaseline={message.baselineTick}. This is fine.");
                return;
            }
            identity.lastUnreliableStateTime = connection.remoteTS;

            using (NetReaderPooled reader = NetReaderPool.Get(message.payload))
            {
                identity.DeserializeClient(reader, false);
            }
        }
    }
    #endregion

    #region AddPlayer And Spawned And Unspawned
    public static bool AddPlayer()
    {
        if(ready == false)
        {
            Debug.LogError("Client AddPlayer failed: client is not ready");
            return false;
        }

        if (connection == null)
        {
            Debug.LogError("Client AddPlayer failed: connection missing");
            return false;
        }

        if (connection.playerIdentity != null)
        {
            Debug.LogWarning("Client AddPlayer failed: a PlayerController was already added.");
            return false;
        }

        Send(new AddPlayerMessage());
        return true;
    }

    //添加prefab时需要检测object存在identity组件
    internal static readonly Dictionary<uint, ClientSpawnHandlerDelegate> spawnHandlers = new();
    internal static readonly Dictionary<uint, ClientUnspawnHandlerDelegate> unspawnHandlers = new();
    public static readonly Dictionary<uint, GameObject> prefabs = new();
    private static readonly Dictionary<NetIdentity, ObjectSpawnMessage> pendingSpawns = new();
    private static readonly Dictionary<ulong, NetIdentity> spawnableObjects = new();//暂存可以被同步的物体
    private static bool isSpawnFinished = true;//这个地方应该改，改成加载游戏场景时给每个player发送信息

    public static void RegisterNetManager(NetIdentity identity, NetManagerType type)
    {
        if (spawned.TryGetValue((uint)type, out NetIdentity netIdentity) && netIdentity != null)
            return;

        identity.isServer = false;
        identity.isClient = true;
        identity.isOwned = false;

        identity.connectionToServer = connection;

        spawned.Add((uint)type, identity);
    }
    public static void UnregisterNetManager(NetIdentity identity, NetManagerType type)
    {
        if (spawned.ContainsKey((uint)type) == false)
            return;

        spawned.Remove((uint)type);

    }
    //用以生成场景的物体
    private static void OnObjectSpawnStarted(ObjectSpawnStartedMessage msg)
    {
        spawnableObjects.Clear();

        NetIdentity[] allIdentities = Resources.FindObjectsOfTypeAll<NetIdentity>();
        foreach (NetIdentity identity in allIdentities)
        {
            if (Utils.IsSceneObject(identity) && identity.netID == 0)
            {
                if (spawnableObjects.TryGetValue(identity.sceneID, out NetIdentity existingIdentity))
                {
                    string warningMsg = "NetworkClient: There are multiple scene objects with the same sceneID";
                    Debug.LogWarning(warningMsg, identity.gameObject);
                }
                else
                {
                    spawnableObjects.Add(identity.sceneID, identity);
                }
            }
        }

        pendingSpawns.Clear();
        isSpawnFinished = false;
    }
    //-----------------------------------------------------------------------------------
    private static void OnObjectSpawn(ObjectSpawnMessage message)
    {

        if (FindOrSpawnObject(message, out NetIdentity identity))
        {
            if (isSpawnFinished)
            {

                ApplySpawnPayload(identity, message);
            }
            else
            {
                byte[] payloadCopy = new byte[message.payload.Count];

                if (message.payload.Count > 0)
                {
                    Array.Copy(
                        message.payload.Array, message.payload.Offset,
                        payloadCopy, 0,
                        message.payload.Count);
                }

                ObjectSpawnMessage messageCopy = new ObjectSpawnMessage
                {
                    netID = message.netID,
                    isOwner = message.isOwner, 
                    sceneID = message.sceneID,
                    assetID = message.assetID,

                    position = message.position,
                    rotation = message.rotation,
                    scale = message.scale,
                    payload = new ArraySegment<byte>(payloadCopy)
                };
                spawned[message.netID] = identity;
                pendingSpawns[identity] = messageCopy;
            }

        }
    }
    private static bool FindOrSpawnObject(ObjectSpawnMessage message, out NetIdentity identity)
    {
        if (spawned.TryGetValue(message.netID, out identity))
            return true;

        if (message.assetID == 0 && message.sceneID == 0)
        {
            Debug.LogError($"OnSpawn message with netId '{message.netID}' has no AssetId or sceneId");
            return false;
        }

        if(message.sceneID == 0)
            identity = SpawnPrefab(message);
        else
            identity = SpawnSceneObject(message.sceneID);

        if (identity == null)
            return false;
        else
            return true;
    }
    private static NetIdentity SpawnPrefab(ObjectSpawnMessage message)
{
        if (spawnHandlers.TryGetValue(message.assetID, out ClientSpawnHandlerDelegate handler))
        {
            GameObject obj = handler(message);
            if (obj == null)
            {
                Debug.LogError($"Spawn Handler returned null, Handler assetID '{message.assetID}'");
                return null;
            }
            if (obj.TryGetComponent(out NetIdentity identity) == false)
            {
                Debug.LogError($"Object Spawned by handler did not have a NetworkIdentity");
                return null;
            }
            return identity;
        }
        else
        {
            if (GetPrefab(message.assetID, out GameObject prefab))
            {
                GameObject obj = GameObject.Instantiate(prefab);
                NetIdentity identity = obj.GetComponent<NetIdentity>();
                return identity;
            }
        }
        Debug.LogError($"Failed to spawn server object, object did not exist in list of prefab");
        return null;
    }
    private static bool GetPrefab(uint assetID, out GameObject prefab)
    {
        prefab = null;
        if (assetID == 0)
            return false;

        if (prefabs.TryGetValue(assetID, out prefab))
            return true;
        else
            return false;
    }
    private static NetIdentity SpawnSceneObject(ulong sceneID)
    {
        if (spawnableObjects.TryGetValue(sceneID, out NetIdentity identity))
        {
            spawnableObjects.Remove(sceneID);
            return identity;
        }
        else
        {
            Debug.LogError($"NetworkClient: Could not find scene object with sceneID {sceneID}");
            return null;
        }

    }

    //-----------------------------------------------------------------------------------
    private static void OnObjectSpawnFinished(ObjectSpawnFinishedMessage msg)
    {
        foreach (NetIdentity identity in spawned.Values.OrderBy(uv => uv.netID))
        {
            if(identity != null)
            {
                if (pendingSpawns.TryGetValue(identity, out ObjectSpawnMessage message))
                    ApplySpawnPayload(identity, message);
            }
            else
                Debug.LogWarning("Found null entry in NetworkClient.spawned");
        }
        pendingSpawns.Clear();
        isSpawnFinished = true;
    }
    private static void ApplySpawnPayload(NetIdentity identity, ObjectSpawnMessage message)
    {
        spawned[message.netID] = identity;
        if (message.assetID != 0)
            identity.assetID = message.assetID;

        identity.transform.position = message.position;
        identity.transform.rotation = message.rotation;
        identity.transform.localScale = message.scale;
        identity.netID = message.netID;
        identity.isOwned = message.isOwner;

        identity.isClient = true;
        identity.connectionToServer = connection;

        if (identity.isOwned)
            connection?.owned.Add(identity);

        if (message.payload.Count > 0)
        {
            using (NetReaderPooled payloadReader = NetReaderPool.Get(message.payload))
            {
                identity.DeserializeClient(payloadReader, true);
            }
        }

        if (isSpawnFinished)
        {
            identity.OnStartClient();
        }
    }

    //-----------------------------------------------------------------------------------
    private static void OnObjectUnspawn(ObjectUnspawnMessage message)
    {

        uint netId = message.netID;

        if (spawned.TryGetValue(netId, out NetIdentity identity) && identity != null)
        {
            identity.OnStopClient();

            if(unspawnHandlers.TryGetValue(identity.assetID, out ClientUnspawnHandlerDelegate handle))
            {
                handle(identity.gameObject);
            }
            else
            {
                GameObject.Destroy(identity.gameObject);
            }

            identity.ResetState();

            connection.owned.Remove(identity); // if any
            spawned.Remove(netId);
        }
        else
        {
            Debug.LogWarning("Unspawn Failed, netid not exist. netID : " + netId.ToString());
        }
    }

    #region Prefabs Register
    private static uint RegisterPrefabIdentity(
        NetIdentity prefab, 
        ClientSpawnHandlerDelegate spawnHandler = null,
        ClientUnspawnHandlerDelegate unspawnHandler = null)
    {
        if (prefab.assetID == 0)
        {
            Debug.LogError($"Can not Register '{prefab.name}' because it had empty assetid. If this is a scene Object use RegisterSpawnHandler instead");
            return 0;
        }

        if (prefab.sceneID != 0)
        {
            Debug.LogError($"Can not Register '{prefab.name}' because it has a sceneId, make sure you are passing in the original prefab and not an instance in the scene.");
            return 0;
        }

        if (prefabs.ContainsKey(prefab.assetID))
        {
            GameObject existingPrefab = prefabs[prefab.assetID];
            Debug.LogWarning($"There has exist a prefab with ID{prefab.assetID}, failed to register");
            return 0;
        }

        if (spawnHandlers.ContainsKey(prefab.assetID))
        {
            Debug.LogWarning($"There has exist a prefab which has it own spawn handlers, Default prefab construction method will be Skipped");
        }
        prefabs[prefab.assetID] = prefab.gameObject;
        if(spawnHandler != null)
            spawnHandlers[prefab.assetID] = spawnHandler;

        if (unspawnHandler != null)
            unspawnHandlers[prefab.assetID] = unspawnHandler;

        return prefab.assetID;
    }

    public static uint RegisterPrefab(
        GameObject prefab, 
        ClientSpawnHandlerDelegate spawnHandler = null,
        ClientUnspawnHandlerDelegate unspawnHandler = null)
    {
        if (prefab == null)
        {
            Debug.LogError("Could not register prefab because it was null");
            return 0;
        }

        if (prefab.TryGetComponent(out NetIdentity identity) == false)
        {
            Debug.LogError($"Could not register '{prefab.name}' since it contains no NetworkIdentity component");
            return 0;
        }

        return RegisterPrefabIdentity(identity, spawnHandler, unspawnHandler);
    }

    #endregion

    #endregion

    #region Get NetworkObject

    public static bool TryGetNetworkObject(uint netID, out GameObject go)
    {
        if(spawned.TryGetValue(netID, out NetIdentity identity))
        {
            go = identity.gameObject;
            return true;
        }
        else
        {
            go = null;
            Debug.LogWarning("Net Object not found, id : " + netID.ToString());
            return false;
        }
    }

    #endregion

    #region TimeInterpolation

    public static SnapshotInterpolationSettings snapshotSettings = new();
    public static double localTimeline;//时间插值
    internal static double localTimescale = 1;

    public static double bufferTimeMultiplier;
    public static double bufferTime => NetServer.sendInterval * bufferTimeMultiplier;


    public static SortedList<double, TimeSnapshot> snapshots = new ();
    private static ExponentialMovingAverage driftEma;
    private static ExponentialMovingAverage deliveryTimeEma; 
    private static void InitTimeInterpolation()
    {
        bufferTimeMultiplier = snapshotSettings.bufferTimeMultiplier;
        localTimeline = 0;
        localTimescale = 1;
        snapshots.Clear();

        driftEma = new ExponentialMovingAverage(NetServer.sendRate * snapshotSettings.driftEmaDuration);
        deliveryTimeEma = new ExponentialMovingAverage(NetServer.sendRate * snapshotSettings.deliveryTimeEmaDuration);
    }
    private static void OnTimeSnapshotMessage(TimeSnapshotMessage msg)
    {
        TimeSnapshot snapshot = new TimeSnapshot(connection.remoteTS, NetTime.localTime);
        SnapshotInterpolation.InsertAndAdjust(
            snapshots,
            snapshot,
            NetClient.snapshotSettings.bufferLimit,
            ref localTimeline,
            ref localTimescale,
            ref driftEma,
            ref deliveryTimeEma,
            NetServer.sendInterval,
            bufferTime,
            NetClient.snapshotSettings.catchupSpeed,
            NetClient.snapshotSettings.slowdownSpeed,
            NetClient.snapshotSettings.catchupNegativeThreshold,
            NetClient.snapshotSettings.catchupPositiveThreshold
        );
    }

    private static void UpdateTimeInterpolation()
    {
        if (snapshots.Count > 0)
        {
            SnapshotInterpolation.UpdateTime(Time.unscaledDeltaTime, ref localTimeline, localTimescale);
            SnapshotInterpolation.StepInterpolation(snapshots, localTimeline, out _, out _, out double t);
        }
    }
    #endregion
}
