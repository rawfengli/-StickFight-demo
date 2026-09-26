using System;
using System.Collections.Generic;
using UnityEngine;

//之后再补身份唯一性认证

public static class NetServer
{
    public static Action<NetConnectionToClient> OnConnectedEvent;
    public static Action<NetConnectionToClient> OnConnectionReadyEvent;
    public static Action<NetConnectionToClient> OnDisconnectedEvent;
    public static Action<NetConnectionToClient, TransportError, string> OnErrorEvent;
    public static Action<NetConnectionToClient, Exception> OnTransportExceptionEvent;

    public static bool active;

    private static Dictionary<int, NetConnectionToClient> connections = new();
    private static List<NetConnectionToClient> connectionsCopy = new();

    private static readonly Dictionary<ushort, NetworkMessageDelegate> handlers = new();

    private static bool initialized;

    private static int maxConnections;
    public static bool listen = true;//这个可能是为host准备的

    public static Dictionary<int, Dictionary<uint, NetIdentity>> allSceneSpawned = new();
    public static Dictionary<uint, NetIdentity> spawned = new();

    #region Send Statistics

    private static double lastSendTime;
    public static int sendRate = 60;
    public static float sendInterval
    {
        get
        {
            if (sendRate < int.MaxValue)
                return 1.0f / sendRate;
            else
                return 0f;
        }
    }

    private static double lastUnreliableBaselineTime;
    public static int unreliableBaselineRate = 1;//1s
    public static float unreliableBaselineInterval
    {
        get
        {
            if (sendRate < int.MaxValue)
                return 1.0f / unreliableBaselineRate;
            else
                return 0f;
        }
    }

    public static double actualSendRateStart { get; private set; }
    public static int actualSendRateCounter { get; private set; }
    public static int actualSendRate { get; private set; }

    #endregion

    #region Frame Monitor
    public static TimeSample earlyUpdateDuration;
    public static TimeSample lateUpdateDuration;

    public static TimeSample fullUpdateDuration;
    #endregion
    // disconnect inactive
    public static bool disconnectInactiveConnections;
    public static float disconnectInactiveTimeout = 180;//3 min

    public static bool exceptionsDisconnect = true; // 在一些情况下输出报错信息


    #region common
    /// <summary>
    /// 不仅仅是监听，同时也是实际上启动服务器
    /// </summary>
    /// <param name="maxConnections"></param>
    public static void StartListen(int maxConnections)
    {
        Init();

        NetTime.ResetStatics();

        if (listen)
        {
            NetServer.maxConnections = maxConnections;

            Transport.instance.ServerStart();
            if (Utils.IsHeadless())
            {
                Debug.Log($"Server listening on port {Transport.instance.Port}");
            }
            else
                Debug.Log("Server started listening");
        }
        else
            maxConnections = 0;

        active = true;
    }
    private static void Init()
    {
        if (initialized)
            return;
        connections.Clear();//why do more thing on connection

        //只要启动服务器就开始计时
        if (Transport.instance == null)
        {
            Debug.Log("Init Server Failed, active transport not exist");
            return;
        }
        initialized = true;
        AddTransportHandlers();
        RegisterMessageHandlers();

        earlyUpdateDuration = new TimeSample(sendRate);
        lateUpdateDuration = new TimeSample(sendRate);
        fullUpdateDuration = new TimeSample(sendRate);
    }

