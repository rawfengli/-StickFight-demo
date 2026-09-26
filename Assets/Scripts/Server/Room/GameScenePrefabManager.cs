using System.IO;
using UnityEngine;
#if UNITY_EDITOR || UNITY_SERVER
public static class InGameSceneManager
{
    public static GameObject GetGameScenePrefab(int sceneID)
    {
        string path = Path.Combine("Server", "Prefabs", "Game Scene Prefabs");
        path = Path.Combine(path, "Scene Prefab_" + sceneID.ToString());
        GameObject prefab = Resources.Load<GameObject>(path);

        if (prefab == null)
        {
            Debug.LogError($"找不到 场景Prefab: {path}");
            return null;
        }
        return prefab;
    }
    public static GameObject GetWeaponPrefab(int sceneID, out string weaponName)
    {
        string path = Path.Combine("Server", "Prefabs", "Weapons");
        path = Path.Combine(path, "Weapon_" + sceneID.ToString());
        GameObject prefab = Resources.Load<GameObject>(path);
        weaponName = prefab.GetComponent<Weapon>().WeaponName;
        if (prefab == null)
        {
            Debug.LogError($"找不到 武器Prefab: {path}");
            return null;
        }
        return prefab;
    }
}
#endif
