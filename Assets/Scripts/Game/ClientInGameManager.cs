using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using static UnityEngine.ParticleSystem;

public class ClientInGameManager : MonoBehaviour
{
    [SerializeField] private int initCount = 0;
    [SerializeField] private int targetCount = 0;
    [SerializeField] private NetInGameManager netManager;
    private bool ready = false;
    private bool hasSendReady = false;
#if UNITY_EDITOR || !UNITY_SERVER
    private void OnEnable()
    {
        InGameEventBus.Instance.Register_GameHasStarted_Event(OnGameHasStarted);
    }
    private void OnDisable()
    {
        InGameEventBus.Instance.Unregister_GameHasStarted_Event(OnGameHasStarted);
    }
    public void OnGameHasStarted()
    {
        ScreenTransitionCanvas.Instance.ScreenTransition(
            ScreenTransitionHandle.BlackScreenFadeInAndOut,
            0.6f, 0.2f, 
            () => LoadingScreenCanvas.Instance.Disable());
    }

    public void Awake()
    {
        initCount = 0;
        targetCount = 0;
        ready = false;
        hasSendReady = false;

        ManagerInitHandle(ParticlesSystemManagerInit);
        ManagerInitHandle(BulletsManagerInit);
        ManagerInitHandle(PrefabsLoaderInit);
        ManagerInitHandle(PlayerMaterialsManagerInit);
    }
    public void Update()
    {
        if(ready && !hasSendReady)
        {
            netManager.CmdAnswerSceneLoaded();
            hasSendReady = true;
        }

        if(initCount >= targetCount)
            ready = true;
    }
    public void ManagerInitHandle(Action<Action> initHandle)
    {
        targetCount++;
        initHandle.Invoke(() => initCount++);
    }
    #region Particles
    private List<string> ParticlesName = new List<string>
    {
        "Dust Particles",
        "Jump Particle",

        "Bullet Hit Ground Particles",
        "Hand Attack Particle",

        "Hand Hit Body Particles",
        "Bullet Hit Body Particles"
    };
    private List<string> ParticlesColorName = new List<string>
    {
        "default",
        "red",
        "blue",
        "green",
        "yellow",
    };
    private void ParticlesSystemManagerInit(Action onInited)
    {
        StartCoroutine(ParticlesSystemManagerInitCoroutine(onInited));
    }
    private IEnumerator ParticlesSystemManagerInitCoroutine(Action onInited)
    {
        int id = 0;
        foreach(var name in ParticlesName)
        {
            Task<GameObject> task;

            id++;
            task = AddressablesDriver.LoadAsset<GameObject>(name);
            while (!task.IsCompleted)
                yield return null;
            ParticlesSystemManager.Instance.SetParticlesTemplate(id, task.Result);
        }
        foreach (var color in ParticlesColorName)
        {
            Task<Material> task;

            string name = "material hit particle " + color;
            task = AddressablesDriver.LoadAsset<Material>(name);
            while (!task.IsCompleted)
                yield return null;
            HitMaterials.Instance.materials.Add(task.Result);
        }

        ParticlesSystemManager.Instance.SetReady();
        onInited?.Invoke();
    }
    #endregion

    #region Bullet
    private List<string> BullteName = new List<string>
    {
        "Pistol Bullet",
        "Shotgun Bullet",
    };

    private void BulletsManagerInit(Action onInited)
    {
        StartCoroutine(BulletsManagerInitCoroutine(onInited));
    }
    private IEnumerator BulletsManagerInitCoroutine(Action onInited)
    {
        Task<GameObject> task;
        int id = 0;
        foreach (var name in BullteName)
        {
            id++;
            task = AddressablesDriver.LoadAsset<GameObject>(name);
            while (!task.IsCompleted)
                yield return null;
            BulletsManager.Instance.SetBulletTemplate(id, task.Result);
        }
        BulletsManager.Instance.SetReady();
        onInited?.Invoke();
    }
    #endregion

    #region prefabs
    private List<string> prefabsName = new List<string>
    {
        "Pistol",
        "Shotgun",
        "Ground",
        "Stick Man",
        "Street Light",
        "Pistol Bullet",
        "Shotgun Bullet"
    };
    private void PrefabsLoaderInit(Action onInited)
    {
        StartCoroutine(PrefabsLoaderInitCoroutine(onInited));
    }
    private IEnumerator PrefabsLoaderInitCoroutine(Action onInited)
    {
        foreach (var name in prefabsName)
        {
            Task<GameObject> goTask;
            goTask = AddressablesDriver.LoadAsset<GameObject>(name);
            
            while (!goTask.IsCompleted)
                yield return null;

            GameObject prefab = goTask.Result;

            Task<uint> task;
            if (prefab.TryGetComponent<INetSpawn>(out INetSpawn spawnHandle))
            {
                task = AddressablesDriver.RegisterClientPrefab(name, spawnHandle.Spawn, spawnHandle.Unspawn);
            }
            else
            {
                task = AddressablesDriver.RegisterClientPrefab(prefab.name);
            }
            while (!task.IsCompleted)
                yield return null;
        }
        onInited?.Invoke();
    }
    #endregion
    #region stickman material
    private List<string> StickManColorName = new List<string>
    {
        "default",
        "red",
        "blue",
        "green",
        "yellow",
    };
    private void PlayerMaterialsManagerInit(Action onInited)
    {
        StartCoroutine(PlayerMaterialsManagerCoroutine(onInited));
    }
    private IEnumerator PlayerMaterialsManagerCoroutine(Action onInited)
    {
        Task<Material> task;
        int id = 0;
        foreach (var color in StickManColorName)
        {
            id++;
            string name = "material stickman " + color;
            task = AddressablesDriver.LoadAsset<Material>(name);
            while (!task.IsCompleted)
                yield return null;
            PlayerMaterialsManager.Instance.materials.Add(task.Result);
        }
        onInited?.Invoke();
    }

    #endregion
#endif
}
