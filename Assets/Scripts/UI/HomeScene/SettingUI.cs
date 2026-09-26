using System;
using System.Collections;
using UnityEngine;
using UnityEngine.U2D;
using static HomeButtonManager;

public class SettingUI : MonoBehaviour
{
    private enum MoveStatus
    {
        AtStart,
        OnLerpMove,
        OnUnfold,
        AtTarget
    }
    [Header("Object")]
    [SerializeField]
    private HomeButtonManager homeButtonManager;
    [SerializeField]
    public GameObject option;
    [SerializeField]
    public GameObject content;
    [Header("Lerp Move Setting")]
    public Vector2 StartPos;
    public Vector2 TargetPos;
    [Range(10.0f, 200.0f)]
    public float lerpMoveOffset_All = 10f;
    public float lerpValue_All = 0.03f;

    public float waitTimeBetweenLerpAndUnfold = 0.1f;

    [Header("Unfold Setting")]
    public float StartPos_ContentX;
    public float StartPos_OptionX;
    public float TargetPos_ContentX = 50f;
    public float TargetPos_OptionX = -70f;
    [Range(0.0f, 200.0f)]
    public float UnfoldOffset = 50f;
    public float lerpValue_Unfold = 0.03f;
    [Space(20)]
    [Header("Avatar Setting")]
    [SerializeField] private AvatarSelector avatarSelector;

    private MoveStatus status = 0;
    private void Awake()
    {
        status = MoveStatus.AtStart;
        StartPos_ContentX = content.transform.localPosition.x;
        StartPos_OptionX = option.transform.localPosition.x;
        StartPos = new(transform.localPosition.x, transform.localPosition.y);

        LoadAvatar();

    }

    private void OnEnable()
    {
        homeButtonManager[HomeOptionIndex.Setting].OnReleased += MoveIn;
        homeButtonManager[HomeOptionIndex.Setting].OnOtherReleased += MoveOut;
    }
    private void OnDisable()
    {
        homeButtonManager[HomeOptionIndex.Setting].OnReleased -= MoveIn;
        homeButtonManager[HomeOptionIndex.Setting].OnOtherReleased -= MoveOut;
    }
    private void LoadAvatar()
    {
        string key = "avatar";

        StartCoroutine(
            AddressablesDriver.LoadAssetCoroutine<SpriteAtlas>(
                key,
                (SpriteAtlas atlas) =>
                {
                    int count = Enum.GetValues(typeof(Avatars)).Length - 1;//- 1 for unkown
                    for (int i = 1; i <= count; i++)
                    {
                        Sprite sprite = atlas.GetSprite("avatar_" + i.ToString());
                        avatarSelector.avatars[i - 1].Init(i, sprite);
                        PlayerInfo.RegisterAvatar(sprite);
                    }
                })
            );
    }

    //In
    private void MoveIn(WRButton _)
    {
        switch(status)
        {
            case MoveStatus.AtStart:
                StopOut();
                LerpIn();
                break;

            case MoveStatus.OnLerpMove:
                StopOut();
                LerpIn();
                break;

            case MoveStatus.OnUnfold:
                StopOut();
                UnfoldContent();
                break;

            case MoveStatus.AtTarget:
                break;
        }

    }
    private Coroutine coroutine_LerpIn;
    private void LerpIn()
    {
        if (coroutine_LerpIn != null)
            StopCoroutine(coroutine_LerpIn);

        coroutine_LerpIn = StartCoroutine(LerpInCoroutine());
    }
    private IEnumerator LerpInCoroutine()
    {
        status = MoveStatus.OnLerpMove;
        Vector2 nowPos = new(transform.localPosition.x, transform.localPosition.y);
        Vector2 dir = TargetPos - nowPos;

        Vector2 _targetPos = TargetPos + lerpMoveOffset_All * dir.normalized;
        while ((nowPos - _targetPos).magnitude > lerpMoveOffset_All)
        {
            nowPos = Vector2.Lerp(nowPos, _targetPos, lerpValue_All);
            transform.localPosition = nowPos;
            yield return null;
        }
        status = MoveStatus.OnUnfold;

        float nowTime = 0;
        while(nowTime < waitTimeBetweenLerpAndUnfold)
        {
            nowTime += Time.deltaTime;
            yield return null;
        }

        UnfoldContent();
    }

