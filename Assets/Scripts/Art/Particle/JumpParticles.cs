using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class JumpParticles : CommonParticles
{
    public const float HIPS_HEIGHT_OFFSET = 6f;
    private const float LIFE_TIME = 2f;
    private const float MOVE_TIME = 0.1f;
    private const float MOVE_DISTANCE = 8f;
    Coroutine jumpParticlesCoroutine;
    Coroutine lifeCoroutine;
    private void OnEnable()
    {
        if (jumpParticlesCoroutine != null)
            StopCoroutine(jumpParticlesCoroutine);
        jumpParticlesCoroutine = StartCoroutine(JumpParticlesCoroutine());

        if (lifeCoroutine != null)
            StopCoroutine(lifeCoroutine);
        lifeCoroutine = StartCoroutine(LifeCoroutine());
    }
    IEnumerator JumpParticlesCoroutine()
    {
        float _time = 0;
        while (_time < JumpParticles.MOVE_TIME)
        {
            _time += Time.deltaTime;
            yield return null;
        }
        gameObject.transform.position += Vector3.up * MOVE_DISTANCE;
    }

    IEnumerator LifeCoroutine()
    {
        float _time = 0;
        while (_time < JumpParticles.LIFE_TIME)
        {
            _time += Time.deltaTime;
            yield return null;
        }
        ParticlesSystemManager.Instance.Return<JumpParticles>(this);
    }
}
