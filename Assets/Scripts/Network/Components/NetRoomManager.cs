using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEditor.Rendering;
using UnityEngine;
using static Unity.Burst.Intrinsics.X86.Avx;

public enum JoinedRoomStatus : byte
{
    Unkown = 0,
    Succeeded = 1,
    RoomNotExist = 2,
    Full = 3
}


public class NetRoomManager : NetBehaviour
{
    private readonly static Type type = typeof(NetRoomManager);

    [SerializeField] bool OnServer;
    #region Message
    
    private struct JoinRoomData : INetTransportData<JoinRoomData>
    {
        public uint targetRoomID;
        public int avatarIndex;
        public string name;
        public Action<NetWriter, JoinRoomData> write => 
            (writer, para) => 
            {
                writer.Write<uint>(para.targetRoomID);
                writer.Write<int>(para.avatarIndex);
                writer.Write<string>(para.name);
            };
        public Func<NetReader, JoinRoomData> read =>
            (reader) =>
            {
                JoinRoomData para = new JoinRoomData();
                para.targetRoomID = reader.Read<uint>();
                para.avatarIndex = reader.Read<int>();
                para.name = reader.Read<string>();
                return para;
            };
    }
    public struct CreateRoomData : INetTransportData<CreateRoomData>
    {
        public int avatarID;
        public string playerName;
        public Action<NetWriter, CreateRoomData> write =>
            (writer, para) =>
            {
                writer.Write<int>(para.avatarID);
                writer.Write<string>(para.playerName);
            };

        public Func<NetReader, CreateRoomData> read =>
            (reader) =>
            {
                CreateRoomData para = new CreateRoomData();
                para.avatarID = reader.Read<int>();
                para.playerName = reader.Read<string>();
                return para;
            };
    }
    public struct StartGameData : INetTransportData<StartGameData>
    {
        public uint roomID;
        public Action<NetWriter, StartGameData> write =>
            (writer, para) =>
            {
                writer.Write<uint>(para.roomID);
            };

        public Func<NetReader, StartGameData> read =>
            (reader) =>
            {
                StartGameData para = new StartGameData();

                para.roomID = reader.Read<uint>();
                return para;
            };
    }
    public struct ReceiveStartGameData : INetTransportData<ReceiveStartGameData>
    {
        public Action<NetWriter, ReceiveStartGameData> write => (writer, para) => { };
        public Func<NetReader, ReceiveStartGameData> read => (reader) => new ReceiveStartGameData();
    }
    public struct RoomCreatedNetData : INetTransportData<RoomCreatedNetData>
    {
        public uint roomID;
        public byte playerIndex;
        public Action<NetWriter, RoomCreatedNetData> write =>
            (writer, para) =>
            {
                writer.Write<uint>(para.roomID);
                writer.Write<byte>(para.playerIndex);

            };

        public Func<NetReader, RoomCreatedNetData> read =>
            (reader) =>
            {
                RoomCreatedNetData para = new();
                para.roomID = reader.Read<uint>();
                para.playerIndex = reader.Read<byte>();

                return para;
            };
    }
    public struct NewPlayerNetData : INetTransportData<NewPlayerNetData>
    {
        public uint roomID;

        public byte newPlayerIndex;
        public bool isSelf;
        public JoinedRoomStatus status;

        public int[] otherPlayerAvatarIndexs;//4 + 1
        public string[] otherPlayerNames;//4 + 1

        public int newPlayerAvatarIndex;
        public string newPlayerName;

        public NewPlayerNetData(
            uint roomID, byte playerIndex, bool isSelf,
            JoinedRoomStatus status,
            int newPlayerAvatarIndex = 0,
            string newPlayerName = null)
        {
            this.roomID = roomID;
            this.newPlayerIndex = playerIndex;
            this.isSelf = isSelf;
            this.status = status;

            if (isSelf)
            {
                otherPlayerAvatarIndexs = new int[GameRoomData.MAX_PLAYER_COUNT + 1];
                otherPlayerNames = new string[GameRoomData.MAX_PLAYER_COUNT + 1];
                this.newPlayerAvatarIndex = 0;
                this.newPlayerName = string.Empty;
            }
            else
            {
                otherPlayerAvatarIndexs = null;
                otherPlayerNames = null;

                this.newPlayerAvatarIndex = newPlayerAvatarIndex;
                this.newPlayerName = newPlayerName;
            }
        }

