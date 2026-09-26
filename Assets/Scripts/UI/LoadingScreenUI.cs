using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;


public class LoadingScreenUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI text;
    private string resourceName;
    public void SetLoadingResourceName(string name)
        => resourceName = name;
    public void OnLoading(float percent)
    {
        float _percent = (float)Math.Round(percent * 100.0f, 2);
        text.text = $"等待{resourceName}资源加载 " + _percent.ToString() + "%";
    }
    public void OnSucceeded()
    {
        text.text = $"{resourceName}加载完成";
    }
    public void OnFailed()
    {
        text.text = $"{resourceName}资源加载失败";
    }
    public void OnAllLoaded()
    {
        text.text = $"资源加载完成，稍作等待";
    }
    public void OnEnable()
    {
        LoadingScreenCanvas.Instance.OnLoadingEvent += OnLoading;
        LoadingScreenCanvas.Instance.OnSucceededEvent += OnSucceeded;
        LoadingScreenCanvas.Instance.OnFailedEvent += OnFailed;
        LoadingScreenCanvas.Instance.OnAllLoadedEvent += OnAllLoaded;

    }
    public void OnDisable()
    {
        LoadingScreenCanvas.Instance.OnLoadingEvent -= OnLoading;
        LoadingScreenCanvas.Instance.OnSucceededEvent -= OnSucceeded;
        LoadingScreenCanvas.Instance.OnFailedEvent -= OnFailed;
        LoadingScreenCanvas.Instance.OnAllLoadedEvent += OnAllLoaded;
    }
}
