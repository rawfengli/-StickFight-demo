using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

#if UNITY_EDITOR || UNITY_SERVER

public class ServerRoom
{
    public uint roomID;

    public NetConnectionToClient host;

    public bool gameOnRunning;
    public int activePlayerCount;
    public int SceneObjectReadyCount;
    public int SceneLoadReadyCount;

    public NetSingleRoomManager managerReference;

    //0 index set null
    public readonly NetConnectionToClient[] players = new NetConnectionToClient[4 + 1];
    public readonly string[] names = new string[4 + 1];
    public readonly int[] avatarIndexs = new int[4 + 1];
}
//应该用静态的
public class RoomServerManager
{
    private static RoomServerManager instance;
    public static RoomServerManager Instance
    {
        get
        {
            if (instance == null)
                instance = new RoomServerManager();
            return instance;
        }
    }
    private Pool<ServerRoom> pool = new Pool<ServerRoom>(() => new ServerRoom(), 10);

    private Dictionary<uint, ServerRoom> rooms = new();
    public const int MAX_PLAYER = 4;
    public async Task<ServerRoom> CreateNewRoom(NetConnectionToClient conn, NetRoomManager.CreateRoomData data)
    {
        uint roomID;
        while (true)
        {
            roomID = (uint)Random.Range((int)1e7, (int)1e8);
            if (rooms.ContainsKey(roomID))
                await Task.Yield();
            else
                break;
        }
        return CreateNewRoom(roomID, conn, data);
    }
    public ServerRoom CreateNewRoom(uint roomID, NetConnectionToClient conn, NetRoomManager.CreateRoomData data)
    {
        ServerRoom room = pool.Get();

        room.SceneLoadReadyCount = 0;
        room.SceneObjectReadyCount = 0;
        room.activePlayerCount = 1;

        room.gameOnRunning = false;
        room.roomID = roomID;
        room.host = conn;
        room.players[1] = conn;
        room.names[1] = data.playerName;
        room.avatarIndexs[1] = data.avatarID;

        rooms.Add(roomID, room);

        ChatServerManager.Instance.CreateChatRoom(roomID);

        return room;

    }

    public bool JoinRoom(uint roomID, int avatarIndex, string name, NetConnectionToClient conn, out int newPlayerIndex, out ServerRoom room, out JoinedRoomStatus status)
    {
        newPlayerIndex = 0;
        status = JoinedRoomStatus.Unkown;
        if (rooms.TryGetValue(roomID, out room))
        {
            for(int i = 1; i < room.players.Length; i++)
            {
                if (room.players[i] == null)
                {
                    room.players[i] = conn;
                    room.avatarIndexs[i] = avatarIndex;
                    room.names[i] = name;
                    room.activePlayerCount++;

                    newPlayerIndex = i;

                    status = JoinedRoomStatus.Succeeded;
                    return true;
                }
            }
            status = JoinedRoomStatus.Full;
            return false;
        }
        else
        {
            status = JoinedRoomStatus.RoomNotExist;
            return false;
        }
    }
    public void ExitRoom() { }
    public void DestoryRoom() { }
    public bool TryGetRoom(uint roomID, out ServerRoom room)
        => rooms.TryGetValue(roomID, out room);
    public bool StartGame(uint roomID, out ServerRoom room)
    {
        if (TryGetRoom(roomID, out room))
        {
            if (room.gameOnRunning)
                return false;
            else
            {
                room.gameOnRunning = true;
                return true;
            }
        }
        else
            return false;
    }
    public void StoreSingleRoomManager(uint roomID, NetSingleRoomManager manager)
    {
        if (TryGetRoom(roomID, out ServerRoom room))
        {
            manager.roomID = roomID;
            room.managerReference = manager;
        }
        else
        {
            Debug.LogWarning("Net Room Not Find for ID : " + roomID.ToString());
        }
    }
    public void PlayerLoadSceneReady(uint roomID, int playerIndex)
    {
        rooms[roomID].SceneLoadReadyCount++;

        int now = rooms[roomID].SceneLoadReadyCount;
        int target = rooms[roomID].activePlayerCount;
        if (now >= target)
        {
            ServerManager._Instance.CreateAtiveGame(InGameSceneManager.GetGameScenePrefab(1), rooms[roomID]);
        }
    }
    public void PlayerInGameReady(uint roomID, int playerIndex)
    {
        NetSingleRoomManager _room = rooms[roomID].managerReference;
        rooms[roomID].SceneObjectReadyCount++;

        int now = rooms[roomID].SceneObjectReadyCount;
        int target = rooms[roomID].activePlayerCount;
        if (now >= target)
        {
            ServerManager._Instance.StartGame(rooms[roomID]);
        }
    }
}
#endif
