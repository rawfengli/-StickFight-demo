using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;
#if UNITY_EDITOR || UNITY_SERVER
public class NetSingleRoomManager : MonoBehaviour
{
    public uint roomID;
    [Header("Network")]
    [SerializeField] public NetInGameManager netInGameManager;
    [Header("Scene")]
    private SceneGenerationData data;

    [Header("Player")]
    [SerializeField] public NetStickManManager[] players = new NetStickManManager[5];

    [Header("Weapon")]
    //这里的AllCount只是为了方便而暂时这么写的
    public const int ALL_COUNT = 2;
    public int nowWeapon = 0;
    public Vector2 SpawnWeaponPos = new(0, 10);
    private Dictionary<int, Weapon> spawnedWeapons = new();
    private int nowWeaponSpawnCount = 0;
    private float weaponSpawnPassTime = 0;
    private const float SPAWN_WEAPON_TIME = 4f;
    private int spawnedID = 0;
    public void SetSceneGenerationData(SceneGenerationData data)
    {
        this.data = data;
    }
    public void DestroyWeapon(GameObject gameObject)
    {
        if (!gameObject.TryGetComponent<Weapon>(out Weapon weapon))
            return;
        if (RoomServerManager.Instance.TryGetRoom(roomID, out ServerRoom room))
        {

            spawnedWeapons.Remove(weapon.spawnedID);
            NetServer.Unspawn(gameObject, room.players);
            Destroy(gameObject);
        }
    }
    public void SpawnWeapon()
    {
        if(RoomServerManager.Instance.TryGetRoom(roomID, out ServerRoom room))
        {
            nowWeapon++;
            
            GameObject weapon = Instantiate(
                InGameSceneManager.GetWeaponPrefab(nowWeapon, out string name), 
                SpawnWeaponPos, 
                Quaternion.identity);
            
            weapon.name = name;
            NetServer.Spawn(weapon, room.players);

            Weapon _weapon = weapon.GetComponent<Weapon>();
            _weapon.DestoryWeaponHandle += DestroyWeapon;
            _weapon.spawnedID = spawnedID;
            spawnedID++;
            spawnedWeapons.Add(nowWeapon, _weapon);

            nowWeaponSpawnCount++;
            weaponSpawnPassTime = 0;

            nowWeapon %= 2;
        }
        else
        {
            Debug.LogWarning("Net Room Not Find for ID : " + roomID.ToString());
        }
    }

    public bool CheckCouldSpawnWeapon()
    {
        weaponSpawnPassTime += Time.fixedDeltaTime;
        if (weaponSpawnPassTime >= SPAWN_WEAPON_TIME && nowWeaponSpawnCount < ALL_COUNT)
        {
            return true;
        }
        else
        {
            return false;
        }

    }
    public void FixedUpdate()
    {
        if(CheckCouldSpawnWeapon())
        {
            SpawnWeapon();
        }

    }
    public void OnPlayerDie(int playerIndex, ServerRoom room)
    {
        int aliveCount = 0;
        int winner = 0;
        for(int i = 1; i <= 4; i++)
        {
            if (players[i] == null)
                continue;
            aliveCount++;
            if (!players[i].playerData.isAlive)
                aliveCount--;
            else
                winner = i;
        }
        if (aliveCount <= 1)
            GameFinished(winner, room);
    }
    private void GameFinished(int winner, ServerRoom room)
    {
        foreach(var conn in room.players)
        {
            if (conn == null)
                continue;
            netInGameManager.CmdGameFinish(winner, conn);
        }
        ResetStatus();
        DestroyAllWeapons();
        GameStart();
    }
    public void GameStart()
    {
        targetCount = 0;
        beReadyCount = 0;

        for (int i = 1; i < 4; i++)
        {
            if (players[i] != null)
            {
                Vector3 startPos = data.PlayerGenerationPosition[i];
                targetCount++;
                players[i].playerData.isReseting = true;
                StartCoroutine(MoveToStartPosCoroutine(i, startPos, players[i]));
            }
        }
        StartCoroutine(WaitUntilPlayersAllReadyCoroutine());
    }
    private int targetCount;
    private int beReadyCount;
    private IEnumerator WaitUntilPlayersAllReadyCoroutine()
    {
        while(true)
        {
            if (beReadyCount >= targetCount)
                break;
            yield return null;
        }
        for (int i = 1; i < 4; i++)
        {
            if (players[i] != null)
            {
                players[i].standingAction.ResetGravity();
                players[i].playerData.isReseting = false;
            }
        }
    }
    private float MoveToStartPosTime = 1f;
    private float PauseTime = 0.4f;
    private IEnumerator MoveToStartPosCoroutine(int playerIndex, Vector3 targetPos, NetStickManManager player)
    {
        float nowTime;
        nowTime = 0;
        while (nowTime < PauseTime)
        {
            nowTime += Time.deltaTime;
            yield return null;
        }
        nowTime = 0;
        while (nowTime < MoveToStartPosTime)
        {
            float t = 0.1f;
            Vector3 playerPos = player.playerObj.transform.position;
            player.playerObj.transform.position = Vector3.Lerp(playerPos, targetPos, t);

            nowTime += Time.deltaTime;
            yield return null;
        }

        Vector3 pos = player.playerObj.transform.position;
        nowTime = 0;
        while (nowTime < PauseTime)
        {
            player.playerObj.transform.position = pos;
            nowTime += Time.deltaTime;
            yield return null;
        }
        beReadyCount++;
    }
    private void ResetStatus()
    {
        nowWeapon = 0;
        nowWeaponSpawnCount = 0;
        weaponSpawnPassTime = 0;

        for (int i = 1; i < 4; i++)
        {
            if (players[i] != null)
            {
                players[i].playerData.ResetPlayerData();
            }
        }
    }
    private void DestroyAllWeapons()
    {
        var weapons = spawnedWeapons.Values.Cast<Weapon>().ToList();
        for (int i = 0; i < weapons.Count(); i++)
        {
            DestroyWeapon(weapons[i].gameObject);
        }
        spawnedWeapons.Clear(); ;
    }
    public void SetPlayer(int index, NetStickManManager player)
    {
        players[index] = player;
    }
}
#endif