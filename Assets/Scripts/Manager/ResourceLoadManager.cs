using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class ResourceLoadManager : MonoBehaviour
{
    private static ResourceLoadManager instance;
    public static ResourceLoadManager Instance
    {
        get
        {
            if(instance == null)
            {
                GameObject go = new GameObject("Resource Load Manager");
                instance = go.AddComponent<ResourceLoadManager>();
            }
            return instance;
        }
    }

    public void Awake()
    {
        if(instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(instance);
    }
    public async Task LoadAssets<T>(
        List<string> keys, 
        Action<float> OnLoading, 
        Action OnSucceeded, 
        Action OnFailed)
    {
        await AddressablesDriver.LoadAssetsGroup<T>(keys, OnLoading, OnSucceeded, OnFailed);
    }
}
