using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Unity.VisualScripting.Antlr3.Runtime.Tree;
using UnityEngine;

//这些单例应该继承一个单例基类的
public class MenuButtonManager : MonoBehaviour
{
    private static MenuButtonManager instance;
    public static MenuButtonManager Instance
    {
        get
        {
            if (instance != null)
                return instance;
            else
                throw new Exception("Instance not exist or not yet initialized");
        }
    }
    [Header("Other Managers")]
    [SerializeField] private HomeButtonManager homeButtonManager;
    [Header("Button")]
    public List<MenuButton> buttons = new();

    [Header("Button Move")]
    public float lerpValue = 0.02f;
    public float startMoveInterval = 0.1f;
    public bool movingOut { get; set; }//true - out, false - to start
    public int count => buttons.Count;
    private Coroutine coroutine;
    [Header("Audio")]
    public AudioClip hoverClip;
    public AudioClip releasedClip;

    private void Awake()
    {
        instance = this;
        movingOut = false;
        foreach (var button in buttons)
        {
            button.menuButtonManager = this;
        }

    }
    private void OnEnable()
    {

        //home button
        foreach (var button in homeButtonManager.buttons)
        {
            button.OnReleased += DoAllButtonsMove_HomeButton;
            button.OnReleased += OnReleased_Audio;
            button.OnHover += OnHover_Audio;
        }
    }
    private void OnDisable()
    {
        //home button
        foreach (var button in homeButtonManager.buttons)
        {
            button.OnReleased -= DoAllButtonsMove_HomeButton;
            button.OnReleased -= OnReleased_Audio;
            button.OnHover -= OnHover_Audio;
        }
    }
    private void OnHover_Audio()
    {
        AudioPlayer.Instance.Play(hoverClip);
    }
    private void OnReleased_Audio(WRButton _)
    {
        AudioPlayer.Instance.Play(releasedClip);
    }
    #region Lerp Move
    private bool moveImmediately;
    public void DoAllButtonsMove_HomeButton(WRButton invokedButton)
    {
        HomeButton button = (HomeButton)invokedButton;
        //如果button是让home界面显现，且正在移动的协程(或以及移动完成的协程)的目的地方向和其一致，则忽略这次调度
        if (button.GoHome == (!movingOut))
            return;

        movingOut ^= true;

        moveImmediately = false;
        if (coroutine != null)
            StopCoroutine(coroutine);

        coroutine = StartCoroutine(MoveCoroutine());
    }
    private IEnumerator MoveCoroutine()
    {
        foreach (var button in buttons)
        {
            button.lerpValue = lerpValue;
            button.DoMove();
            float nowTime = 0;
            while(nowTime < startMoveInterval && moveImmediately == false)
            {
                nowTime += Time.deltaTime;
                yield return null;
            }
        }
    }
    #endregion
}