        public Action<NetWriter, NewPlayerNetData> write =>
            (writer, para) =>
            {
                writer.Write<uint>(para.roomID);
                writer.Write<byte>(para.newPlayerIndex);
                writer.Write<bool>(para.isSelf);
                writer.Write<byte>((byte)para.status);

                if(para.status == JoinedRoomStatus.Succeeded)
                {
                    if (para.isSelf)
                    {
                        writer.WriteArray<int>(para.otherPlayerAvatarIndexs);
                        writer.WriteArray<string>(para.otherPlayerNames);
                    }
                    else
                    {
                        writer.Write<int>(para.newPlayerAvatarIndex);
                        writer.Write<string>(para.newPlayerName);
                    }
                }
            };

        public Func<NetReader, NewPlayerNetData> read =>
            (reader) =>
            {
                NewPlayerNetData para = new();
                para.roomID = reader.Read<uint>();
                para.newPlayerIndex = reader.Read<byte>();
                para.isSelf = reader.Read<bool>();
                para.status = (JoinedRoomStatus)reader.Read<byte>();

                if(para.status == JoinedRoomStatus.Succeeded)
                {
                    if (para.isSelf)
                    {
                        para.otherPlayerAvatarIndexs = reader.ReadArray<int>();
                        para.otherPlayerNames = reader.ReadArray<string>();
                    }
                    else
                    {
                        para.newPlayerAvatarIndex = reader.Read<int>();
                        para.newPlayerName = reader.Read<string>();
                    }
                }
                return para;
            };
    }
    public struct OtherPlayerExitedNetData : INetTransportData<OtherPlayerExitedNetData>
    {
        public uint roomID;
        public byte playerIndex;
        public Action<NetWriter, OtherPlayerExitedNetData> write =>
            (writer, para) =>
            {
                writer.Write<uint>(para.roomID);
                writer.Write<byte>(para.playerIndex);
            };

        public Func<NetReader, OtherPlayerExitedNetData> read =>
            (reader) =>
            {
                OtherPlayerExitedNetData para = new();
                
                para.roomID += reader.Read<uint>();
                para.playerIndex += reader.Read<byte>();

                return para;
            };
    }
    /// <summary>
    /// 我们这里设定必须客户端等到服务端响应后才在客户端侧正式退出
    /// 尽管这样任然会存在如果客户端发出退出，但服务器器已经启动一个局内游戏的情况
    /// 但是这样只需要单独判断即可
    /// </summary>
    public struct AnswerExitRequestData : INetTransportData<AnswerExitRequestData>
    {
        public Action<NetWriter, AnswerExitRequestData> write => (writer, para) => { };
        public Func<NetReader, AnswerExitRequestData> read => (reader) => new AnswerExitRequestData();
    }
    #endregion

    #region Network
    public const int PLAYER_LIMIT = 4;

    static NetRoomManager()
    {
#if UNITY_EDITOR || UNITY_SERVER

        RemoteProcedureCalls.RegisterClientCommand(type, "CreateRoom", InvokeCreateRoom);
        RemoteProcedureCalls.RegisterClientCommand(type, "JoinRoom", InvokeJoinRoom);
        RemoteProcedureCalls.RegisterClientCommand(type, "StartGame", InvokeStartGame);
#endif

#if UNITY_EDITOR || !UNITY_SERVER
        RemoteProcedureCalls.RegisterServerCommand(type, "RoomCreated", InvokeRoomCreated, false);
        RemoteProcedureCalls.RegisterServerCommand(type, "NewPlayerJoin", InvokeNewPlayerJoin, false);
        RemoteProcedureCalls.RegisterServerCommand(type, "ReceiveStartGame", InvokeReceiveStartGame, false);
#endif
    }

#if UNITY_EDITOR || !UNITY_SERVER

    #region Cmd

