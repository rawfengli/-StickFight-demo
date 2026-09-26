using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using UnityEngine;

public class ParticlesSystemManager : MonoBehaviour
{
    private static class ParticlesPool<T>
        where T : CommonParticles
    {
        public static GameObject parent;
        public static Pool<T> pool;
    }
    private static ParticlesSystemManager instance;
    public static ParticlesSystemManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("Particles System Manager");
                instance = go.AddComponent<ParticlesSystemManager>();
            }
            return instance;
        }
    }
    private bool ready = false;
    private bool hasInit = false;
    public void SetReady() 
        => ready = true;
    public void SetParticlesTemplate(int id, GameObject template)
    {
        switch (id)
        {
            case 1:
                DustParticlesTemplateGO = template;
                break;
            case 2:
                JumpParticlesTemplateGO = template;
                break;
            case 3:
                BulletHitGroundParticlesTemplateGO = template;
                break;
            case 4:
                HandAttackParticlesTemplateGO = template;
                break;
            case 5:
                BodyOnHitParticlesTemplateGO = template;//hand
                break;
            case 6:
                BulletHitBodyParticlesTemplateGO = template;
                break;
            default:
                break;

        }
    }
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }
    public void Update()
    {
        if(ready && !hasInit)
        {
            Init();
            hasInit = true;
        }
    }

    private void Init()
    {
        InitSingleParticles<DustParticles>(DustParticlesTemplate, 16, "Dust Particles Parent");
        InitSingleParticles<JumpParticles>(JumpParticlesTemplate, 8, "Jump Particles Parent");
        InitSingleParticles<BulletHitGroundParticles>(BulletHitGroundParticlesTemplate, 32, "Bullet Hit Particles Parent");
        InitSingleParticles<HandAttackParticles>(HandAttackParticlesTemplate, 8, "Hand Attack Particles Parent");
        InitSingleParticles<BodyOnHitParticles>(BodyOnHitParticlesTemplate, 16, "On Hit With Hand Particles Parent");
        InitSingleParticles<BulletHitBodyParticles>(BulletHitBodyParticlesTemplate, 16, "On Hit With Bullet Particles Parent");
    }
    private void OnDestroy()
    {
        DestoryManager<DustParticles>();
        DestoryManager<JumpParticles>();
        DestoryManager<BulletHitGroundParticles>();
    }
    private void InitSingleParticles<T>(T template, int initSize, string name)
        where T : CommonParticles
    {
        ParticlesPool<T>.parent = new GameObject(name);
        ParticlesPool<T>.parent.transform.SetParent(transform);
        ParticlesPool<T>.parent.transform.localPosition = Vector3.zero;
        ParticlesPool<T>.pool = new(
            () => CreateParticlesInstance<T>(template),
            initSize,
            OnGetParticlesInstance<T>,
            OnReturnParticlesInstance<T>);
    }
    private void DestoryManager<T>()
        where T : CommonParticles
    {
        ParticlesPool<T>.parent = null;
        ParticlesPool<T>.pool = null;
    }
    // Pool
    //注意Template必须是prefabs
    private DustParticles DustParticlesTemplate => DustParticlesTemplateGO.GetComponent<DustParticles>();
    internal GameObject DustParticlesTemplateGO;
    // -------------------------------------------------------------------------
    private JumpParticles JumpParticlesTemplate => JumpParticlesTemplateGO.GetComponent<JumpParticles>();
    internal GameObject JumpParticlesTemplateGO;
    // -------------------------------------------------------------------------
    private BulletHitGroundParticles BulletHitGroundParticlesTemplate => BulletHitGroundParticlesTemplateGO.GetComponent<BulletHitGroundParticles>();
    internal GameObject BulletHitGroundParticlesTemplateGO;
    // -------------------------------------------------------------------------
    private HandAttackParticles HandAttackParticlesTemplate => HandAttackParticlesTemplateGO.GetComponent<HandAttackParticles>();
    private GameObject HandAttackParticlesTemplateGO;
    // -------------------------------------------------------------------------
    private BodyOnHitParticles BodyOnHitParticlesTemplate => BodyOnHitParticlesTemplateGO.GetComponent<BodyOnHitParticles>();
    internal GameObject BodyOnHitParticlesTemplateGO;
    // -------------------------------------------------------------------------
    private BulletHitBodyParticles BulletHitBodyParticlesTemplate => BulletHitBodyParticlesTemplateGO.GetComponent <BulletHitBodyParticles>();
    internal GameObject BulletHitBodyParticlesTemplateGO;

    public T Get<T>() where T : CommonParticles
    {
        if(ParticlesPool<T>.pool == null)
        {
            Debug.LogError("Particles Type not exist!");
            return null;
        }
        return ParticlesPool<T>.pool.Get();
    }
    public void Return<T>(T value) where T : CommonParticles
    {
        if (ParticlesPool<T>.pool == null)
        {
            Debug.LogError("Particles Type not exist!");
            return;
        }
        ParticlesPool<T>.pool.Return(value);
    }
    private static T CreateParticlesInstance<T>(T template)
        where T : CommonParticles
    {
        GameObject particles = GameObject.Instantiate(template.gameObject);
        particles.SetActive(false);
        particles.transform.SetParent(ParticlesPool<T>.parent.transform);
        particles.transform.localPosition = Vector3.zero;

        return particles.GetComponent<T>();
    }
    private static void OnGetParticlesInstance<T>(T instance)
        where T : CommonParticles
    {
        instance.gameObject.SetActive(true);
    }
    private static void OnReturnParticlesInstance<T>(T instance)
        where T : CommonParticles
    {
        instance.gameObject.SetActive(false);
    }
}
