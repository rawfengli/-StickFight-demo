using System;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;
using static NetRoomManager;

[DefaultExecutionOrder(-1)]
public class NetStickManManager : NetBehaviour
{
    private static Type type = typeof(NetStickManManager);
    private uint _roomID;
    public uint roomID
    {
        get
        {
            if (NetServer.active)
            {
                return _roomID;
            }
            else
            {
                return GameRoomData.room.roomID;
            }
        }
    }
    private NetIdentity netIdentity;
    public bool isPlayerSelf
    {
        get
        {
            if(NetClient.connection != null && netIdentity.isOwned)
                return true;
            else
                return false;
        }
    }
    public bool IsClientPlayer => NetServer.active == false && NetClient.ready;

    public bool playerNetReady => NetDataReady;
    public int playerIndex;
    private bool NetDataReady;

    #region Network
    static NetStickManManager()
    {
#if UNITY_EDITOR || UNITY_SERVER

        RemoteProcedureCalls.RegisterClientCommand(type, "AnswerPlayerReady", InvokeAnswerPlayerReady);
        RemoteProcedureCalls.RegisterClientCommand(type, "DoJump", InvokeDoJump);
        RemoteProcedureCalls.RegisterClientCommand(type, "Move", InvokeMove);
        RemoteProcedureCalls.RegisterClientCommand(type, "DragBody", InvokeDragBody);
        RemoteProcedureCalls.RegisterClientCommand(type, "RotateAimer", InvokeRotateAimer);
        RemoteProcedureCalls.RegisterClientCommand(type, "UseWeapon", InvokeUseWeapon);
#endif

#if UNITY_EDITOR || !UNITY_SERVER
        RemoteProcedureCalls.RegisterServerCommand(type, "ReceiveHit", InvokeReceiveHit, false);
        RemoteProcedureCalls.RegisterServerCommand(type, "SyncPlayerInfo", InvokeSyncPlayerInfo, false);
        RemoteProcedureCalls.RegisterServerCommand(type, "SyncPlayerBaseInfo", InvokeSyncPlayerBaseInfo, false);
        RemoteProcedureCalls.RegisterServerCommand(type, "HoldWeapon", InvokeHoldWeapon, false);
        RemoteProcedureCalls.RegisterServerCommand(type, "ReceiveJump", InvokeReceiveJump, false);
        RemoteProcedureCalls.RegisterServerCommand(type, "ReceiveUseWeapon", InvokeReceiveUseWeapon, false);
        RemoteProcedureCalls.RegisterServerCommand(type, "ReceiveHandAttack", InvokeReceiveHandAttack, false);
#endif

    }

    #region Message
    private struct AnswerPlayerReadyData : INetTransportData<AnswerPlayerReadyData>
    {
        public uint roomID;
        public int playerIndex;
        public Action<NetWriter, AnswerPlayerReadyData> write =>
            (writer, para) =>
            {
                writer.Write<uint>(para.roomID);
                writer.Write<int>(para.playerIndex);

            };

        public Func<NetReader, AnswerPlayerReadyData> read =>
            (reader) =>
            {
                AnswerPlayerReadyData para = new AnswerPlayerReadyData();
                para.roomID = reader.Read<uint>();
                para.playerIndex = reader.Read<int>();
                return para;
            };
    }
    private struct DoJumpNetdata : INetTransportData<DoJumpNetdata>
    {
        public uint roomID;
        public Action<NetWriter, DoJumpNetdata> write =>
            (writer, para) =>
            {
                writer.Write(para.roomID);
            };