    internal static void NetworkEarlyUpdate()
    {
        if (active == false)
            return;

        earlyUpdateDuration.Begin();
        fullUpdateDuration.Begin();

        if (Transport.instance != null)
            Transport.instance.ServerEarlyUpdate();

        foreach (NetConnectionToClient connection in connections.Values)
            connection.UpdateTimeInterpolation();

        earlyUpdateDuration.End();
    }
    internal static void NetworkLateUpdate()
    {
        if (active == false)
            return;

        lateUpdateDuration.Begin();

        bool sendIntervalElapsed = Utils.AccurateIntervalElapsed(NetTime.localTime, sendInterval, ref lastSendTime);
        bool unreliableBaselineElapsed = Utils.AccurateIntervalElapsed(NetTime.localTime, unreliableBaselineInterval, ref lastUnreliableBaselineTime);

        if (Application.isPlaying == true && sendIntervalElapsed)
        {
            Broadcast(unreliableBaselineElapsed);
        }

        //cal actualSendRate in single frame
        actualSendRateCounter++;
        if (NetTime.localTime >= actualSendRateStart + 1)
        {
            float passed = (float)(NetTime.localTime - actualSendRateStart);
            actualSendRate = Mathf.RoundToInt(actualSendRateCounter / passed);
            actualSendRateStart = NetTime.localTime;
            actualSendRateCounter = 0;
        }
        if (Transport.instance != null)
            Transport.instance.ServerLateUpdate();

        lateUpdateDuration.End();
        fullUpdateDuration.End();
    }
    private static void Broadcast(bool unreliableBaselineElapsed)
    {
        //because during Broadcast, some conn may be removed from Dictionary, so here we copy
        connectionsCopy.Clear();
        connectionsCopy.AddRange(connections.Values);

        foreach (NetConnectionToClient connection in connectionsCopy)
        {
            if (DisconnectIfInactive(connection))
                continue;
            if (connection.ready)
            {
                connection.Send(new TimeSnapshotMessage(), (int)Channels.Unreliable);

                BroadcastMessageToConnection(connection, unreliableBaselineElapsed);
            }
            NetTime.UpdateServer(connection);
            connection.Update();
        }
    }
    private static bool DisconnectIfInactive(NetConnectionToClient connection)
    {
        if (disconnectInactiveConnections && connection.IsAlive(disconnectInactiveTimeout) == false)
        {
            Debug.LogWarning($"Disconnecting {connection} for inactivity!");
            connection.Disconnect();
            return true;
        }
        return false;
    }
    private static void BroadcastMessageToConnection(NetConnectionToClient connection, bool unreliableBaselineElapsed)
    {
        //对每一个identity他所正在观察的其他identity进行序列化后传输
        bool existNull = false;
        foreach (NetIdentity identity in connection.observing)
        {
            if (identity != null)
            {
                using (NetWriterPooled serialization = NetWriterPool.Get(),
                                       baselineSerialization = NetWriterPool.Get(),
                                       deltaSerialization = NetWriterPool.Get())
                {
                    SerializeBehaviourForConnection(
                        identity, connection, unreliableBaselineElapsed,
                        serialization, baselineSerialization, deltaSerialization);

                    if (serialization.Position > 0)
                    {
                        SyncBehavioursMessage msg = new SyncBehavioursMessage
                        {
                            netID = identity.netID,
                            payload = serialization.ToArraySegment()
                        };
                        connection.Send(msg);
                    }

                    if (deltaSerialization.Position > 0)
                    {
                        SyncBehavioursUnreliableDeltaMessage msg = new SyncBehavioursUnreliableDeltaMessage
                        {
                            baselineTick = identity.lastUnreliableBaselineSent,
                            netID = identity.netID,
                            payload = deltaSerialization.ToArraySegment()
                        };

                        connection.Send(msg, (int)Channels.Unreliable);
                    }

                    if (baselineSerialization.Position > 0)
                    {
                        identity.lastUnreliableBaselineSent = (byte)Time.frameCount;
                        SyncBehavioursUnreliableBaselineMessage msg = new SyncBehavioursUnreliableBaselineMessage
                        {
                            baselineTick = identity.lastUnreliableBaselineSent,
                            netID = identity.netID,
                            payload = baselineSerialization.ToArraySegment()
                        };
                        connection.Send(msg);
                    }
                }
            }
            else
            {
                existNull = true;
                Debug.LogWarning($"Found 'null' entry in observing list for connectionId={connection.connectionID}. these later will be removed");

            }
            if (existNull)
                connection.observing.RemoveWhere(identity => identity == null);

        }
    }
    private static void SerializeBehaviourForConnection(
        NetIdentity identity, NetConnectionToClient targetConnection, bool unreliableBaselineElapsed,
        NetWriter serialization, NetWriter baselineSerialization, NetWriter deltaSerialization)
    {
        identity.SerializeServer(
            Time.frameCount, unreliableBaselineElapsed,
            out NetWriter ownerWriterReliable, out NetWriter observersWriterReliable,
            out NetWriter ownerWriterUnreliableBaseline, out NetWriter observersWriterUnreliableBaseline,
            out NetWriter ownerWriterUnreliableDelta, out NetWriter observersWriterUnreliableDelta);

        if (identity.connectionToClient == targetConnection)
        {
            ArraySegment<byte> data;

            data = ownerWriterReliable.ToArraySegment();
            serialization.WriteBytes(data.Array, data.Offset, data.Count);

            data = ownerWriterUnreliableDelta.ToArraySegment();
            deltaSerialization.WriteBytes(data.Array, data.Offset, data.Count);

            data = ownerWriterUnreliableBaseline.ToArraySegment();
            baselineSerialization.WriteBytes(data.Array, data.Offset, data.Count);
        }
        else
        {
            ArraySegment<byte> data;

            data = observersWriterReliable.ToArraySegment();
            serialization.WriteBytes(data.Array, data.Offset, data.Count);

            data = observersWriterUnreliableDelta.ToArraySegment();
            deltaSerialization.WriteBytes(data.Array, data.Offset, data.Count);

            data = observersWriterUnreliableBaseline.ToArraySegment();
            baselineSerialization.WriteBytes(data.Array, data.Offset, data.Count);
        }
    }
    #endregion