    public void CmdCreateRoom()
    {
        CreateRoomData data = new CreateRoomData();

        data.playerName = PlayerInfo.playerName;
        data.avatarID = PlayerInfo.avatarIndex;

        string functionName = "CreateRoom";
        SendCommandToServer<CreateRoomData>(type, functionName, data, (int)Channels.Reliable);
    }

    public void CmdJoinRoom(uint roomID)
    {
        JoinRoomData data = new JoinRoomData();
        data.targetRoomID = roomID;
        data.name = PlayerInfo.playerName;
        data.avatarIndex = PlayerInfo.avatarIndex;

        string functionName = "JoinRoom";
        SendCommandToServer<JoinRoomData>(type, functionName, data, (int)Channels.Reliable);
    }

    public void CmdStartGame(uint roomID)
    {
        StartGameData data = new StartGameData();
        data.roomID = roomID;
        string functionName = "StartGame";
        SendCommandToServer<StartGameData>(type, functionName, data, (int)Channels.Reliable);
    }

    #endregion

    #region Invoke

    private static void InvokeRoomCreated(NetBehaviour behaviour, NetReader reader, NetConnectionToClient conn)
    {
        NetTransportData.Read(reader, out RoomCreatedNetData para);
        ((NetRoomManager)behaviour).RoomCreated(para);
    }
    void RoomCreated(RoomCreatedNetData data)
    {
        EventBus.InvokeRoomCreatedEvents(data);
    }

    //-----------------------------------------------------------------------------------------------

    private static void InvokeNewPlayerJoin(NetBehaviour behaviour, NetReader reader, NetConnectionToClient conn)
    {
        NetTransportData.Read(reader, out NewPlayerNetData para);
        ((NetRoomManager)behaviour).NewPlayerJoin(para);
    }
    void NewPlayerJoin(NewPlayerNetData para)
    {
        if (para.isSelf)
            EventBus.InvokeRoomJoinedEvents(para);
        else
            EventBus.InvokeNewPlayerJoinedEvents(para);
    }
    //-----------------------------------------------------------------------------------------------
    private static void InvokeReceiveStartGame(NetBehaviour behaviour, NetReader reader, NetConnectionToClient conn)
    {
        NetTransportData.Read(reader, out ReceiveStartGameData para);
        ((NetRoomManager)behaviour).ReceiveStartGame(para);
    }
    void ReceiveStartGame(ReceiveStartGameData data)
    {

        EventBus.InvokeReceiveStartGameEvents(data);
    }
    #endregion


#endif

#if UNITY_EDITOR || UNITY_SERVER
    #region Invoke

    private static void InvokeCreateRoom(NetBehaviour behaviour, NetReader reader, NetConnectionToClient conn)
    {
        NetTransportData.Read(reader, out CreateRoomData para);
        ((NetRoomManager)behaviour).CreateRoom(para, conn);
    }

    void CreateRoom(CreateRoomData data, NetConnectionToClient conn)
    {
        StartCoroutine(ReceiveCreatePlayerCoroutine(conn, data));
    }
    IEnumerator ReceiveCreatePlayerCoroutine(NetConnectionToClient conn, CreateRoomData data)
    {
        Task<ServerRoom> task = RoomServerManager.Instance.CreateNewRoom(conn, data);

        while (!task.IsCompleted)
            yield return null;

        CmdRoomCreated(task.Result, conn);
    }
    //-----------------------------------------------------------------------------------------------

