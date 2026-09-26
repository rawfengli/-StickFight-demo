using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public enum HitInfo : byte
{
    Ground = 0,
    Player = 1,
    
}

public struct BulletHitGroundDataNetData : INetTransportData<BulletHitGroundDataNetData>
{
    public Vector3 pos;
    public Vector3 normal;

    public uint netID;
    public int shotPlayerIndex;
    public Action<NetWriter, BulletHitGroundDataNetData> write =>
        (writer, para) =>
        {
            writer.Write(para.pos);
            writer.Write(para.normal);
            writer.Write(para.netID);
            writer.Write<int>(para.shotPlayerIndex);
        };
    public Func<NetReader, BulletHitGroundDataNetData> read =>
        (reader) =>
        {
            BulletHitGroundDataNetData para = new BulletHitGroundDataNetData();
            para.pos = reader.Read<Vector3>();
            para.normal = reader.Read<Vector3>();
            para.netID = reader.Read<uint>();
            para.shotPlayerIndex = reader.Read<int>();
            
            return para;
        };
}

public abstract class Bullet : MonoBehaviour, INetSpawn
{
    protected abstract void Init();
    protected abstract void OnEnable();
    protected abstract void OnDisable();
    public abstract void OnBulletHitGround(Vector3 pos, Vector3 normal, int shotPlayerIndex);
    public abstract GameObject Spawn(ObjectSpawnMessage msg);
    public abstract GameObject Unspawn(GameObject go);
}
