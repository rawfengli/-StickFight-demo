using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BulletsManager : NetBehaviour
{
    private static class BulletsPool<T>
        where T : Bullet
    {
        public static GameObject parent;
        public static Pool<T> pool;
    }
    private static BulletsManager instance;
    public static BulletsManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("Bullet System Manager");
                instance = go.AddComponent<BulletsManager>();
            }
            return instance;
        }
    }
    [Header("Network")]
    [SerializeField] private bool OnServer;
    private bool ready = false;
    private bool hasInit = false;
    public void SetReady()
        => ready = true;
    public void SetBulletTemplate(int id, GameObject template)
    {
        switch (id)
        {
            case 1:
                PistolBulletTemplateGO = template;
                break;
            case 2:
                ShotgunBulletTemplateGO = template;
                break;
            default:
                break;

        }
    }
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
#if UNITY_EDITOR || UNITY_SERVER
        if (OnServer)
        {
            Init();
        }
#endif
        identity.SetNetManagerID(NetManagerType.NetBullet, OnServer);
        instance = this;
    }
    private void OnDestroy()
    {
        identity.RemoveNetManagerID(NetManagerType.NetBullet, OnServer);
    }

    public void Update()
    {
        if (ready && !hasInit)
        {
            Init();
            hasInit = true;
        }
    }
    private void Init()
    {
        InitSingleBullet<PistolBullet>(PistolBulletTemplate, 16, "Pistol Bullet Parent");
        InitSingleBullet<ShotgunBullet>(ShotgunBulletTemplate, 16, "Shotgun Bullet Parent");

    }
    #region Pool
    private PistolBullet PistolBulletTemplate => PistolBulletTemplateGO.GetComponent<PistolBullet>();
    [SerializeField] internal GameObject PistolBulletTemplateGO;

    private ShotgunBullet ShotgunBulletTemplate => ShotgunBulletTemplateGO.GetComponent<ShotgunBullet>();
    [SerializeField] internal GameObject ShotgunBulletTemplateGO;


    private void InitSingleBullet<T>(T template, int initSize, string name)
        where T : Bullet
    {
        BulletsPool<T>.parent = new GameObject(name);
        BulletsPool<T>.parent.transform.SetParent(transform);
        BulletsPool<T>.parent.transform.localPosition = Vector3.zero;
        BulletsPool<T>.pool = new(
            () => CreateBulletInstance<T>(template),
            initSize,
            OnGetBulletInstance<T>,
            OnReturnBulletInstance<T>);
    }
    private static T CreateBulletInstance<T>(T template)
        where T : Bullet
    {
        GameObject Bullet = GameObject.Instantiate(template.gameObject);
        Bullet.SetActive(false);
        Bullet.transform.SetParent(BulletsPool<T>.parent.transform);
        Bullet.transform.localPosition = Vector3.zero;

        return Bullet.GetComponent<T>();
    }
    public T Get<T>() where T : Bullet
    {
        if (BulletsPool<T>.pool == null)
        {
            Debug.LogError("Bullet Type not exist!");
            return null;
        }
        return BulletsPool<T>.pool.Get();
    }
    public void Return<T>(T value) where T : Bullet
    {
        if (BulletsPool<T>.pool == null)
        {
            Debug.LogError("Bullet Type not exist!");
            return;
        }
        BulletsPool<T>.pool.Return(value);
    }

    private static void OnGetBulletInstance<T>(T instance)
        where T : Bullet
    {
        instance.gameObject.SetActive(true);
    }
    private static void OnReturnBulletInstance<T>(T instance)
        where T : Bullet
    {
        instance.gameObject.SetActive(false);
    }
    #endregion

    #region Network
    static BulletsManager()
    {
        Type type = typeof(BulletsManager);
#if UNITY_EDITOR || !UNITY_SERVER
        RemoteProcedureCalls.RegisterServerCommand(type, "NetworkOnBulletHit", InvokeNetworkOnBulletHit, false);
#endif
    }
#if UNITY_EDITOR || !UNITY_SERVER
    
    #region Client

    private static void InvokeNetworkOnBulletHit(NetBehaviour behaviour, NetReader reader, NetConnectionToClient conn)
    {
        NetTransportData.Read(reader, out BulletHitGroundDataNetData para);
        ((BulletsManager)behaviour).NetworkOnBulletHit(para);
    }

    void NetworkOnBulletHit(BulletHitGroundDataNetData para)
    {
        if (NetClient.TryGetNetworkObject(para.netID, out GameObject obj))
        {
            Bullet bullet = obj.GetComponent<Bullet>();
            bullet.OnBulletHitGround(para.pos, para.normal, para.shotPlayerIndex);
        }
    }

    #endregion
    
#endif
#if UNITY_EDITOR || UNITY_SERVER
    #region Server
    public void CmdNetworkOnBulletHit(
        uint roomID,
        Vector3 pos,
        Vector3 normal,
        HitInfo hitInfo,
        Bullet bullet,
        int hitPlayerIndex,
        int shotPlayerIndex)
    {
        if (RoomServerManager.Instance.TryGetRoom(roomID, out ServerRoom room) == false)
        {
            Debug.LogWarning("Network message: On Bullet Hit send failed, room not find for id : " + roomID.ToString());
            return;
        }
        if (bullet.TryGetComponent<NetIdentity>(out NetIdentity identity))
        {
            BulletHitGroundDataNetData para = new();
            para.pos = pos;
            para.normal = normal;
            para.netID = identity.netID;
            para.shotPlayerIndex = shotPlayerIndex;

            string functionName = "NetworkOnBulletHit";
            Type type = typeof(BulletsManager);
            foreach (var conn in room.players)
            {
                if (conn == null)
                    continue;

                SendCommandToTargetClient<BulletHitGroundDataNetData>(conn, type, functionName, para, (int)Channels.Reliable);
            }
        }
        else
        {
            Debug.LogWarning("Cmd Bullet Hit Data Failed, identity not found");
        }
    }

    public void NetworkSpawnBullet(uint roomID, GameObject bullet)
    {
        if (NetServer.active == false || NetClient.active == true)
            return;
        if (RoomServerManager.Instance.TryGetRoom(roomID, out ServerRoom room))
        {
            NetServer.Spawn(bullet, room.players);
        }
        else
        {
            Debug.LogWarning("spawn object failed for roomid not exist : " + roomID.ToString());
        }
    }
    public void NetworkUnspawnBullet(uint roomID, GameObject bullet)
    {
        if (NetServer.active == false || NetClient.active == true)
            return;
        if (RoomServerManager.Instance.TryGetRoom(roomID, out ServerRoom room))
        {
            NetServer.Unspawn(bullet, room.players);
        }
        else
        {
            Debug.LogWarning("unspawn object failed for roomid not exist : " + roomID.ToString());
        }
    }
    #endregion
#endif

    #endregion
}
