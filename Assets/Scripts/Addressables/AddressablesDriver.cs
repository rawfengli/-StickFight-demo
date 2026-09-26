using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

/// <summary>
/// 这个静态类用于对接AddressableManager与其他模块的api
/// </summary>
public static class AddressablesDriver
{
    public static bool initialized => AddressableManager.Instance.initialized;
    public static async void AddressableInit()
        => await AddressableManager.Instance.Init();

    #region Addressables

    public static async Task LoadAssetsGroup<T>(
        List<string> keys,
        Action<float> OnLoad = null,
        Action OnSucceeded = null,
        Action OnFailed = null,
        Addressables.MergeMode mode = Addressables.MergeMode.Union,
        Action<T> OnEachAssetLoaded = null)
    {
        try
        {
            await AddressableManager.Instance.LoadAssets<T>(keys, OnLoad, OnSucceeded, OnFailed, mode, OnEachAssetLoaded);
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to Load Assets,reason : {e}");
        }
    }
    public static void UnloadAllAssets()
        => AddressableManager.Instance.UnloadAssets();
    public static async Task<T> LoadAsset<T>(string name)
        where T : class
    {
        try
        {
            return await AddressableManager.Instance.LoadAssetAsync<T>(name);
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to Load Assets,reason : {e}");
            return null;
        }
    }
    public static GameObject LoadPrefab(string name)
    {
        try
        {
            return AddressableManager.Instance.InstantiateObject(name);
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to Load Assets,reason : {e}");
            return null;
        }
    }
    #endregion

    #region Network

    public static async Task<uint> RegisterClientPrefab(
        string name, 
        ClientSpawnHandlerDelegate spawnHandler = null, 
        ClientUnspawnHandlerDelegate unspawnHandler = null)
    {
        GameObject prefab = await AddressableManager.Instance.LoadAssetAsync<GameObject>(name);
        return NetClient.RegisterPrefab(prefab, spawnHandler, unspawnHandler);
    }
    public static async Task<uint> RegisterClientPrefab(
        string name, string label, 
        ClientSpawnHandlerDelegate spawnHandler = null,
        ClientUnspawnHandlerDelegate unspawnHandler = null)
    {
        GameObject prefab = await AddressableManager.Instance.LoadAssetAsync<GameObject>(name, label);
        return NetClient.RegisterPrefab(prefab, spawnHandler, unspawnHandler);
    }

    #endregion

    #region 

    public static IEnumerator LoadAssetCoroutine<T>(string key, Action<T> _event = null)
    {

        Task<T> task = AddressableManager.Instance.LoadAssetAsync<T>(key);

        while (!task.IsCompleted)
            yield return null;
        _event?.Invoke(task.Result);
    }

    public static IEnumerator LoadScene(string sceneName, Action OnLoadedScene = null)
    {
        AsyncOperationHandle<SceneInstance> sceneHandle = Addressables.LoadSceneAsync(sceneName, LoadSceneMode.Single);

        yield return sceneHandle.IsDone;
        OnLoadedScene?.Invoke();
        if (sceneHandle.Status == AsyncOperationStatus.Succeeded)
        {
            Debug.Log("Scene has loaded");
        }
    }
    #endregion
}
