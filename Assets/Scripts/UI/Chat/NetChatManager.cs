using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Assertions.Must;
using static NetChatManager;

public class ChatRoom
{
    public const int MAX_CONVERSATION_COUNT = 50;

    public uint roomID;
    public ValueTuple<int, string, string>[] conversationLog
        = new ValueTuple<int, string, string>[MAX_CONVERSATION_COUNT];
    public int latestConversationIndex = 0;
    public int nextConversationIndex = 0;
    public int totalConversationCount = 0;

    public void Clear()
    {
        roomID = 0;
        latestConversationIndex = 0;
        nextConversationIndex = 0;
        totalConversationCount = 0;
        for(int i = 0; i < MAX_CONVERSATION_COUNT; i++)
            conversationLog[i] = default;
    }
}
public class NetChatManager : NetBehaviour
{

    [SerializeField] 
    private bool OnServer;
    private ChatRoom room;
    public Action<ReplyChatMessageData> OnReceiveMessage;
    public Action<ChatRoom> OnReceiveHistoricalMessages;
    #region Network

    #region Message Data
    public struct RequestHistoricalMessageData : INetTransportData<RequestHistoricalMessageData>
    {
        public uint roomID;
        public Action<NetWriter, RequestHistoricalMessageData> write
            => (writer, para) =>
            {
                writer.Write<uint>(para.roomID);
            };

        public Func<NetReader, RequestHistoricalMessageData> read
            => (reader) =>
            {
                RequestHistoricalMessageData para = new();
                para.roomID = reader.Read<uint>();
                return para;

            };
    }
    public struct ReplyHistoricalMessageData : INetTransportData<ReplyHistoricalMessageData>
    {
        public int[] playerIndexs;
        public string[] playerNames;
        public string[] conversationLog;

        public Action<NetWriter, ReplyHistoricalMessageData> write => (writer, para) =>
        {
            writer.WriteArray<int>(para.playerIndexs);
            writer.WriteArray<string>(para.playerNames);
            writer.WriteArray<string>(para.conversationLog);
        };
        public Func<NetReader, ReplyHistoricalMessageData> read => (reader) =>
        {
            ReplyHistoricalMessageData para = new();
            para.playerIndexs = reader.ReadArray<int>();
            para.playerNames = reader.ReadArray<string>();
            para.conversationLog = reader.ReadArray<string>();
            return para;
        };
    }
    private struct SendChatMessageData : INetTransportData<SendChatMessageData>
    {
        public int playerIndex;
        public uint roomID;
        public string playerName;
        public string content;
        public Action<NetWriter, SendChatMessageData> write =>
            (writer, para) =>
            {
                writer.Write<int>(para.playerIndex);
                writer.Write<uint>(para.roomID);
                writer.Write<string>(para.playerName);
                writer.Write<string>(para.content);
            };

        public Func<NetReader, SendChatMessageData> read =>
            (reader) =>
            {
                SendChatMessageData para = new();

                para.playerIndex = reader.Read<int>();
                para.roomID = reader.Read<uint>();
                para.playerName = reader.Read<string>();
                para.content = reader.Read<string>();

                return para;
            };
    }
    public struct ReplyChatMessageData : INetTransportData<ReplyChatMessageData>
    {
        public uint roomID;
        public int playerIndex;
        public string playerName;
        public string content;
        public Action<NetWriter, ReplyChatMessageData> write =>
            (writer, para) =>
            {
                writer.Write<uint>(para.roomID);
                writer.Write<int>(para.playerIndex);
                writer.Write<string>(para.playerName);
                writer.Write<string>(para.content);
            };

        public Func<NetReader, ReplyChatMessageData> read =>
            (reader) =>
            {
                ReplyChatMessageData para = new();

                para.roomID = reader.Read<uint>();
                para.playerIndex = reader.Read<int>();
                para.playerName = reader.Read<string>();
                para.content = reader.Read<string>();
                return para;
            };
    }
    #endregion

    static NetChatManager()
    {
        Type type = typeof(NetChatManager);
#if UNITY_EDITOR || UNITY_SERVER
        RemoteProcedureCalls.RegisterClientCommand(type, "SendMessage", InvokeJoinRoom);
        RemoteProcedureCalls.RegisterClientCommand(type, "RequestHistoricalMessage", InvokeRequestMessage);
#endif

#if UNITY_EDITOR || !UNITY_SERVER
        RemoteProcedureCalls.RegisterServerCommand(type, "ReceiveMessage", InvokeReceiveMessage, false);
        RemoteProcedureCalls.RegisterServerCommand(type, "ReplyHistoricalMessage", InvokeReplyHistoricalMessage, false);
#endif
    }
#if UNITY_EDITOR || !UNITY_SERVER//Client
    public void CmdSendMessage(string content)
    {
        SendChatMessageData data = new SendChatMessageData();

        if (GameRoomData.roomAvailable == false)
            return;
        if (content == string.Empty)
        {
            Debug.LogWarning("Client Send Message is empty, send cmd will be skipped");
            return;
        }

        data.playerName = PlayerInfo.playerName;
        data.playerIndex = GameRoomData.room.selfIndex;
        data.roomID = GameRoomData.room.roomID;
        data.content = content;

        string functionName = "SendMessage";
        Type type = typeof(NetChatManager);
        SendCommandToServer<SendChatMessageData>(type, functionName, data, (int)Channels.Reliable);
    }
    public void CmdRequestHistoricalMessage(uint roomID)
    {
        RequestHistoricalMessageData data = new();
        data.roomID = roomID;

        Type type = typeof(NetChatManager);
        string functionName = "RequestHistoricalMessage";
        SendCommandToServer<RequestHistoricalMessageData>(type, functionName, data, (int)Channels.Reliable);
    }
    private static void InvokeReceiveMessage(NetBehaviour behaviour, NetReader reader, NetConnectionToClient _)
    {
        NetTransportData.Read(reader, out ReplyChatMessageData para);
        ((NetChatManager)behaviour).ReceiveMessage(para);

    }
    void ReceiveMessage(ReplyChatMessageData para)
    {
        AddMessage(para.playerIndex, para.playerName, para.content);
        OnReceiveMessage?.Invoke(para);
    }

