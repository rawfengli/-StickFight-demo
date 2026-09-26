using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class HandAttackParticles : CommonParticles
{
    public const float LIFE_TIME = 1.8f;
    private const float MOVE_TIME = 0.1f;
    private const float MOVE_DISTANCE = 8f;

    private Vector3 attackDir;
    private Vector3 handStartPos;
    Coroutine handAttackParticlesCoroutine;
    Coroutine lifeCoroutine;
    public void SetValue(Vector3 handStartPos, Vector3 attackDir)
    {
        this.handStartPos = handStartPos;
        this.attackDir = attackDir;

        gameObject.transform.position = handStartPos - MOVE_DISTANCE * 0.5f * attackDir;
        gameObject.transform.rotation = Quaternion.FromToRotation(Vector3.up, attackDir);

        if (handAttackParticlesCoroutine != null)
            StopCoroutine(handAttackParticlesCoroutine);
        handAttackParticlesCoroutine = StartCoroutine(HandAttackParticlesCoroutine());

        if (lifeCoroutine != null)
            StopCoroutine(lifeCoroutine);
        lifeCoroutine = StartCoroutine(LifeCoroutine());
    }
    private void OnEnable() { }
    IEnumerator HandAttackParticlesCoroutine()
    {
        float _time = 0;
        while (_time < HandAttackParticles.MOVE_TIME)
        {
            _time += Time.deltaTime;
            yield return null;
        }
        gameObject.transform.position += attackDir * MOVE_DISTANCE;
    }
    IEnumerator LifeCoroutine()
    {
        float _time = 0;
        while (_time < HandAttackParticles.LIFE_TIME)
        {
            _time += Time.deltaTime;
            yield return null;
        }
        ParticlesSystemManager.Instance.Return<HandAttackParticles>(this);
    }
}