    #region Transport Action
    private static void AddTransportHandlers()
    {
        RemoveTransportHandlers();
        Transport.instance.OnServerConnectedWithAddress += OnTransportConnectedWithAddress;
        Transport.instance.OnServerDataReceived += OnTransportData;
        Transport.instance.OnServerDisconnected += OnTransportDisconnected;
        Transport.instance.OnServerError += OnTransportError;
        Transport.instance.OnServerTransportException += OnTransportException;
    }
    private static void RemoveTransportHandlers()
    {
        Transport.instance.OnServerConnectedWithAddress -= OnTransportConnectedWithAddress;
        Transport.instance.OnServerDataReceived -= OnTransportData;
        Transport.instance.OnServerDisconnected -= OnTransportDisconnected;
        Transport.instance.OnServerError -= OnTransportError;
        Transport.instance.OnServerTransportException -= OnTransportException;
    }

    private static void OnTransportConnectedWithAddress(int connectionID, string clientAddress)
    {
        if (ValidNewConnection(connectionID, clientAddress))
        {
            NetConnectionToClient conn = new NetConnectionToClient(connectionID, clientAddress);
            AddConnection(conn);
            OnConnectedEvent?.Invoke(conn);
        }
        else
        {
            Transport.instance.ServerDisconnect(connectionID);
        }
    }

