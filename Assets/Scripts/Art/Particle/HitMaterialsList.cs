using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HitMaterials : MonoBehaviour
{
    public enum MaterialColor
    {
        Default = 0,
        Red = 1,
        Blue = 2,
        Green = 3,
        Yellow = 4,

    }
    private static HitMaterials instance;
    public static HitMaterials Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("Bullet Hit Materials");
                instance = go.AddComponent<HitMaterials>();
            }
            return instance;
        }
    }
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Init();
        instance = this;
    }
    private void Init() { }
    internal List<Material> materials = new();
    public Material GetMaterial(MaterialColor material)
        => materials[(int)material];
    public Material this[MaterialColor index]
        => materials[(int)index];
    public Material this[int index]
        => materials[index];
}
