using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
public class MenuButton : WRButton
{
    public MenuButtonManager menuButtonManager { get; set; }
    [Header("Lerp Move")]
    public Vector2 startPos;
    public Vector2 targetPos;

    public float lerpValue = 0.05f;
    private bool movingOut;
    private Coroutine coroutine;
    [Header("Button UI")]
    [SerializeField] private Image image;
    [SerializeField] private TextMeshProUGUI text;
    protected override void Awake()
    {
        base.Awake();
        startPos = new Vector2(transform.localPosition.x, transform.localPosition.y);
    }
    protected override void OnEnable()
    {
        base.OnEnable();
        OnNormal += OnNormal_UI;
        OnHover += OnHover_Audio;
        OnHover += OnHover_UI;
    }
    protected override void OnDisable()
    {
        base.OnDisable();
        OnNormal -= OnNormal_UI;
        OnHover -= OnHover_Audio;
        OnHover -= OnHover_UI;
    }
    #region Move
    public void DoMove()
    {
        if (coroutine != null)
        {
            StopCoroutine(coroutine);
        }
        coroutine = StartCoroutine(Move());
    }
    IEnumerator Move()
    {
        movingOut = menuButtonManager.movingOut;
        Vector2 _target;
        if (movingOut)
            _target = targetPos;
        else
            _target = startPos;

        while (true)    
        {
            Vector2 nowPos = new Vector2(transform.localPosition.x, transform.localPosition.y);

            if ((nowPos - _target).magnitude < 1f)
                break;

            nowPos = Vector2.Lerp(nowPos, _target, lerpValue);
            transform.localPosition = new(nowPos.x, nowPos.y, 0);
            yield return null;
        }
    }
    #endregion
    protected virtual void OnNormal_UI()
    {
        Color color = new Color(0.7f, 0.7f, 0.7f, 1);
        text.color = color;
        image.color = color;
        text.fontStyle = FontStyles.Bold;
    }
    protected virtual void OnHover_Audio()
    {

    }
    protected virtual void OnHover_UI()
    {
        Color color = new Color(1f, 1f, 1f, 1);
        text.color = color;
        image.color = color;
        text.fontStyle = FontStyles.Bold;
    }

}