    private static bool UnpackAndInvoke(NetConnectionToClient conn, NetReader reader, int channel)
    {
        if (NetMessages.UnpackID(reader, out ushort msgType))
        {
            if (handlers.TryGetValue(msgType, out NetworkMessageDelegate handler))
            {
                handler.Invoke(conn, reader, channel);
                //ping msg和pong msg不应该算进去
                //这里mirror 会把ping-pong消息也算入lastMessageTime
                //这会导致DisconnectIfInactive永远不可能生效
                conn.lastActiveMessageTime = NetTime.localTimeF;
                return true;
            }
            else
            {
                Debug.LogWarning($"Unknown message id: {msgType} for connection: {conn}.");
                return false;
            }
        }
        else
        {
            Debug.LogWarning($"Invalid message header for connection: {conn}.");
            return false;
        }
    }
    private static void OnTransportData(int connectionID, ArraySegment<byte> data, int channelID)
    {
        if (connections.TryGetValue(connectionID, out NetConnectionToClient connection))
        {
            if (!connection.unbatcher.AddBatch(data))
            {
                if (exceptionsDisconnect)
                {
                    Debug.LogError($"NetworkServer: received message from connectionID:{connectionID} was too short.");
                    connection.Disconnect();
                }
                else
                    Debug.LogWarning($"NetworkServer: received message from connectionID:{connectionID} was too short.");

                return;
            }

            try
            {
                while (connection.unbatcher.ReceiveNextMessage(out ArraySegment<byte> message, out double remoteTS))
                {
                    using (NetReaderPooled reader = NetReaderPool.Get(message))
                    {
                        if (reader.Remaining >= NetMessages.HASH_HEADER_SIZE)
                        {
                            connection.remoteTS = remoteTS;
                            if (!UnpackAndInvoke(connection, reader, channelID))
                            {
                                if (exceptionsDisconnect)
                                {
                                    Debug.LogError($"NetworkServer: failed to unpack and invoke message. Disconnecting {connectionID}.Disconnect");
                                    connection.Disconnect();
                                }
                                else
                                    Debug.LogWarning($"NetworkServer: failed to unpack and invoke message from connectionId:{connectionID}.");

                                return;
                            }
                        }
                        else
                        {
                            if (exceptionsDisconnect)
                            {
                                Debug.LogError($"NetworkServer: received message from connectionID:{connectionID} was too short for message header. Disconnect.");
                                connection.Disconnect();
                            }
                            else
                                Debug.LogWarning($"NetworkServer: received message from connectionID:{connectionID} was too short for message header. Disconnect.");

                            return;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"NetworkServer: failed to parse batch from connectionID:{connectionID}: {e.Message}. Disconnect.");
                connection.Disconnect();
                return;
            }
            if (connection.unbatcher.BatchesCount > 0)
            {
                Debug.LogError($"Still had {connection.unbatcher.BatchesCount} batches remaining after processing,Invaild Case, Disconnect.");

                connection.unbatcher.Clear();
                connection.Disconnect();
            }
        }
        else
            Debug.LogError($"HandleData Unknown connectionID:{connectionID}");
    }

    private static void OnTransportDisconnected(int connectionID)
    {
        if (connections.TryGetValue(connectionID, out NetConnectionToClient connection) == false)
            return;

        connection.Clean();
        RemoveConnection(connectionID);
        OnDisconnectedEvent?.Invoke(connection);
    }
    private static bool ValidNewConnection(int connectionID, string address)
    {
        if (!listen)
        {
            Debug.Log($"Server not listening, rejecting connectionId={connectionID} with address={address}");
            return false;
        }
        if (connectionID == 0)
        {
            Debug.Log($"ID zero is invalid ID, rejecting connectionId={connectionID} with address={address}");
            return false;
        }
        if (connections.ContainsKey(connectionID))
        {
            Debug.LogError($"{connectionID}already exist.");
            return false;
        }
        if (connections.Count >= maxConnections)
        {
            Debug.LogError($"Server full,  rejecting connectionId={connectionID} with address={address}");
            return false;
        }
        return true;
    }

    static void OnTransportError(int connectionId, TransportError error, string reason)
    {
        // transport errors will happen. logging a warning is enough.
        // make sure the user does not panic.
        Debug.LogWarning($"Server Transport Error for connId={connectionId}: {error}: {reason}.");
        // try get connection. passes null otherwise.
        connections.TryGetValue(connectionId, out NetConnectionToClient conn);
        OnErrorEvent?.Invoke(conn, error, reason);
    }
    static void OnTransportException(int connectionId, Exception exception)
    {
        // transport errors will happen. logging a warning is enough.
        // make sure the user does not panic.
        Debug.LogWarning($"Server Transport Exception for connId={connectionId}: {exception}");
        // try get connection. passes null otherwise.
        connections.TryGetValue(connectionId, out NetConnectionToClient conn);
        OnTransportExceptionEvent?.Invoke(conn, exception);
    }
    #endregion

    #region Message Handler
    //之后再补身份唯一性认证
    public static void RegisterHandler<T>(Action<NetConnectionToClient, T> handler, bool requireAuthentication = true)
        where T : struct, INetMessage
    {
        ushort msgType = NetMessages.GetMessageHash<T>();
        if (handlers.ContainsKey(msgType))
        {
            Debug.LogWarning($"RegisterHandler replacing handler for {typeof(T).FullName}, id={msgType}. replacement Happen.");
        }
        //NetworkMessages.Lookup[msgType] = typeof(T);mirror use this to debug
        handlers[msgType] = NetMessages.WrapHandler(handler, requireAuthentication, exceptionsDisconnect);
    }

    public static void RegisterHandler<T>(Action<NetConnectionToClient, T, int> handler, bool requireAuthentication = true)
        where T : struct, INetMessage
    {
        ushort msgType = NetMessages.GetMessageHash<T>();
        if (handlers.ContainsKey(msgType))
        {
            Debug.LogWarning($"RegisterHandler replacing handler for {typeof(T).FullName}, id={msgType}. replacement Happen.");
        }

        handlers[msgType] = NetMessages.WrapHandler(handler, requireAuthentication, exceptionsDisconnect);
    }

    public static void UnregisterHandler<T>()
        where T : struct, INetMessage
    {
        ushort msgType = NetMessages.GetMessageHash<T>();
        handlers.Remove(msgType);
    }

    private static void RegisterMessageHandlers()
    {
        RegisterHandler<ReadyMessage>(OnClientReadyMessage);
        RegisterHandler<PingMessage>(NetTime.OnServerPing, false);
        RegisterHandler<PongMessage>(NetTime.OnServerPong, false);
        RegisterHandler<TimeSnapshotMessage>(OnTimeSnapshotMessage, false);
        RegisterHandler<CommandToServerMessage>(OnCommandMessage, true);

        RegisterHandler<SyncBehavioursMessage>(OnSyncBehavioursMessage, true);
        RegisterHandler<SyncBehavioursUnreliableBaselineMessage>(OnSyncBehavioursUnreliableBaselineMessage, true);
        RegisterHandler<SyncBehavioursUnreliableDeltaMessage>(OnSyncBehavioursUnreliableDeltaMessage, true);

    }
    private static void UnegisterMessageHandlers()
    {
        UnregisterHandler<ReadyMessage>();
        UnregisterHandler<PingMessage>();
        UnregisterHandler<PongMessage>();
        UnregisterHandler<TimeSnapshotMessage>();
        UnregisterHandler<CommandToServerMessage>();
    }

    private static void OnClientReadyMessage(NetConnectionToClient conn, ReadyMessage msg)
    {
        conn.ready = true;
        OnConnectionReadyEvent.Invoke(conn);
    }
    private static void OnTimeSnapshotMessage(NetConnectionToClient conn, TimeSnapshotMessage msg)
    {
        conn.OnTimeSnapshot(new TimeSnapshot(conn.remoteTS, NetTime.localTime));
    }
    private static void OnCommandMessage(NetConnectionToClient conn, CommandToServerMessage msg, int channelID)
    {
        if (conn.ready == false)
        {
            Debug.LogWarning("Received command message from unready client.");
            return;
        }
        if (spawned.TryGetValue(msg.netID, out NetIdentity identity) == false)
        {
            Debug.LogWarning($"Received command message on unknown identity,netID:{msg.netID}.");
            return;
        }

        //bool requiresAuthority = RemoteProcedureCalls.CommandRequiresAuthority(msg.functionHash);
        bool requiresAuthority = false;
        if (requiresAuthority && identity.connectionToClient != conn)
        {
            if (msg.netBehaviourID < identity.behaviours.Length &&
                identity.behaviours[msg.netBehaviourID] is NetBehaviour component)
            {
                if (RemoteProcedureCalls.GetFunctionMethodName(msg.functionHash, out string methodName))
                {
                    Debug.LogWarning($"Command {methodName} received for {identity.name} [netId={msg.netID}] component {component.name} [index={msg.netBehaviourID}] without authority");
                    return;
                }
            }
            Debug.LogWarning($"Command received for {identity.name} [netId={msg.netID}] without authority");
            return;
        }

        using (NetReaderPooled networkReader = NetReaderPool.Get(msg.payload))
            identity.HandleRemoteCall(msg.netBehaviourID, msg.functionHash, RemoteCallType.CommandFromClientToServer, networkReader, conn);

    }

    private static void OnSyncBehavioursMessage(NetConnectionToClient connection, SyncBehavioursMessage message)
    {
        if (spawned.TryGetValue(message.netID, out NetIdentity identity) && identity != null)
        {
            if (identity.connectionToClient == connection)
            {
                using (NetReaderPooled reader = NetReaderPool.Get(message.payload))
                {
                    if (identity.DeserializeServer(reader, false) == false)
                    {
                        if (exceptionsDisconnect)
                        {
                            Debug.LogError($"Server failed to deserialize client state for {identity.name} with netId={identity.netID}, Disconnecting.");
                            connection.Disconnect();
                        }
                        else
                            Debug.LogWarning($"Server failed to deserialize client state for {identity.name} with netId={identity.netID}.");
                    }
                }
            }
            else
                Debug.LogWarning($"Sync Message from {connection} for {identity.name} with different connection.");
        }

    }
    //几乎没有区别就只有验证通道和更新基线编号
    private static void OnSyncBehavioursUnreliableBaselineMessage(NetConnectionToClient connection, SyncBehavioursUnreliableBaselineMessage message, int channelID)
    {
        if (channelID != (int)Channels.Reliable)
        {
            Debug.LogError($"Server OnEntityStateMessageUnreliableBaseline arrived on channel {channelID} instead of Reliable. This should never happen!");
            return;
        }
        if (spawned.TryGetValue(message.netID, out NetIdentity identity) && identity != null)
        {
            if (identity.connectionToClient == connection)
            {
                identity.lastUnreliableBaselineReceived = message.baselineTick;
                using (NetReaderPooled reader = NetReaderPool.Get(message.payload))
                {
                    if (identity.DeserializeServer(reader, false) == false)
                    {
                        if (exceptionsDisconnect)
                        {
                            Debug.LogError($"Server failed to deserialize client state for {identity.name} with netId={identity.netID}, Disconnecting.");
                            connection.Disconnect();
                        }
                        else
                            Debug.LogWarning($"Server failed to deserialize client state for {identity.name} with netId={identity.netID}.");
                    }
                }
            }
            else
                Debug.LogWarning($"Sync Message from {connection} for {identity.name} with different connection.");
        }
    }
    private static void OnSyncBehavioursUnreliableDeltaMessage(NetConnectionToClient connection, SyncBehavioursUnreliableDeltaMessage message)
    {
        if (spawned.TryGetValue(message.netID, out NetIdentity identity) && identity != null)
        {
            if (identity.connectionToClient == connection)
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
                    if (identity.DeserializeServer(reader, false) == false)
                    {
                        if (exceptionsDisconnect)
                        {
                            Debug.LogError($"Server failed to deserialize client state for {identity.name} with netId={identity.netID}, Disconnecting.");
                            connection.Disconnect();
                        }
                        else
                            Debug.LogWarning($"Server failed to deserialize client state for {identity.name} with netId={identity.netID}.");
                    }
                }
            }
            else
                Debug.LogWarning($"Sync Message from {connection} for {identity.name} with different connection.");
        }
    }


