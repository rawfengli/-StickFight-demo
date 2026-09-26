using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR || UNITY_SERVER
public class NetChatRoom
{
    public const int MAX_CONVERSATION_COUNT = 50;

    public uint roomID;
    //第一个说明是哪一号发的，第二个是说他的名称是什么，第三是内容
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
        for (int i = 0; i < MAX_CONVERSATION_COUNT; i++)
            conversationLog[i] = default;
    }
}
public class ChatServerManager
{
    private static ChatServerManager instance;
    public static ChatServerManager Instance
    {
        get
        {
            if (instance == null)
                instance = new ChatServerManager();
            return instance;
        }
    }
    private Pool<NetChatRoom> pool = new Pool<NetChatRoom>(() => new NetChatRoom(), 10);
    public Dictionary<uint, NetChatRoom> rooms = new();
    public void AddMessage(uint roomID, int playerIndex, string name, string content)
    {
        if(rooms.TryGetValue(roomID, out NetChatRoom room))
        {
            if (room.totalConversationCount >= NetChatRoom.MAX_CONVERSATION_COUNT)
            {
                room.nextConversationIndex = room.latestConversationIndex;
                room.latestConversationIndex++;
                if (room.latestConversationIndex >= NetChatRoom.MAX_CONVERSATION_COUNT)
                    room.latestConversationIndex %= NetChatRoom.MAX_CONVERSATION_COUNT;

                room.conversationLog[room.nextConversationIndex] = (playerIndex, name, content);
            }
            else
                room.conversationLog[room.nextConversationIndex++] = (playerIndex, name, content);

            room.totalConversationCount++;
        }
    }
    public void GetHistoricalMessage(uint roomID, out NetChatManager.ReplyHistoricalMessageData data)
    {
        data = new();

        if (rooms.TryGetValue(roomID, out NetChatRoom room))
        {
            int count = Math.Min(room.totalConversationCount, NetChatRoom.MAX_CONVERSATION_COUNT);

            data.playerIndexs = new int[count];
            data.playerNames = new string[count];
            data.conversationLog = new string[count];

            int index;
            if (room.totalConversationCount >= NetChatRoom.MAX_CONVERSATION_COUNT)
                index = room.latestConversationIndex;
            else
                index = 0;

            for (int i = 0; i < count; i++)
            {
                data.playerIndexs[i] = room.conversationLog[index].Item1;
                data.playerNames[i] = room.conversationLog[index].Item2;
                data.conversationLog[i] = room.conversationLog[index].Item3;

                index++;

                if (index >= NetChatRoom.MAX_CONVERSATION_COUNT)
                    index = 0;
            }
        }
        else
            Debug.LogWarning($"Room not found for ID : {roomID}");
    }
    public NetConnectionToClient[] GetPlayersInRoom(uint roomID)
    {
        if (RoomServerManager.Instance.TryGetRoom(roomID, out ServerRoom room))
            return room.players;
        else
        {
            Debug.LogWarning($"Room not found for ID : {roomID}");
            return null;
        }
    }

    public void CreateChatRoom(uint roomID)
    {
        NetChatRoom room = pool.Get();
        rooms.Add(roomID, room);

    }

    public void DestoryChatRoom(uint roomID)
    {
        if(rooms.TryGetValue(roomID, out NetChatRoom room))
        {
            room.Clear();
            pool.Return(room);
        }
    }
}
#endif
