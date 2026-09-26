using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR || UNITY_SERVER
public class ServerManager : NetManager
{
    public static ServerManager _Instance => (ServerManager)Instance;
    private void Start()
    {
        StartServer();
    }
    public override void OnStartServer()
    {
        base.OnStartServer();
    }
    [Header("Stick Man Test")]
    public GameObject StickMan;
    public NetInGameManager netInGameManager;
    public void CreateAtiveGame(GameObject scenePrefab, ServerRoom room)
    {
        CreateScenePrefab(scenePrefab, room, out List<Vector2> PlayerPos, out NetSingleRoomManager singleRoom);
        CreatePlayers(room, PlayerPos, singleRoom);
    }
    public void CreateScenePrefab(GameObject scenePrefab, ServerRoom room, out List<Vector2> PlayerPos, out NetSingleRoomManager singleRoom)
    {
        GameObject roomSecneObject = new GameObject("Game:Room" + room.roomID.ToString());
        singleRoom = roomSecneObject.AddComponent<NetSingleRoomManager>();
        singleRoom.netInGameManager = netInGameManager;

        RoomServerManager.Instance.StoreSingleRoomManager(room.roomID, singleRoom);

        SceneGenerationData sceneGenerationData = scenePrefab.GetComponent<SceneGenerationData>();
        singleRoom.SetSceneGenerationData(sceneGenerationData);
        PlayerPos = sceneGenerationData.PlayerGenerationPosition;

        roomSecneObject.transform.position = Vector3.zero;
        void CreateEveryNetIdentity(Transform parent)
        {
            foreach (Transform transform in parent)
            {
                if (transform.TryGetComponent<NetIdentity>(out NetIdentity identity))
                {
                    Vector3 position = transform.position;
                    Quaternion rotation = transform.rotation;
                    Vector3 scale = transform.localScale;

                    GameObject obj = Instantiate(transform.gameObject, roomSecneObject.transform);
                    obj.transform.position = position;
                    obj.transform.rotation = rotation;
                    obj.transform.localScale = scale;

                    NetServer.Spawn(obj, room.players, null);
                    continue;
                }
                if (transform.TryGetComponent<OnlyServerObject>(out OnlyServerObject serverObject))
                {
                    Vector3 position = transform.position;
                    Quaternion rotation = transform.rotation;
                    Vector3 scale = transform.localScale;

                    GameObject obj = Instantiate(transform.gameObject, roomSecneObject.transform);
                    obj.transform.position = position;
                    obj.transform.rotation = rotation;
                    obj.transform.localScale = scale;
                    continue;
                }
                CreateEveryNetIdentity(transform);
            }
        }

        CreateEveryNetIdentity(scenePrefab.transform);
    }

    public void CreatePlayers(ServerRoom room, List<Vector2> PlayerPos, NetSingleRoomManager singleRoom)
    {
        for (int i = 1; i <= 4; i++)
        {
            NetConnectionToClient conn = room.players[i];
            if (conn == null)
                return;
            Vector2 pos;
            if (PlayerPos.Count - 1 < i)
                pos = PlayerPos[0];
            else
                pos = PlayerPos[i];

            GameObject playerObj = Instantiate(StickMan, pos, Quaternion.identity);
            NetServer.Spawn(playerObj, room.players, conn);
            NetStickManManager player = playerObj.GetComponent<NetStickManManager>();
            player.SetNetData(i, room.roomID);
            singleRoom.SetPlayer(i, player);
        }
    }
    public void StartGame(ServerRoom room)
    {
        foreach(var player in room.managerReference.players)
        {
            if (player == null)
                continue;
            netInGameManager.CmdGameHasStarted(room);
        }
    }
}
#endif