    #endregion

    #region AddPlayer And Spawn

    /// <summary>
    /// 这个函数已经不适合这个项目的架构了
    /// </summary>
    /// <returns></returns>
    public static bool SpawnObjectsInNowScene()
    {
        bool pass = true;
        if (pass)
            return true;
        if (!active)
            return false;
        NetIdentity[] identities = Resources.FindObjectsOfTypeAll<NetIdentity>();

        foreach (NetIdentity identity in identities)
        {
            if (Utils.IsSceneObject(identity) && identity.netID == 0)
            {
                identity.gameObject.SetActive(true);
            }
        }

        foreach (NetIdentity identity in identities)
        {
            if (Utils.IsSceneObject(identity) && identity.netID == 0)
            {
                Spawn(identity.gameObject, identity.connectionToClient);
            }
        }
        return true;
    }

    public static void RegisterNetManager(NetIdentity identity, NetManagerType type)
    {
        if (spawned.ContainsKey((uint)type))
            return;
        identity.isServer = true;
        identity.isClient = false;

        identity.connectionToClient = null;
        spawned.Add((uint)type, identity);
    }
    public static void UnregisterNetManager(NetIdentity identity, NetManagerType type)
    {
        if (spawned.ContainsKey((uint)type) == false)
            return;

        spawned.Remove((uint)type);

    }
    private static void Respawn(NetIdentity identity)
    {
        if (identity.netID == 0)
            Spawn(identity.gameObject, null);
        else
            SendSpawnMessage(identity, identity.connectionToClient);
    }
    public static void Spawn(GameObject obj, NetConnectionToClient conn = null)
    {
        if(active == false)
        {
            Debug.LogError("Spawn failed, server not active");
            return;
        }
        if (obj.TryGetComponent(out NetIdentity identity) == false)
        {
            Debug.LogError($"Spawn failed, {obj.name} does not have a NetworkIdentity component.");
            return;
        }

        if (spawned.ContainsKey(identity.netID))
        {
            Debug.LogWarning($"Spawned {identity.netID} already exists.");
            return;
        }

        identity.connectionToClient = conn;
        identity.gameObject.SetActive(true);

        //一般初始化
        if (identity.isServer == false && identity.netID == 0)
        {
            identity.isServer = true;
            identity.netID = NetIdentity.GetNextNetworkID();

            spawned[identity.netID] = identity;
            identity.OnStartServer();
        }

        if(identity.visibility != Visibility.ForceHidden)
        {
            foreach (NetConnectionToClient targetConnection in connections.Values)
            {
                if(targetConnection.ready)
                {
                    identity.AddObserver(targetConnection);
                    SendSpawnMessage(identity, targetConnection);
                }
            }
        }
    }
    public static void Spawn(GameObject obj, NetConnectionToClient[] otherConns, NetConnectionToClient ownerConn = null)
    {
        if (active == false)
        {
            Debug.LogError("Spawn failed, server not active");
            return;
        }

        if (obj.TryGetComponent(out NetIdentity identity) == false)
        {
            Debug.LogError($"Spawn failed, {obj.name} does not have a NetworkIdentity component.");
            return;
        }

        if (spawned.ContainsKey(identity.netID))
        {
            Debug.LogWarning($"Spawned {identity.netID} already exists.");
            return;
        }

        identity.connectionToClient = ownerConn;
        identity.gameObject.SetActive(true);
        //一般初始化
        if (identity.isServer == false && identity.netID == 0)
        {
            identity.isServer = true;
            
            identity.netID = NetIdentity.GetNextNetworkID();

            spawned[identity.netID] = identity;
            identity.OnStartServer();
        }

        if (identity.visibility != Visibility.ForceHidden)
        {
            foreach (NetConnectionToClient targetConnection in otherConns)
            {
                if (targetConnection == null)
                    continue;
                if (targetConnection.ready)
                {
                    identity.AddObserver(targetConnection);
                    SendSpawnMessage(identity, targetConnection);
                }
            }
        }
    }


