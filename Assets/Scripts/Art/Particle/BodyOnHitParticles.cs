using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static HitMaterials;

public class BodyOnHitParticles : CommonParticles
{
    private ParticleSystemRenderer particleSystemRenderer;
    public const float LIFE_TIME = 1.5f;
    Coroutine lifeCoroutine;
    private void Awake()
    {
        particleSystemRenderer = GetComponent<ParticleSystemRenderer>();
    }
    public void SetColorMaterial(MaterialColor material)
    {
        particleSystemRenderer.material = HitMaterials.Instance[material];
    }
    public void SetColorMaterial(int playerIndex)
    {
        particleSystemRenderer.material = HitMaterials.Instance[playerIndex];
    }

    private void OnEnable()
    {
        if (lifeCoroutine != null)
            StopCoroutine(lifeCoroutine);

        lifeCoroutine = StartCoroutine(LifeCoroutine());
    }

    IEnumerator LifeCoroutine()
    {
        float _time = 0;
        while (_time < BulletHitGroundParticles.LIFE_TIME)
        {
            _time += Time.deltaTime;
            yield return null;
        }
        ParticlesSystemManager.Instance.Return<BodyOnHitParticles>(this);
    }
}