        public Func<NetReader, DoJumpNetdata> read =>
            (reader) =>
            {
                DoJumpNetdata para = new DoJumpNetdata();
                para.roomID = reader.Read<uint>();
                return para;
            };
    }
    private struct ReceiveJumpNetdata : INetTransportData<ReceiveJumpNetdata>
    {
        public Action<NetWriter, ReceiveJumpNetdata> write => (writer, para) => { };
        public Func<NetReader, ReceiveJumpNetdata> read => (reader) => new ReceiveJumpNetdata();
    }
    private struct DoRotateAimerNetdata : INetTransportData<DoRotateAimerNetdata>
    {
        public Vector3 mousePos;
        public Action<NetWriter, DoRotateAimerNetdata> write =>
            (writer, para) =>
            {
                writer.Write<Vector3>(para.mousePos);
            };
        public Func<NetReader, DoRotateAimerNetdata> read =>
            (reader) =>
            {
                DoRotateAimerNetdata para = new DoRotateAimerNetdata();
                para.mousePos = reader.Read<Vector3>();
                return para;
            };
    }
    private struct HoldWeaponNetData : INetTransportData<HoldWeaponNetData>
    {
        public uint netID;
        public Action<NetWriter, HoldWeaponNetData> write =>
            (writer, para) =>
            {
                writer.Write<uint>(para.netID);
            };
        public Func<NetReader, HoldWeaponNetData> read =>
            (reader) =>
            {
                HoldWeaponNetData para = new HoldWeaponNetData();
                para.netID = reader.Read<uint>();
                return para;
            };
    }
    private struct UseWeaponNetData : INetTransportData<UseWeaponNetData>
    {
        public uint roomID;
        public Vector3 mousePos;
        public Action<NetWriter, UseWeaponNetData> write =>
            (writer, para) =>
            {
                writer.Write<uint>(para.roomID);
                writer.Write<Vector3>(para.mousePos);
            };

        public Func<NetReader, UseWeaponNetData> read =>
            (reader) =>
            {
                UseWeaponNetData para = new UseWeaponNetData();
                para.roomID = reader.Read<uint>();
                para.mousePos = reader.Read<Vector3>();
                return para;
            };
    }
    private struct ReceiveUseWeaponNetData : INetTransportData<ReceiveUseWeaponNetData>
    {
        public Action<NetWriter, ReceiveUseWeaponNetData> write => (writer, para) => { };

        public Func<NetReader, ReceiveUseWeaponNetData> read => (reader) => new ReceiveUseWeaponNetData();
    }
    public struct ReceiveHandAttackNetData : INetTransportData<ReceiveHandAttackNetData>
    {
        public Vector3 handStartPos;
        public Vector3 dir;

        public Action<NetWriter, ReceiveHandAttackNetData> write =>
            (writer, para) =>
            {
                writer.Write<Vector3>(para.handStartPos);
                writer.Write<Vector3>(para.dir);
            };

        public Func<NetReader, ReceiveHandAttackNetData> read =>
            (reader) =>
            {
                ReceiveHandAttackNetData para = new ReceiveHandAttackNetData();

                para.handStartPos = reader.Read<Vector3>();
                para.dir = reader.Read<Vector3>();

                return para;
            };
    }
    private struct DoMoveNetdata : INetTransportData<DoMoveNetdata>
    {
        public int dir;
        public Action<NetWriter, DoMoveNetdata> write =>
            (writer, para) =>
            {
                writer.Write(para.dir);
            };

        public Func<NetReader, DoMoveNetdata> read =>
            (reader) =>
            {
                DoMoveNetdata para = new DoMoveNetdata();
                para.dir = reader.Read<int>();

                return para;
            };
    }
    private struct DoDragBodyNetdata : INetTransportData<DoDragBodyNetdata>
    {
        public Action<NetWriter, DoDragBodyNetdata> write => (writer, para) => { };
        public Func<NetReader, DoDragBodyNetdata> read => (reader) => new DoDragBodyNetdata();
    }
    private struct ReceiveHitNetdata : INetTransportData<ReceiveHitNetdata>
    {
        public Vector3 damageDir;
        public Vector3 hitPos;
        public byte hitType;
        public Action<NetWriter, ReceiveHitNetdata> write =>
            (writer, para) =>
            {
                writer.Write(para.damageDir);
                writer.Write(para.hitPos);
                writer.Write(para.hitType);
            };
        public Func<NetReader, ReceiveHitNetdata> read =>
            (reader) =>
            {
                ReceiveHitNetdata para = new ReceiveHitNetdata();
                para.damageDir = reader.Read<Vector3>();
                para.hitPos = reader.Read<Vector3>();
                para.hitType = reader.Read<byte>();
                return para;
            };
    }
    private struct SyncPlayerBaseInfoNetData : INetTransportData<SyncPlayerBaseInfoNetData>
    {
        public int playerIndex;
        public Action<NetWriter, SyncPlayerBaseInfoNetData> write =>
            (writer, para) =>
            {
                writer.Write(para.playerIndex);
            };
        public Func<NetReader, SyncPlayerBaseInfoNetData> read =>
            (reader) =>
            {
                SyncPlayerBaseInfoNetData para = new SyncPlayerBaseInfoNetData();

                para.playerIndex = reader.Read<int>();

                return para;
            };
    }

