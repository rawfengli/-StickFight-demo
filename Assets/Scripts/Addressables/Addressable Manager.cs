using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

public class AddressableManager : MonoBehaviour
{
    public static GameObject obj;
    private static AddressableManager _instance;
    public static AddressableManager Instance
    {
        get
        {
            if(_instance == null)
            {
                obj = new GameObject(); 
                _instance = obj.AddComponent<AddressableManager>();
                DontDestroyOnLoad(obj);
            }
            return _instance;
        }
    }
    public bool initialized { get; private set; } = false;

    private Dictionary<string, AsyncOperationHandle> handles = new();
    private Dictionary<string, AsyncOperationHandle> sceneHandles = new();

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        obj = gameObject;
        DontDestroyOnLoad(obj);
    }

    void CustomExceptionHandler(AsyncOperationHandle handle, Exception exception)
    {
        if (exception is InvalidKeyException)
            return;

        Addressables.LogException(handle, exception);
    }
    public async Task Init()
    {
        var handle = Addressables.InitializeAsync();

        await handle.Task;
        if (handle.Status != AsyncOperationStatus.Succeeded)
            throw new System.Exception("Addressables Initialize Failed.");

        Addressables.Release(handle);

        initialized = true;
    }

    public async Task CatalogUpdate()
    {
        var checkHandle = Addressables.CheckForCatalogUpdates(false);

        List<string> catalogs = await checkHandle.Task;

        if (catalogs != null && catalogs.Count > 0)
        {
            var updateHandle = Addressables.UpdateCatalogs(catalogs, false);

            await updateHandle.Task;

            if (updateHandle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError("Addressables Catalog Update Failed.");
            }

            Addressables.Release(updateHandle);
        }

        Addressables.Release(checkHandle);
    }

    public async Task<bool> DownloadAsync(string key, Action<float> OnLoad = null, Action OnFinish = null)
    {
        var handle = Addressables.DownloadDependenciesAsync(key, false);

        try
        {
            while (handle.IsDone == false)
            {
                OnLoad?.Invoke(handle.PercentComplete);
                await Task.Yield();
            }
            OnFinish?.Invoke();
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to load asset, key:{key}. exception:{e}");
            return false;
        }
        finally
        {
            Addressables.Release(handle);
        }
    }

    public async Task<bool> DownloadAsync(List<string> key, Addressables.MergeMode mode, Action<float> OnLoad = null, Action OnFinish = null)
    {
        var handle = Addressables.DownloadDependenciesAsync(key, mode, false);

        try
        {
            while (handle.IsDone == false)
            {
                OnLoad?.Invoke(handle.PercentComplete);
                await Task.Yield();
            }
            OnFinish?.Invoke();
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to load asset, key:{key}. exception:{e}");
            return false;
        }
        finally
        {
            Addressables.Release(handle);
        }
    }

    private Stack<AsyncOperationHandle> loadedAssets = new();

    public async Task LoadAssets<T>(
        List<string> keys, 
        Action<float> OnLoad = null, 
        Action OnSucceeded = null, 
        Action OnFailed = null,
        Addressables.MergeMode mode = Addressables.MergeMode.Union,
        Action<T> OnEachAssetLoaded = null)
    {
        var locations = Addressables.LoadResourceLocationsAsync(keys, mode, typeof(T));

        await locations.Task;

        if (locations.Status != AsyncOperationStatus.Succeeded || locations.Result.Count <= 0)
        {
            Addressables.Release(locations);
            Debug.Log("Load none Asset,ingore for type " + typeof(T).Name);

            return;
        }
        Addressables.Release(locations);

        var handles = Addressables.LoadAssetsAsync<T>(keys, null, mode);

        while (!handles.IsDone)
        {
            OnLoad?.Invoke(handles.PercentComplete);
            await Task.Yield();
        }
        OnLoad?.Invoke(1f);

        if (handles.Status == AsyncOperationStatus.Succeeded)
        {
            OnSucceeded?.Invoke();
            loadedAssets.Push(handles);
            foreach(var asset in handles.Result)
            {
                OnEachAssetLoaded?.Invoke(asset);
            }
        }
        else
        {
            if (OnFailed != null)
                OnFailed?.Invoke();
            else
                Debug.LogWarning($"Load Assets Failed, key:{keys}");

            Addressables.Release(handles);
        }
    }
    public void UnloadAssets()
    {
        while (loadedAssets.Count > 0)
        {
            AsyncOperationHandle handle = loadedAssets.Pop();
            Addressables.Release(handle);
        }
    }

    //only once
    public async Task<T> LoadAssetAsync<T>(string key)
    {
        if (handles.ContainsKey(key))
        {
            if (handles[key].IsDone)
            {
                if (!(handles[key].Result is T))
                    throw new Exception("Type T of Loading Asset not match");

                return (T)handles[key].Result;
            }
            else
            {
                throw new Exception("Load Asset is In progress, And don't call it twice");
            }
        }
        var handle = Addressables.LoadAssetAsync<T>(key);

        T asset = await handle.Task;
        
        if (handle.Status != AsyncOperationStatus.Succeeded)
        {
            Addressables.Release(handle);
            throw new Exception($"Load Addressable Failed for key: {key}");
        }

        handles.Add(key, handle);

        return asset;
    }
    public async Task<T> LoadAssetAsync<T>(string name, string label)
    {
        string key = name + "_" + label;

        if (this.handles.ContainsKey(key))
        {
            if (this.handles[key].IsDone)
            {
                Debug.LogWarning("LoadAssetAsync for one key should only do once");
                if (this.handles[key].Result is IList<T> list && list.Count == 1)
                    return list[0];
                else
                {
                    Addressables.Release(this.handles[key]);
                    this.handles.Remove(key);
                    throw new Exception($"Value of Key:{key} is invaild, it will be removed");
                }
            }
            else
            {
                throw new Exception("Load Asset is In progress, And don't call it twice");
            }
        }
        var handles = Addressables.LoadAssetsAsync<T>(new List<string>(){name, label}, null, Addressables.MergeMode.Intersection);

        IList<T> result = await handles.Task;

        if (handles.Status != AsyncOperationStatus.Succeeded)
        {
            Addressables.Release(handles);
            throw new Exception($"Load Addressable Failed for key: {key}");
        }

        if (result.Count != 1)
        {
            Addressables.Release(handles);
            throw new Exception($"key of {key} has Multiple results,Please Use LoadAssetsAsync");
        }

        this.handles[key] = handles;

        return result[0];
    }

    public async Task<SceneInstance> LoadSceneAsync(string sceneName, LoadSceneMode mode = LoadSceneMode.Single, Action<bool, SceneInstance?> OnFinish = null)
    {
        var handle = Addressables.LoadSceneAsync(sceneName, mode);

        SceneInstance scene;

        try
        {
            scene = await handle.Task;
        }
        catch (Exception e)
        {
            Debug.LogError($"Load Scene Failed: {sceneName}, reason:{e}");
            OnFinish?.Invoke(false, null);
            return default;
        }

        if (handle.Status != AsyncOperationStatus.Succeeded)
        {
            Debug.LogError($"Load Scene Failed: {sceneName}");
            OnFinish?.Invoke(false, null);
            return default;
        }

        OnFinish?.Invoke(true, handle.Result);
        sceneHandles[sceneName] = handle;

        return scene;

    }
    public async Task ReleaseSceneAsync(string sceneName)
    {
        if (sceneHandles.TryGetValue(sceneName, out var handle) == false)
            return;

        if (handle.Result is SceneInstance scene)
        {
            var unloadHandle = Addressables.UnloadSceneAsync(scene);

            await unloadHandle.Task;

            Addressables.Release(unloadHandle);
        }

        sceneHandles.Remove(sceneName);
    }

    public GameObject InstantiateObject(string name, Vector3 pos = default, Quaternion rot = default)
    {
        if (TryGetAsset<GameObject>(name, out GameObject prefab))
        {
            GameObject instance = Instantiate(prefab);
            instance.transform.position = pos;
            instance.transform.rotation = rot;
            return instance;
        }
        else
            return null;
    }

    public GameObject InstantiateObject(string name, string label, Vector3 pos = default, Quaternion rot = default)
    {
        if (TryGetAsset<GameObject>(name, label, out GameObject prefab))
        {
            GameObject instance = Instantiate(prefab);
            instance.transform.position = pos;
            instance.transform.rotation = rot;
            return instance;
        }
        else
            return null;
    }

    public bool TryGetAsset<T>(string name, out T res)
    {
        res = default;
        if (handles.ContainsKey(name))
        {
            if (handles[name].Result is T)
            {
                res = (T)handles[name].Result;
                return true;
            }
            else
            {
                Debug.LogWarning($"Type of key:{name} not Match");
                return false;
            }
        }
        else
        {
            Debug.LogWarning($"Key:{name} not exist");
            return false;
        }
    }
    public bool TryGetAsset<T>(string name, string label, out T res)
    {
        res = default;
        string key = name + "_" + label;
        if (handles.ContainsKey(key))
        {
            if (handles[key].Result is IList<T> list)
            {
                if(list.Count == 1)
                {
                    res = list[0];
                    return true;
                }
                else
                {
                    Addressables.Release(this.handles[key]);
                    handles.Remove(key);
                    Debug.LogWarning($"Count of IList of Key:{key} is invaild(should be one), it will be removed");
                    return false;
                }
            }
            else
            {
                Debug.LogWarning($"Type of key:{key} not Match, it may be not a iList or type not match");
                return false;
            }
        }
        else
        {
            Debug.LogWarning($"Key:{key} not exist");
            return false;
        }
    }

    public void Release(string name)
    {
        if (handles.TryGetValue(name, out var handle) == false)
        {
            Debug.Log("asset you want to release not exist");
            return;
        }
        Addressables.Release(handle);

        handles.Remove(name);
    }
    public void Release(string name, string lable)
    {
        string key = name + "_" + lable;
        if (handles.TryGetValue(key, out var handle) == false)
        {
            Debug.Log("asset you want to release not exist");
            return;
        }
        Addressables.Release(handle);

        handles.Remove(key);
    }
    /// <summary>
    /// ReleaseAll不会释放场景资源，可能存在当前的场景正是handle中的情况 
    /// </summary>
    public void ReleaseAll()
    {
        UnloadAssets();

        foreach (var handle in handles.Values)
            Addressables.Release(handle);
        handles.Clear();
    }

}
