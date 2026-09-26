using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DustParticles : CommonParticles
{
    public const float HALF_HEIGHT = 0.5f;
    public const float LIFE_TIME = 1.5f;
    Coroutine footDustCoroutine;

    private void OnEnable()
    {
        if (footDustCoroutine != null)
            StopCoroutine(footDustCoroutine);

        footDustCoroutine = StartCoroutine(FootDustCoroutine());
    }

    IEnumerator FootDustCoroutine()
    {
        float _time = 0;
        while (_time < DustParticles.LIFE_TIME)
        {
            _time += Time.deltaTime;
            yield return null;
        }
        ParticlesSystemManager.Instance.Return<DustParticles>(this);
    }
}
