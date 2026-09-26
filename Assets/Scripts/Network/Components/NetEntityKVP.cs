using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NetEntityKVP : MonoBehaviour
{
    [SerializeField]
    private EntityEnum _entityEnum;
    public EntityEnum entityEnum => _entityEnum;
    private GameObject go => gameObject;
}