    private struct SyncPlayerInfoNetData : INetTransportData<SyncPlayerInfoNetData>
    {
        public int hp;
        public Action<NetWriter, SyncPlayerInfoNetData> write =>
            (writer, para) =>
            {
                writer.Write(para.hp);
            };
        public Func<NetReader, SyncPlayerInfoNetData> read =>
            (reader) =>
            {
                SyncPlayerInfoNetData para = new SyncPlayerInfoNetData();

                para.hp = reader.Read<int>();

                return para;
            };
    }
    #endregion

#if UNITY_EDITOR || !UNITY_SERVER//Client
    #region Cmd

    public void CmdAnswerPlayerReady()
    {
        AnswerPlayerReadyData data = new AnswerPlayerReadyData();
        data.roomID = GameRoomData.room.roomID;
        data.playerIndex = GameRoomData.room.selfIndex;

        string functionName = "AnswerPlayerReady";
        SendCommandToServer<AnswerPlayerReadyData>(type, functionName, data, (int)Channels.Reliable);
    }
    public void CmdDoJump()
    {
        DoJumpNetdata data = new();
        data.roomID = GameRoomData.room.roomID;

        string functionName = "DoJump";
        SendCommandToServer<DoJumpNetdata>(type, functionName, data, (int)Channels.Reliable);
    }
    public void CmdRotateAimer(Vector3 mousePos)
    {
        DoRotateAimerNetdata data = new();
        data.mousePos = mousePos;

        string functionName = "RotateAimer";
        SendCommandToServer<DoRotateAimerNetdata>(type, functionName, data, (int)Channels.Unreliable);
    }
    public void CmdUseWeapon(Vector3 mousePos)
    {
        UseWeaponNetData data = new();
        data.roomID = GameRoomData.room.roomID;
        data.mousePos = mousePos;

        string functionName = "UseWeapon";
        SendCommandToServer<UseWeaponNetData>(type, functionName, data, (int)Channels.Reliable);
    }
    public void CmdMove(int dir)
    {
        DoMoveNetdata data = new();
        data.dir = dir;

        string functionName = "Move";
        SendCommandToServer<DoMoveNetdata>(type, functionName, data, (int)Channels.Unreliable);
    }
    public void CmdDragBody()
    {
        DoDragBodyNetdata data = new();

        string functionName = "DragBody";
        SendCommandToServer<DoDragBodyNetdata>(type, functionName, data, (int)Channels.Reliable);
    }
    #endregion

    #region Invoke
    private static void InvokeReceiveJump(NetBehaviour behaviour, NetReader reader, NetConnectionToClient conn)
    {
        NetTransportData.Read(reader, out ReceiveJumpNetdata para);
        ((NetStickManManager)behaviour).ReceiveJump();
    }
    void ReceiveJump()
    {
        playerController.ReceiveJump();
    }
    //----------------------------------------------------------------------------------------
    private static void InvokeHoldWeapon(NetBehaviour behaviour, NetReader reader, NetConnectionToClient conn)
    {
        NetTransportData.Read(reader, out HoldWeaponNetData para);
        ((NetStickManManager)behaviour).HoldWeapon(para);
    }
    void HoldWeapon(HoldWeaponNetData data)
    {
        uint netID = data.netID;

        NetClient.TryGetNetworkObject(netID, out GameObject go);
        if (go.TryGetComponent<Weapon>(out Weapon weapon))
        {
            fightAction.HoldWeaponClient(weapon);
        }
    }
    //----------------------------------------------------------------------------------------
    private static void InvokeReceiveUseWeapon(NetBehaviour behaviour, NetReader reader, NetConnectionToClient conn)
    {
        NetTransportData.Read(reader, out ReceiveUseWeaponNetData para);
        ((NetStickManManager)behaviour).ReceiveUseWeapon(para);
    }
    void ReceiveUseWeapon(ReceiveUseWeaponNetData data)
    {
        fightAction.ReceiveUseWeapon();
    }
    //----------------------------------------------------------------------------------------
    private static void InvokeReceiveHandAttack(NetBehaviour behaviour, NetReader reader, NetConnectionToClient conn)
    {
        NetTransportData.Read(reader, out ReceiveHandAttackNetData para);
        ((NetStickManManager)behaviour).ReceiveHandAttack(para);
    }
    void ReceiveHandAttack(ReceiveHandAttackNetData data)
    {
        fightAction.ReceiveHandAttack(data.handStartPos, data.dir);
    }
    //----------------------------------------------------------------------------------------
    private static void InvokeReceiveHit(NetBehaviour behaviour, NetReader reader, NetConnectionToClient conn)
    {
        NetTransportData.Read(reader, out ReceiveHitNetdata para);
        ((NetStickManManager)behaviour).ReceiveHit(para);
    }
    void ReceiveHit(ReceiveHitNetdata para)
    {
        fightAction.ReceiveHit(para.damageDir, para.hitPos, (HitType)para.hitType);
    }
    //----------------------------------------------------------------------------------------
    private static void InvokeSyncPlayerInfo(NetBehaviour behaviour, NetReader reader, NetConnectionToClient conn)
    {
        NetTransportData.Read(reader, out SyncPlayerInfoNetData para);
        ((NetStickManManager)behaviour).SyncPlayerInfo(para);
    }

