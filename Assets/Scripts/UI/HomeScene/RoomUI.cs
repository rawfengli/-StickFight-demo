using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class RoomUI : MonoBehaviour
{
    [Header("Object")]
    [SerializeField]
    private HomeButtonManager homeButtonManager;
    [SerializeField]
    private GameObject option;
    [SerializeField]
    private GameObject content;

    [Header("Common")]
    [Range(10.0f, 200.0f)]
    public float lerpMoveOffset = 10f;
    public float lerpValue = 0.03f;
    private bool isMovingOut = false;

    [Header("Option Lerp Move Setting")]
    public Vector2 StartPos_Option;
    public Vector2 TargetPos_Option;

    [Header("Content Lerp Move Setting")]
    public Vector2 StartPos_Content;
    public Vector2 TargetPos_Content;

    private void Awake()
    {
        isMovingOut = false;
        StartPos_Option = option.transform.localPosition;
        StartPos_Content = content.transform.localPosition;
    }
    private void OnEnable()
    {
        //唯一的问题是，这里不应该在这里注册，
        //应该注册到homeButtonManager中的某个事件或调用Manager的方法， 然后再由homeButtonManager对对应的butonn进行注册
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

        Vector2 target_Option;
        Vector2 target_Content;

        Vector2 nowPos_Option = option.transform.localPosition;
        Vector2 nowPos_Content = content.transform.localPosition;
        
        if (isMovingOut)
        {
            Vector2 dir = Vector2.left;

            target_Option = TargetPos_Option + lerpMoveOffset * dir;
            target_Content = TargetPos_Content + lerpMoveOffset * dir;
        }
        else
        {
            Vector2 dir = Vector2.right;

            target_Option = StartPos_Option + lerpMoveOffset * dir;
            target_Content = StartPos_Content + lerpMoveOffset * dir;
        }

        while ( (nowPos_Option - target_Option).magnitude > lerpMoveOffset ||
                (nowPos_Content - target_Content).magnitude > lerpMoveOffset)
        {
            nowPos_Option = Vector2.Lerp(nowPos_Option, target_Option, lerpValue);
            nowPos_Content = Vector2.Lerp(nowPos_Content, target_Content, lerpValue);
            
            option.transform.localPosition = nowPos_Option;
            content.transform.localPosition = nowPos_Content;

            yield return null;
        }
    }

}
