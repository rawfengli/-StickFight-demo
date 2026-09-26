using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMaterialsManager : MonoBehaviour
{
    public enum MaterialColor
    {
        Default = 0,
        Red = 1,
        Blue = 2,
        Green = 3,
        Yellow = 4,

    }
    private static PlayerMaterialsManager instance;
    public static PlayerMaterialsManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("Stick Man Materials");
                instance = go.AddComponent<PlayerMaterialsManager>();
            }
            return instance;
        }
    }
    internal List<Material> materials = new List<Material>();
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
    public Material GetMaterial(MaterialColor material)

        => materials[(int)material];
    public Material this[MaterialColor index]
        => materials[(int)index];
    public Material this[int index]
        => materials[index];

}