    void SyncPlayerInfo(SyncPlayerInfoNetData para)
    {
        playerData.SetHp(para.hp);
    }
    //----------------------------------------------------------------------------------------
    private static void InvokeSyncPlayerBaseInfo(NetBehaviour behaviour, NetReader reader, NetConnectionToClient conn)
    {
        NetTransportData.Read(reader, out SyncPlayerBaseInfoNetData para);
        ((NetStickManManager)behaviour).SyncPlayerBaseInfo(para);
    }

    void SyncPlayerBaseInfo(SyncPlayerBaseInfoNetData para)
    {
        playerIndex = para.playerIndex;
    }
    #endregion
#endif

#if UNITY_EDITOR || UNITY_SERVER
    #region Invoke
    private static void InvokeAnswerPlayerReady(NetBehaviour behaviour, NetReader reader, NetConnectionToClient conn)
    {
        NetTransportData.Read(reader, out AnswerPlayerReadyData para);
        ((NetStickManManager)behaviour).AnswerPlayerReady(para);
    }
    void AnswerPlayerReady(AnswerPlayerReadyData data)
    {
        RoomServerManager.Instance.PlayerInGameReady(data.roomID, data.playerIndex);
    }
    //----------------------------------------------------------------------------------------
    private static void InvokeDoJump(NetBehaviour behaviour, NetReader reader, NetConnectionToClient conn)
    {
        NetTransportData.Read(reader, out DoJumpNetdata para);
        ((NetStickManManager)behaviour).DoJump(para);
    }
    void DoJump(DoJumpNetdata para)
    {
        if (RoomServerManager.Instance.TryGetRoom(para.roomID, out ServerRoom room) &&
            playerController.Jump())
        {
            foreach (var conn in room.players)
            {
                if (conn != null)
                {
                    CmdReceiveJump(conn);
                }
            }
        }
    }
    //----------------------------------------------------------------------------------------
    private static void InvokeRotateAimer(NetBehaviour behaviour, NetReader reader, NetConnectionToClient conn)
    {
        NetTransportData.Read(reader, out DoRotateAimerNetdata para);
        ((NetStickManManager)behaviour).RotateAimer(para);
    }
    void RotateAimer(DoRotateAimerNetdata para)
    {
        fightAction.RotateAim(para.mousePos);
    }
    //----------------------------------------------------------------------------------------
    private static void InvokeUseWeapon(NetBehaviour behaviour, NetReader reader, NetConnectionToClient conn)
    {
        NetTransportData.Read(reader, out UseWeaponNetData para);
        ((NetStickManManager)behaviour).UseWeapon(para);
    }
    void UseWeapon(UseWeaponNetData data)
    {
        if (RoomServerManager.Instance.TryGetRoom(data.roomID, out ServerRoom room))
        {
            if (fightAction.holdWeapon)
            {
                if (fightAction.UseWeapon(data.mousePos))
                {
                    foreach (var conn in room.players)
                    {
                        if (conn != null)
                        {
                            CmdReceiveUseWeapon(conn);
                        }
                    }
                }
            }
            else
            {
                if (fightAction.HandAttack(data.mousePos, out Vector3 handStartPos, out Vector3 dir))
                {
                    foreach (var conn in room.players)
                    {
                        if (conn != null)
                        {
                            CmdReceiveHandAttack(conn, handStartPos, dir);
                        }
                    }
                }
            }

        }
    }
    //----------------------------------------------------------------------------------------
    private static void InvokeMove(NetBehaviour behaviour, NetReader reader, NetConnectionToClient conn)
    {
        NetTransportData.Read(reader, out DoMoveNetdata para);
        ((NetStickManManager)behaviour).Move(para);
    }
    void Move(DoMoveNetdata para)
    {
        movingAction.DoMove(para.dir);
    }
    //----------------------------------------------------------------------------------------
    private static void InvokeDragBody(NetBehaviour behaviour, NetReader reader, NetConnectionToClient conn)
    {
        NetTransportData.Read(reader, out DoDragBodyNetdata para);
        ((NetStickManManager)behaviour).DragBody();
    }
    void DragBody()
    {
        standingAction.Drag();
    }

