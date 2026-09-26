using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.U2D;

[DefaultExecutionOrder(-10)]
public class LoadingScreenCanvas : MonoBehaviour
{
    private static LoadingScreenCanvas instance;
    public static LoadingScreenCanvas Instance => instance;
    
    [SerializeField] private LoadingScreenUI UI;
    public event Action<float> OnLoadingEvent;
    public event Action OnSucceededEvent;
    public event Action OnFailedEvent;
    public event Action OnAllLoadedEvent;
    public const string HOME_SCENE_NAME = "Client Home Page";
    public const string INGAME_SCENE_NAME = "Fight Scene";
    public const string SCENE_HOME_KEY = "common";
    public const string SCENE_INGAME_KEY = "ingame";
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(instance);
    }
    public void Enable()
    {
        gameObject.SetActive(true);
    }
    public void Disable()
    {
        gameObject.SetActive(false);
    }
    private void InvokeOnLoading(float percent)
        => OnLoadingEvent.Invoke(percent);
    private void InvokeOnSucceeded()
        => OnSucceededEvent.Invoke();
    private void InvokeOnFailed()
        => OnFailedEvent.Invoke();
    public void Load(string mainKey)
    {
        Enable();
        StartCoroutine(LoadCoroutine(mainKey));
    }
    IEnumerator LoadCoroutine(string mainKey)
    {
        List<string> keys = new List<string> { mainKey };
        
        keys.Add("");
        {
            UI.SetLoadingResourceName("物件");
            Task task = AddressablesDriver.LoadAssetsGroup<GameObject>(
                keys,
                InvokeOnLoading,
                InvokeOnSucceeded,
                InvokeOnFailed);

            while (!task.IsCompleted)
                yield return null;
        }
        {
            UI.SetLoadingResourceName("材质");
            keys[1] = "material";
            Task task = AddressablesDriver.LoadAssetsGroup<Material>(
                keys,
                InvokeOnLoading,
                InvokeOnSucceeded,
                InvokeOnFailed);

            while (!task.IsCompleted)
                yield return null;
        }
        {
            UI.SetLoadingResourceName("着色器");
            keys[1] = "shader";

            Task task = AddressablesDriver.LoadAssetsGroup<Shader>(
                keys,
                InvokeOnLoading,
                InvokeOnSucceeded,
                InvokeOnFailed);

            while (!task.IsCompleted)
                yield return null;
        }
        {
            UI.SetLoadingResourceName("精灵图");
            keys[1] = "sprite atlas";

            Task task = AddressablesDriver.LoadAssetsGroup<SpriteAtlas>(
                keys,
                InvokeOnLoading,
                InvokeOnSucceeded,
                InvokeOnFailed);
            while (!task.IsCompleted)
                yield return null;
        }
        {
            UI.SetLoadingResourceName("音效");
            keys[1] = "audio clip";

            Task task = AddressablesDriver.LoadAssetsGroup<AudioClip>(
                keys,
                InvokeOnLoading,
                InvokeOnSucceeded,
                InvokeOnFailed);

            while (!task.IsCompleted)
                yield return null;
        }
        OnAllLoadedEvent.Invoke();
    }
}
