using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class SettingOptionButton : WRButton
{
    public Image image;
    [HideInInspector]
    public int index = 0;
    public bool isActived { get; set; }
    /// <summary>
    /// OnOtherReleased会在active变更前触发
    /// </summary>
    public Action<SettingOptionButton> OnOtherReleased;
    protected override void Awake()
    {
        base.Awake();
        image = GetComponent<Image>();
    }
    protected override void OnEnable()
    {
        OnOtherReleased += OtherReleased;

        OnNormal += OnNormal_UI;
        OnHover += OnHover_UI;
        OnReleased += OnReleased_UI;

        base.OnEnable();

    }
    protected override void OnDisable()
    {
        OnOtherReleased -= OtherReleased;

        OnNormal -= OnNormal_UI;
        OnHover -= OnHover_UI;
        OnReleased -= OnReleased_UI;

        base.OnDisable();
    }
    protected override void Update()
    {
        if (buttonActive == false)
            return;

        if (isActived)//pressed
            return;

        bool inside = RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, null);
        inside = IsMouseInside();

        if (Pressed && Input.GetMouseButtonUp(0))
        {
            Pressed = false;
            OnReleased?.Invoke(this);
            return;
        }

        if (Pressed)
            return;

        if (inside && Input.GetMouseButtonDown(0))
        {
            Pressed = true;
            OnPressed?.Invoke(this);
            return;
        }

        if (IsHover != inside)
        {
            if (inside)
            {
                OnHover?.Invoke();
            }
            else
            {
                OnNormal?.Invoke();
            }
        }
        IsHover = inside;
    }

    private void OtherReleased(SettingOptionButton button)
    {
        OnNormal?.Invoke();
        isActived = false;
    }
    //---------------------------------------

    protected virtual void OnNormal_UI()
    {
        Color color = new Color(0.8f, 0.8f, 0.8f, 1.0f);
        image.color = color;
    }
    //---------------------------------------
    protected virtual void OnHover_UI()
    {
        Color color = new Color(1f, 1f, 1f, 1.0f);
        image.color = color;
    }
    //---------------------------------------

    protected virtual void OnReleased_UI(WRButton _)
    {
        isActived = true;

        Color color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        image.color = color;
    }
}
