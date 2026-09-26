using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class CommonWRButton : WRButton
{
    private Image image;

    protected override void Awake()
    {
        base.Awake();
        image = GetComponent<Image>();
    }
    protected override void OnEnable()
    {
        base.OnEnable();
        OnNormal += OnNormal_UI;
        OnHover += OnHover_UI;
        OnPressed += OnPressed_UI;
    }
    protected override void OnDisable()
    {
        base.OnDisable();
        OnNormal -= OnNormal_UI;
        OnHover -= OnHover_UI;
        OnPressed -= OnPressed_UI;
    }

    protected virtual void OnNormal_UI()
    {
        Color color = new Color(0.6f, 0.6f, 0.6f, 1);
        image.color = color;
    }
    protected virtual void OnHover_UI()
    {
        Color color = new Color(0.8f, 0.8f, 0.8f, 1);
        image.color = color;
    }
    protected virtual void OnPressed_UI(WRButton _)
    {
        Color color = new Color(0.3f, 0.3f, 0.3f, 1);
        image.color = color;
    }
}
