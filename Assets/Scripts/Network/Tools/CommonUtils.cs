using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
public static partial class Utils 
{
    public static void RegisterDataParseDelegate<T>(Action<NetWriter, T> write, Func<NetReader, T> read)
        where T : struct
    {
        Writer<T>.write = write;
        Reader<T>.read = read;
    }
    public static bool AccurateIntervalElapsed(double time, double interval, ref double lastTime)
    {
        if (time < lastTime + interval)
            return false;
        long multiplier = (long)(time / interval);
        lastTime = multiplier * interval;
        return true;
    }

    public static uint GetRandomUInt()
    {
        // use Crypto RNG to avoid having time based duplicates
        using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
        {
            byte[] bytes = new byte[4];
            rng.GetBytes(bytes);
            return BitConverter.ToUInt32(bytes, 0);
        }
    }

    //主要用于区分纯逻辑服务器
    public static bool IsHeadless() =>
#if UNITY_SERVER
            true;
#else
    SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null;
#endif

    public static ushort GetFunctionHashCode(string functionName)
    {
        int hash = GetStableHashCode(functionName);
        ushort res = (ushort)((hash >> 16) ^ hash);
        return res;
    }

    public static int GetStableHashCode(string text)
    {
        unchecked
        {
            uint hash = 0x811c9dc5;
            uint prime = 0x1000193;

            for (int i = 0; i < text.Length; ++i)
            {
                byte value = (byte)text[i];
                hash = hash ^ value;
                hash *= prime;
            }

            return (int)hash;
        }
    }
    public static bool IsSceneObject(NetIdentity identity)
    {

        return identity.gameObject.hideFlags != HideFlags.NotEditable &&
            identity.gameObject.hideFlags != HideFlags.HideAndDontSave &&
            identity.sceneID != 0;
    }
    public static bool IsPrefab(GameObject obj)
    {
#if UNITY_EDITOR
        return UnityEditor.PrefabUtility.IsPartOfPrefabAsset(obj);
#else
            return false;
#endif
    }
    //是否这个gameobject源自于一个prefab，和他的层级的父节点无关 
    public static bool IsSceneObjectWithPrefabParent(GameObject gameObject, out GameObject prefab)
    {
        prefab = null;

#if UNITY_EDITOR
        if (UnityEditor.PrefabUtility.IsPartOfPrefabInstance(gameObject) == false)
        {
            return false;
        }
        prefab = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
#endif

        if (prefab == null)
        {
            Debug.LogError($"Failed to find prefab parent for scene object [name:{gameObject.name}]");
            return false;
        }
        return true;
    }

    public static string FullFunctionName(Type componentType, string functionName)
    {
        //不支持函数重载，不过因为RemoteCallDelegate的参数恒定，一般也不会出现重载的情况
        //多程序集存在冲突风险 
        string functionFullName = componentType.Name + "." + functionName;
        return functionFullName;
    }
}
