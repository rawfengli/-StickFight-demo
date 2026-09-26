using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using static NetRoomManager;

public class ClientRoom
{
    public bool active;
    public int selfIndex;
    public uint roomID;
    public bool isHost;
    public readonly string[] names = new string[GameRoomData.MAX_PLAYER_COUNT + 1];
    public readonly int[] avatarIndexs = new int[GameRoomData.MAX_PLAYER_COUNT + 1];
}
public static class GameRoomData
{
    public const int MAX_PLAYER_COUNT = 4;
    public static ClientRoom room { get; private set; } = new();
    public static bool roomAvailable => room.active;

    public static void RegisterAllEvents()
    {
        EventBus.Register_RoomCreated_Event(OnRoomCreated, RoomEventLayer.Data);
        EventBus.Register_RoomJoined_Event(OnRoomJoined, RoomEventLayer.Data);
        EventBus.Register_NewPlayerJoined_Event(OnNewPlayerJoined, RoomEventLayer.Data);
    }
    public static void UnregisterAllEvents()
    {
        EventBus.Unregister_RoomCreated_Event(OnRoomCreated, RoomEventLayer.Data);
        EventBus.Unregister_RoomJoined_Event(OnRoomJoined, RoomEventLayer.Data);
        EventBus.Unregister_NewPlayerJoined_Event(OnNewPlayerJoined, RoomEventLayer.Data);
    }
    public static void OnRoomCreated(RoomCreatedNetData data)
    {
        room.isHost = true;
        room.selfIndex = data.playerIndex;
        room.roomID = data.roomID;

        room.active = true;
    }
    public static void OnRoomJoined(NewPlayerNetData data)
    {
        if (data.status == JoinedRoomStatus.Succeeded)
        {
            room.isHost = false;
            room.selfIndex = data.newPlayerIndex;
            room.roomID = data.roomID;

            for (int i = 1; i <= 4; i++)
            {
                room.names[i] = data.otherPlayerNames[i];
                room.avatarIndexs[i] = data.otherPlayerAvatarIndexs[i];
            }
            room.active = true;
        }
    }
    public static void OnNewPlayerJoined(NewPlayerNetData data)
    {
        room.names[data.newPlayerIndex] = data.newPlayerName;
        room.avatarIndexs[data.newPlayerIndex] = data.newPlayerAvatarIndex;
    }
    public static void OnRoomUnavailable()
    {
        room.isHost = false;
        room.selfIndex = -1;
        room.roomID = 0;
        room.active = false;

        for (int i = 1; i <= 4; i++)
        {
            room.names[i] = string.Empty;
            room.avatarIndexs[i] = -1;
        }
    }
    public static bool IsSelf(int index)
        => index == room.selfIndex;
}
