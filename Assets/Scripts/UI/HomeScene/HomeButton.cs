using System;
using TMPro;
using UnityEngine;

public abstract class HomeButton : WRButton
{
    public abstract bool GoHome { get; }
    [HideInInspector]
    public int index;
    private bool isActived { get; set; }
    public Action<HomeButton> OnOtherReleased;

    [SerializeField] private TextMeshProUGUI text;

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

    private void OtherReleased(HomeButton button)
    {
        OnNormal?.Invoke();
        isActived = false;
    }
    //---------------------------------------

    protected virtual void OnNormal_UI()
    {
        text.color = new Color(1, 1, 1, 0.2f);
        text.fontStyle = FontStyles.Normal;
    }
    //---------------------------------------
    protected virtual void OnHover_UI()
    {
        text.color = new Color(1, 1, 1, 0.4f);
        text.fontStyle = FontStyles.Normal;
    }
    //---------------------------------------

    protected virtual void OnReleased_UI(WRButton _)
    {
        isActived = true;

        text.color = new Color(1, 1, 1, 1f);
        text.fontStyle = FontStyles.Bold;
    }
}