    /*
    public static bool AddPlayerToConnection(NetConnectionToClient conn, GameObject player)
    {
        bool pass = true;
        if (pass)
            return true;

        if (conn.identity != null)
        {
            Debug.Log("AddPlayer: player object already exists");
            return false;
        }

        if (player.TryGetComponent<NetIdentity>(out NetIdentity identity))
        {
            conn.identity = identity;
            identity.BindConnectionToClient(conn);

            //为new player准备场景中的object
            //SpawnObserversForConnection(conn);
            //为场景中的object生成new player
            Respawn(identity);

            return true;
        }
        else
        {
            Debug.LogError("Player prefab does not have a NetIdentity component.Please add it first");
            return false;
        }
    }
    */
    /*
    /// <summary>
    /// 将所有观察者添加到指定的连接上。这里的观察者通常是其他玩家，时机是在玩家加入游戏后，或者切换场景，
    /// 为新的player生成物件
    /// </summary>
    public static void SpawnObserversForConnection(NetConnectionToClient targetConnection)
    {
        if (targetConnection.spawnedObservers)
            return;

        targetConnection.spawnedObservers = true;

        targetConnection.Send(new ObjectSpawnStartedMessage());
        //为每个spawned添加新的观察者,也就是这个conn
        //为这个conn添加所有将要观察的identities
        foreach (NetIdentity identity in spawned.Values)
        {
            if(identity.gameObject.activeSelf)
            {
                switch(identity.visibility)
                {
                    case Visibility.Default:

                        identity.AddObserver(targetConnection);
                        
                        if(targetConnection.ready)
                            SendSpawnMessage(identity, targetConnection);
                        break;

                    case Visibility.ForceHidden:
                        break;

                    case Visibility.ForceShown:
                        identity.AddObserver(targetConnection);

                        if (targetConnection.ready)
                            SendSpawnMessage(identity, targetConnection);
                        break;
                }
            }
        }

        targetConnection.Send(new ObjectSpawnFinishedMessage());
    }
    */
    /// <summary>
    /// 将identity这个物件的spawn消息发送给指定的连接。
    /// </summary>
    public static void SendSpawnMessage(NetIdentity identity, NetConnectionToClient targetConnection)
    {
        if (identity == null || identity.spawnOnServerOnly) 
            return;

        using (NetWriterPooled ownerWriter = NetWriterPool.Get(), observersWriter = NetWriterPool.Get())
        {
            bool isOwner;
            if (targetConnection == identity.connectionToClient)
                isOwner = true;
            else
                isOwner = false;
            ArraySegment<byte> payload = CreateSpawnMessagePayload(isOwner, identity, ownerWriter, observersWriter);

            ObjectSpawnMessage message = new ObjectSpawnMessage
            {
                netID = identity.netID,
                isOwner = isOwner,
                sceneID = identity.sceneID,
                assetID = identity.assetID,

                position = identity.transform.position,
                rotation = identity.transform.rotation,
                scale = identity.transform.localScale,
                payload = payload
            };

            targetConnection.Send(message);
        }
    }
    //对应的owner和observer可能存在不同的序列化逻辑
    private static ArraySegment<byte> CreateSpawnMessagePayload(
        bool isOwner,
        NetIdentity identity,
        NetWriterPooled ownerWriter,
        NetWriterPooled observersWriter)
    {
        if (identity.behaviours.Length == 0)
            return default;

        identity.SerializeServer_SpawnObject(ownerWriter, observersWriter);

        ArraySegment<byte> ownerPayload = ownerWriter.ToArraySegment();
        ArraySegment<byte> observersPayload = observersWriter.ToArraySegment();

        if (isOwner)
            return ownerPayload;
        else
            return observersPayload;
    }