    #endregion

    #region Cmd
    public void CmdReceiveJump(NetConnectionToClient conn)
    {
        ReceiveJumpNetdata data = new();

        string functionName = "ReceiveJump";
        SendCommandToTargetClient(conn, type, functionName, data, (int)Channels.Reliable);
    }
    public void CmdHoldWeapon(uint roomID, uint netID)
    {
        if (RoomServerManager.Instance.TryGetRoom(roomID, out ServerRoom room))
        {
            string functionName = "HoldWeapon";
            HoldWeaponNetData data = new HoldWeaponNetData();
            data.netID = netID;
            foreach (var conn in room.players)
            {
                if (conn == null)
                    continue;

                SendCommandToTargetClient(
                    conn,
                    type,
                    functionName,
                    data,
                    (int)Channels.Reliable);
            }
        }
    }
    public void CmdReceiveUseWeapon(NetConnectionToClient conn)
    {
        ReceiveUseWeaponNetData data = new();

        string functionName = "ReceiveUseWeapon";
        SendCommandToTargetClient(conn, type, functionName, data, (int)Channels.Reliable);
    }
    public void CmdReceiveHandAttack(NetConnectionToClient conn, Vector3 handMousePos, Vector3 dir)
    {
        ReceiveHandAttackNetData data = new();
        data.handStartPos = handMousePos;
        data.dir = dir;

        string functionName = "ReceiveHandAttack";
        SendCommandToTargetClient(conn, type, functionName, data, (int)Channels.Reliable);
    }
    public void CmdReceiveHit(uint roomID, Vector3 damageDir, Vector3 hitPos, HitType hitType)
    {
        ReceiveHitNetdata data = new();
        data.damageDir = damageDir;
        data.hitPos = hitPos;
        data.hitType = (byte)hitType;

        string functionName = "ReceiveHit";
        if (RoomServerManager.Instance.TryGetRoom(roomID, out ServerRoom room))
        {
            foreach (var conn in room.players)
            {
                if (conn == null)
                    continue;

                SendCommandToTargetClient<ReceiveHitNetdata>(conn, type, functionName, data, (int)Channels.Reliable);
            }
        }
    }
    public void CmdSyncPlayerInfo(uint roomID)
    {
        SyncPlayerInfoNetData data = new();
        data.hp = playerData.Hp;

        string functionName = "SyncPlayerInfo";
        if (RoomServerManager.Instance.TryGetRoom(roomID, out ServerRoom room))
        {
            foreach (var conn in room.players)
            {
                if (conn == null)
                    continue;

                SendCommandToTargetClient<SyncPlayerInfoNetData>(
                    conn, type, functionName, data, (int)Channels.Unreliable);
            }
        }
        else
        {
            Debug.Log("Room Id not found,it it fine. room data may not init. ID : " + roomID.ToString());
        }
    }
    public void CmdSyncPlayerBaseInfo()
    {
        SyncPlayerBaseInfoNetData data = new();
        data.playerIndex = playerIndex;

        string functionName = "SyncPlayerBaseInfo";
        if (RoomServerManager.Instance.TryGetRoom(roomID, out ServerRoom room))
        {
            foreach (var conn in room.players)
            {
                if (conn == null)
                    continue;

                SendCommandToTargetClient<SyncPlayerBaseInfoNetData>(
                    conn, type, functionName, data, (int)Channels.Reliable);
            }
        }
    }


