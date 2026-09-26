using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface INetSpawn
{
    public GameObject Spawn(ObjectSpawnMessage msg);
    public GameObject Unspawn(GameObject go);
}
