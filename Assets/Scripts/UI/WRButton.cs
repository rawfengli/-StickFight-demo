using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UnityEngine.GraphicsBuffer;

/// <summary>
/// 或许改成 IPointerHandler一类的会更好
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class WRButton : MonoBehaviour
{
    protected bool IsHover;
    protected bool Pressed;
    protected RectTransform rect;
    public bool buttonActive;
    public Action OnNormal;
    public Action OnHover;
    public Action<WRButton> OnPressed;
    public Action<WRButton> OnReleased;

    protected virtual void Awake()
    {
        rect = GetComponent<RectTransform>();
        buttonActive = true;
    }
    protected virtual void OnEnable()
    {
        OnNormal?.Invoke();
    }
    protected virtual void OnDisable()
    {

    }

    protected virtual void Update()
    {
        if (buttonActive == false)
            return;
        bool inside = RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, null);
        inside = IsMouseInside();

        if (Pressed && Input.GetMouseButtonUp(0))
        {
            Pressed = false;
            OnReleased?.Invoke(this);
            OnNormal?.Invoke();
            return;
        }

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
    protected bool IsMouseInside()
    {
        GraphicRaycaster rayCaster = UGUICanvasManager.Instance.rayCaster;
        EventSystem eventSystem = EventSystem.current;

        List<RaycastResult> results = UGUICanvasManager.Instance.resultsWithoutText;
        if (results.Count > 0)
        {
            GameObject hitObject = results[0].gameObject;
            if (hitObject == gameObject)
                return true;
            else
                return false;
        }
        else
            return false;
    }
}