    #endregion

#endif
    #endregion

    #region Init
    public GameObject playerObj;
    public PlayerData playerData;
    public PlayerController playerController;
    public Standing standingAction;
    public Movement movingAction;
    public Fight fightAction;
    private bool Inited;

    private bool hasSendReadyMsg;

    private void Awake()
    {
        netIdentity = GetComponent<NetIdentity>();
        Inited = false;
        NetDataReady = false;
        hasSendReadyMsg = false;

        playerData = GetComponent<PlayerData>();
        standingAction = GetComponent<Standing>();
        movingAction = GetComponent<Movement>();
        playerController = GetComponent<PlayerController>();
        fightAction = GetComponent<Fight>();

    }
#if UNITY_EDITOR || UNITY_SERVER
    public void SetNetData(int playerIndex, uint roomID)
    {
        this.playerIndex = playerIndex;
        this._roomID = roomID;
        CmdSyncPlayerBaseInfo();
    }
    private void NetworkServerInit()
    {
        if (Inited)
            return;
        playerData.ResetPlayerData();
        Inited = true;

    }
#endif

#if UNITY_EDITOR || !UNITY_SERVER
    public void OnGameHasStarted()
    {
        NetDataReady = true;
    }
    private void NetworkClientInit()
    {
        if (Inited)
            return;
        if(hasSendReadyMsg == false)
        {
            CmdAnswerPlayerReady();
            hasSendReadyMsg = true;
        }
        if (playerNetReady)
        {
            SetMaterial();
            Inited = true;
        }
    }
#endif

#if UNITY_EDITOR || UNITY_SERVER
    private void IfOutOfRange()
    {
        if (MathF.Abs(playerObj.transform.position.x) > 80 || playerObj.transform.position.y < -50)
            playerData.Die();
    }
    public void OnPlayerDie()
    {
        if(RoomServerManager.Instance.TryGetRoom(roomID, out ServerRoom room))
        {
            room.managerReference.OnPlayerDie(playerIndex, room);
        }
    }
#endif

    public void Update()
    {
        if(NetServer.active)
        {
#if UNITY_EDITOR || UNITY_SERVER
            NetworkServerInit();
#endif
        }
        else
        {
#if UNITY_EDITOR || !UNITY_SERVER
            NetworkClientInit();
#endif
        }
    }

    #endregion
    #region Common
    private void SetMaterial()
    {
        if (IsClientPlayer == false)
            return;

        void FindAllObjects(Transform parent)
        {
            foreach (Transform child in parent)
            {
                if (child.TryGetComponent<MeshRenderer>(out MeshRenderer meshRenderer))
                {
                    meshRenderer.material = PlayerMaterialsManager.Instance[playerIndex];
                }
                FindAllObjects(child);
            }
        }
        FindAllObjects(transform);
    }

    public void FixedUpdate()
    {
        if(IsClientPlayer == false)
        {
#if UNITY_EDITOR || UNITY_SERVER
            CmdSyncPlayerInfo(roomID);
            IfOutOfRange();
#endif
        }
        else
        {
            if (playerNetReady == false)
                return;

            OnPlayerLoopInfo info = new();
            info.playerIndex = playerIndex;
            info.hp = playerData.Hp;
            info.hpPercent = (float)playerData.Hp / (float)playerData.MaxHp;

            InGameEventBus.Instance.InvokePlayerLoop(info);
        }
    }

    private void OnEnable()
    {
#if UNITY_EDITOR || !UNITY_SERVER
        InGameEventBus.Instance.Register_GameHasStarted_Event(OnGameHasStarted);
#endif
#if UNITY_EDITOR || UNITY_SERVER
        playerController.RegisterOnPlayerDieEvent(OnPlayerDie);
#endif
    }
    private void OnDisable()
    {
#if UNITY_EDITOR || !UNITY_SERVER
        InGameEventBus.Instance.Unregister_GameHasStarted_Event(OnGameHasStarted);
#endif
#if UNITY_EDITOR || UNITY_SERVER
        playerController.UnregisterOnPlayerDieEvent(OnPlayerDie);
#endif
    }

    #endregion
}