    private static void InvokeJoinRoom(NetBehaviour behaviour, NetReader reader, NetConnectionToClient conn)
    {
        NetTransportData.Read(reader, out JoinRoomData para);
        ((NetRoomManager)behaviour).JoinRoom(para, conn);
    }
    void JoinRoom(JoinRoomData data, NetConnectionToClient conn)
    {
        uint roomID = data.targetRoomID;
        int avatarIndex = data.avatarIndex;
        string name = data.name;

        RoomServerManager.Instance.JoinRoom(roomID, avatarIndex, name, conn, out int newPlayerIndex, out ServerRoom room, out JoinedRoomStatus status);

        if (status == JoinedRoomStatus.Succeeded)
        {
            for (int i = 1; i < room.players.Length; i++)
            {
                if (room.players[i] == null)
                    continue;

                bool isSelf;
                NewPlayerNetData para;

                if (room.players[i] == conn)
                {
                    isSelf = true;
                    para = new NewPlayerNetData(roomID, (byte)newPlayerIndex, isSelf, status);
                }
                else
                {
                    isSelf = false;
                    para = new NewPlayerNetData(roomID, (byte)newPlayerIndex, isSelf, status, data.avatarIndex, name);
                }

                if (isSelf)
                {
                    para.otherPlayerAvatarIndexs = room.avatarIndexs;
                    para.otherPlayerNames = room.names;
                }
                CmdNewPlayerJoin(para, room.players[i]);
            }
        }
        else
        {
            NewPlayerNetData para = new NewPlayerNetData()
            {
                roomID = roomID,
                newPlayerIndex = 0,
                isSelf = true,
                status = status
            };
            CmdNewPlayerJoin(para, conn);
        }

    }

    //-----------------------------------------------------------------------------------------------

    private static void InvokeStartGame(NetBehaviour behaviour, NetReader reader, NetConnectionToClient conn)
    {
        NetTransportData.Read(reader, out StartGameData para);
        ((NetRoomManager)behaviour).StartGame(para);
    }
    void StartGame(StartGameData data)
    {
        uint roomID = data.roomID;
        if(RoomServerManager.Instance.StartGame(roomID, out ServerRoom room))
        {

            for (int i = 1; i < room.players.Length; i++)
            {
                if (room.players[i] == null)
                    continue;

                ReceiveStartGameData para = new();
                CmdReceiveStartGame(para, room.players[i]);
            }
        }
    }

    #endregion

    #region Cmd
    private void CmdRoomCreated(ServerRoom room, NetConnectionToClient conn)
    {
        RoomCreatedNetData data = new RoomCreatedNetData();

        data.roomID = room.roomID;
        data.playerIndex = 1;

        string functionName = "RoomCreated";
        SendCommandToTargetClient<RoomCreatedNetData>(conn, type, functionName, data, (int)Channels.Reliable);

    }

    private void CmdNewPlayerJoin(NewPlayerNetData para, NetConnectionToClient conn)
    {
        string functionName = "NewPlayerJoin";
        SendCommandToTargetClient<NewPlayerNetData>(conn, type, functionName, para, (int)Channels.Reliable);
    }
    private void CmdReceiveStartGame(ReceiveStartGameData para, NetConnectionToClient conn)
    {
        string functionName = "ReceiveStartGame";
        SendCommandToTargetClient<ReceiveStartGameData>(conn, type, functionName, para, (int)Channels.Reliable);
    }

    #endregion
#endif
    #endregion

    #region Common

    private void Awake()
    {
        identity.SetNetManagerID(NetManagerType.NetRoom, OnServer);
    }
    private void OnDestroy()
    {
        identity.RemoveNetManagerID(NetManagerType.NetRoom, OnServer);
    }
    private void OnEnable()
    {
        if (OnServer)
            return;
#if UNITY_EDITOR || !UNITY_SERVER
        RoomEventManager.Instance.Register_CreateRoom_Event(CmdCreateRoom);
        RoomEventManager.Instance.Register_JoinRoom_Event(CmdJoinRoom);
        RoomEventManager.Instance.Register_StartGame_Event(CmdStartGame);
#endif

    }
    private void OnDisable()
    {
        if (OnServer)
            return;
#if UNITY_EDITOR || !UNITY_SERVER
        RoomEventManager.Instance.Unregister_CreateRoom_Event(CmdCreateRoom);
        RoomEventManager.Instance.Unregister_JoinRoom_Event(CmdJoinRoom);
        RoomEventManager.Instance.Unregister_StartGame_Event(CmdStartGame);
#endif

    }
    #endregion
    private const uint MIN_ROOM_ID = (uint)1e7;
    private const uint MAX_ROOM_ID = (uint)1e8;
    public static bool IsRoomIDVaild(uint id)
    {
        if (1e7 <= id && id < 1e8)
            return true;
        else
            return false;
    }
}