    private Coroutine coroutine_Unfold;
    private void UnfoldContent()
    {
        if (coroutine_Unfold != null)
            StopCoroutine(coroutine_Unfold);

        coroutine_Unfold = StartCoroutine(UnfoldContentCoroutine());
    }

    private IEnumerator UnfoldContentCoroutine()
    {
        float nowPosX_Content = content.transform.localPosition.x;
        float nowPosX_Option = option.transform.localPosition.x;

        float targetPosX_Content = TargetPos_ContentX + UnfoldOffset;//right
        float targetPosX_Option = TargetPos_OptionX - UnfoldOffset;//right

        while (Mathf.Abs(nowPosX_Content - targetPosX_Content) > UnfoldOffset)
        {
            nowPosX_Content = Mathf.Lerp(nowPosX_Content, targetPosX_Content, lerpValue_Unfold);
            Vector3 newPos_Content = content.transform.localPosition;
            newPos_Content.x = nowPosX_Content;
            content.transform.localPosition = newPos_Content;

            nowPosX_Option = Mathf.Lerp(nowPosX_Option, targetPosX_Option, lerpValue_Unfold);
            Vector3 newPos_Option = option.transform.localPosition;
            newPos_Option.x = nowPosX_Option;
            option.transform.localPosition = newPos_Option;
            yield return null;
        }
        status = MoveStatus.AtTarget;
    }
    private void StopOut()
    {
        if (coroutine_LerpOut != null)
            StopCoroutine(coroutine_LerpOut);
        if (coroutine_Fold != null)
            StopCoroutine(coroutine_Fold);
    }
    //Out
    //----------------------------------------------------------------------------------
    public void MoveOut(WRButton _)
    {
        switch (status)
        {
            case MoveStatus.AtStart:
                break;

            case MoveStatus.OnLerpMove:
                StopIn();
                LerpOut();
                break;

            case MoveStatus.OnUnfold:
                StopIn();
                FoldContent();
                break;

            case MoveStatus.AtTarget:
                StopIn();
                FoldContent();
                break;
        }
    }

    private Coroutine coroutine_LerpOut;
    private void LerpOut()
    {
        if (coroutine_LerpOut != null)
            StopCoroutine(coroutine_LerpOut);

        coroutine_LerpOut = StartCoroutine(LerpOutCoroutine());
    }
    private IEnumerator LerpOutCoroutine()
    {
        status = MoveStatus.OnLerpMove;

        Vector2 nowPos = new(transform.localPosition.x, transform.localPosition.y);
        Vector2 dir = StartPos - nowPos;

        Vector2 _targetPos = StartPos + lerpMoveOffset_All * dir.normalized;
        while ((nowPos - _targetPos).magnitude > lerpMoveOffset_All)
        {
            nowPos = Vector2.Lerp(nowPos, _targetPos, lerpValue_All);
            transform.localPosition = nowPos;
            yield return null;
        }

        status = MoveStatus.AtStart;
    }
    private Coroutine coroutine_Fold;
    private void FoldContent()
    {
        if (coroutine_Fold != null)
            StopCoroutine(coroutine_Fold);

        coroutine_Fold = StartCoroutine(FoldContentCoroutine());
    }
    private IEnumerator FoldContentCoroutine()
    {
        status = MoveStatus.OnUnfold;
        float nowPosX_Content = content.transform.localPosition.x;
        float nowPosX_Option = option.transform.localPosition.x;

        float targetPosX_Content = StartPos_ContentX - UnfoldOffset;//right
        float targetPosX_Option = StartPos_OptionX + UnfoldOffset;//right

        while (Mathf.Abs(nowPosX_Content - targetPosX_Content) > UnfoldOffset)
        {
            nowPosX_Content = Mathf.Lerp(nowPosX_Content, targetPosX_Content, lerpValue_Unfold);
            Vector3 newPos_Content = content.transform.localPosition;
            newPos_Content.x = nowPosX_Content;
            content.transform.localPosition = newPos_Content;

            nowPosX_Option = Mathf.Lerp(nowPosX_Option, targetPosX_Option, lerpValue_Unfold);
            Vector3 newPos_Option = option.transform.localPosition;
            newPos_Option.x = nowPosX_Option;
            option.transform.localPosition = newPos_Option;
            yield return null;
        }
        //no need for wait
        LerpOut();
    }

    private void StopIn()
    {
        if (coroutine_LerpIn != null)
            StopCoroutine(coroutine_LerpIn);

        if (coroutine_Unfold != null)
            StopCoroutine(coroutine_Unfold);
    }
}
