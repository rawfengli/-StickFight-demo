using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;

public class ChatLerp : MonoBehaviour
{
    [Header("Object")]
    [SerializeField]
    private HomeButtonManager homeButtonManager;
    public Vector2 StartPos;
    public Vector2 TargetPos;
    [Range(10f, 200f)]
    public float lerpOffset = 50f;
    public float lerpValue = 0.03f;
    private bool isMovingOut = false;
    private void Awake()
    {
        isMovingOut = false;
        StartPos = gameObject.transform.localPosition;
    }

    private void OnEnable()
    {
        homeButtonManager[HomeOptionIndex.Room].OnReleased += LerpMove;
        homeButtonManager[HomeOptionIndex.Room].OnOtherReleased += LerpMove;

    }
    private void OnDisable()
    {
        homeButtonManager[HomeOptionIndex.Room].OnReleased -= LerpMove;
        homeButtonManager[HomeOptionIndex.Room].OnOtherReleased -= LerpMove;

    }
    private Coroutine lerpMoveCoroutine;
    private void LerpMove(WRButton _)
    {
        if (lerpMoveCoroutine != null)
            StopCoroutine(lerpMoveCoroutine);

        lerpMoveCoroutine = StartCoroutine(LerpMoveCoroutine());
    }
    private IEnumerator LerpMoveCoroutine()
    {
        isMovingOut ^= true;
        Vector2 _target;
        if (isMovingOut)
            _target = TargetPos + lerpOffset * Vector2.up;
        else
            _target = StartPos + lerpOffset * Vector2.down;

        while (true)
        {
            Vector2 nowPos = new Vector2(transform.localPosition.x, transform.localPosition.y);

            if ((nowPos - _target).magnitude < lerpOffset)
                break;

            nowPos = Vector2.Lerp(nowPos, _target, lerpValue);
            transform.localPosition = new(nowPos.x, nowPos.y, 0);
            yield return null;
        }

    }
}