    /// <summary>
    /// Unspawn不会自动销毁物体,就像spawn不会自动生成物体一样
    /// </summary>
    public static void Unspawn(GameObject obj, NetConnectionToClient[] otherConns, Action destroyAction = null)
    {
        if (!active)
        {
            Debug.LogWarning("NetworkServer not active");
            return;
        }
        if (!obj.TryGetComponent<NetIdentity>(out NetIdentity identity))
        {
            Debug.Log("unspawn gameobject has no network identity");
            return;
        }
        foreach(var conn in otherConns)
        {
            if (conn == null)
                continue;
            identity.RemoveObserver(conn);
            SendUnspawnMessage(identity, conn);
        }
        identity.ResetState();
        identity.OnStopServer();

        spawned.Remove(identity.netID);

        destroyAction?.Invoke();
    }
    public static void SendUnspawnMessage(NetIdentity identity, NetConnectionToClient targetConnection)
    {
        if (identity == null || targetConnection == null)
            return;

        using (NetWriterPooled writer = NetWriterPool.Get())
        {
            ObjectUnspawnMessage message = new ObjectUnspawnMessage
            {
                netID = identity.netID,
            };

            targetConnection.Send(message);
        }

    }
    #endregion
    private static bool AddConnection(NetConnectionToClient conn)
    {
        if (conn.connectionID <= 0 || connections.ContainsKey(conn.connectionID))
            return false;
        connections[conn.connectionID] = conn;
        return true;
    }
    public static bool RemoveConnection(int connectionID) =>
        connections.Remove(connectionID);
}
