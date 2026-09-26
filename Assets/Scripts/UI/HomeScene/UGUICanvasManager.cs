using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DefaultExecutionOrder(-100)]
public class UGUICanvasManager : MonoBehaviour
{
    [Header("Object")]
    private static UGUICanvasManager instance;
    public static UGUICanvasManager Instance => instance;
    public Canvas canvas;
    [Header("Canvas Ray Cast")]
    public GraphicRaycaster rayCaster;
    [HideInInspector]
    public PointerEventData pointerData;
    [HideInInspector]
    public List<RaycastResult> results = new();
    public List<RaycastResult> resultsWithoutText = new();
    public EventSystem eventSystem => EventSystem.current;
    public void Awake()
    {
        instance = this;
        pointerData = new PointerEventData(eventSystem);
    }
    public void Update()
    {
        results.Clear();
        resultsWithoutText.Clear();

        pointerData.position = Input.mousePosition;
        rayCaster.Raycast(pointerData, results);

        foreach (var result in results)
        {
            if(result.gameObject.TryGetComponent<TextMeshProUGUI>(out _))
                continue;
            resultsWithoutText.Add(result);
        }
    }

}