    private static void InvokeReplyHistoricalMessage(NetBehaviour behaviour, NetReader reader, NetConnectionToClient _)
    {
        NetTransportData.Read(reader, out ReplyHistoricalMessageData para);
        ((NetChatManager)behaviour).ReplyHistoricalMessage(para);
    }

    void ReplyHistoricalMessage(ReplyHistoricalMessageData historicalLog)
    {
        int count = historicalLog.conversationLog.Length;
        for (int i = 0; i < count; i++)
        {
            int index = historicalLog.playerIndexs[i];
            string name = historicalLog.playerNames[i];
            string content = historicalLog.conversationLog[i];
            AddMessage(index, name, content);
        }

        OnReceiveHistoricalMessages?.Invoke(room);
    }
#endif

#if UNITY_EDITOR || UNITY_SERVER//Server

    private static void InvokeJoinRoom(NetBehaviour behaviour, NetReader reader, NetConnectionToClient conn)
    {
        NetTransportData.Read(reader, out SendChatMessageData para);
        ((NetChatManager)behaviour).SendMessage(para);
    }

    void SendMessage(SendChatMessageData para)
    {

        ChatServerManager.Instance.AddMessage(para.roomID, para.playerIndex, para.playerName, para.content);

        ReplyChatMessageData msg = new();

        msg.roomID = para.roomID;
        msg.content = para.content;
        msg.playerName = para.playerName;
        msg.playerIndex = para.playerIndex;

        CmdReceiveMessage(msg);
    }

    //--------------------------------------------------------------------------------------------
    private static void InvokeRequestMessage(NetBehaviour behaviour, NetReader reader, NetConnectionToClient conn)
    {
        NetTransportData.Read(reader, out RequestHistoricalMessageData para);
        ((NetChatManager)behaviour).RequestHistoricalMessage(para, conn);
    }

    void RequestHistoricalMessage(RequestHistoricalMessageData data, NetConnectionToClient conn)
    {
        uint roomID = data.roomID;

        ChatServerManager.Instance.GetHistoricalMessage(roomID, out ReplyHistoricalMessageData para);

        CmdReplyHistoricalMessage(para, conn);
    }
    //--------------------------------------------------------------------------------------------

    private void CmdReceiveMessage(ReplyChatMessageData para)
    {
        string functionName = "ReceiveMessage";
        Type type = typeof(NetChatManager);

        NetConnectionToClient[] conns = ChatServerManager.Instance.GetPlayersInRoom(para.roomID);
        foreach (var conn in conns)
        {
            if (conn != null)
            {
                SendCommandToTargetClient<ReplyChatMessageData>(conn, type, functionName, para, (int)Channels.Reliable);
            }
        }
    }
    private void CmdReplyHistoricalMessage(ReplyHistoricalMessageData HistoricalLog, NetConnectionToClient conn)
    {
        string functionName = "ReplyHistoricalMessage";
        Type type = typeof(NetChatManager);
        SendCommandToTargetClient<ReplyHistoricalMessageData>(conn, type, functionName, HistoricalLog, (int)Channels.Reliable);
    }
#endif

    #endregion
    #region common
    private void Awake()
    {
        identity.SetNetManagerID(NetManagerType.NetChat, OnServer);
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
        RoomEventManager.Instance.Register_RoomAvailable_Event(OnRoomAvailable);
#endif
    }
    private void OnDisable()
    {
        if (OnServer)
            return;
#if UNITY_EDITOR || !UNITY_SERVER
        RoomEventManager.Instance.Unregister_RoomAvailable_Event(OnRoomAvailable);
#endif
    }
#if UNITY_EDITOR || !UNITY_SERVER
    private void OnRoomAvailable(uint roomID)
    {
        if (room == null)
            room = new();
        room.Clear();
        room.roomID = roomID;

        CmdRequestHistoricalMessage(roomID);
    }
    private void AddMessage(int playerIndex, string playerName, string content)
    {
        if (room.totalConversationCount >= ChatRoom.MAX_CONVERSATION_COUNT)
        {
            room.nextConversationIndex = room.latestConversationIndex;
            room.latestConversationIndex++;
            if (room.latestConversationIndex >= ChatRoom.MAX_CONVERSATION_COUNT)
                room.latestConversationIndex %= ChatRoom.MAX_CONVERSATION_COUNT;

            room.conversationLog[room.nextConversationIndex] = (playerIndex, playerName, content);
        }
        else
            room.conversationLog[room.nextConversationIndex++] = (playerIndex, playerName, content);

        room.totalConversationCount++;
    }
#endif
    #endregion
}
