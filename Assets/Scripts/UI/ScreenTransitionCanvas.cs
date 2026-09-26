using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public enum ScreenTransitionHandle
{
    Unknown = 0,
    BlackScreenFadeInAndOut = 1,
}
[DefaultExecutionOrder(-10)]
[RequireComponent(typeof(Canvas))]
public class ScreenTransitionCanvas : MonoBehaviour
{
    private static ScreenTransitionCanvas instance;
    public static ScreenTransitionCanvas Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("Load Scene Canvas");
                instance = go.AddComponent<ScreenTransitionCanvas>();
                instance.canvas.sortingOrder = 100;
                instance.canvas.renderMode = RenderMode.ScreenSpaceOverlay;

                GameObject fadeImage = new GameObject("Fade Image");
                fadeImage.transform.SetParent(go.transform);
                fadeImage.transform.localPosition = Vector3.zero;
                fadeImage.transform.localRotation = Quaternion.identity;
                instance.fadeImage = fadeImage.AddComponent<Image>();
                fadeImage.SetActive(false);

                fadeImage.GetComponent<RectTransform>().sizeDelta = new(2560, 1440);
            }
            return instance;
        }
    }
    private Canvas _canvas;
    public Canvas canvas
    {
        get
        {
            if (_canvas == null)
                _canvas = GetComponent<Canvas>();
            return _canvas;
        }
    }
    public Image fadeImage;
    private bool finish;
    private WaitForFixedUpdate waitForFixedUpdate = new();
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        finish = true;
        instance = this;
        Application.targetFrameRate = 120;
        fadeImage?.gameObject.SetActive(false);
        DontDestroyOnLoad(instance);
    }
    public void ScreenTransition(
        ScreenTransitionHandle handle,
        float fadeTime = 1f,
        float pauseTime = 1f, 
        Action OnFadeInEvent = null,
        Action OnStartFadeOutEvent = null,
        Action OnFadeFinishEvent = null)
    {
        if (!finish)
            return;
        finish = false;
        switch (handle)
        {
            case ScreenTransitionHandle.Unknown:
                break;

            case ScreenTransitionHandle.BlackScreenFadeInAndOut:
                BlackScreenFadeIn_Out(fadeTime, pauseTime, OnFadeInEvent, OnStartFadeOutEvent, OnFadeFinishEvent);
                break;
        }
    }
    private void BlackScreenFadeIn_Out(
        float fadeTime, 
        float pauseTime, 
        Action OnFadeInEvent, 
        Action OnStartFadeOutEvent, 
        Action OnFadeFinishEvent)
    {
        StartCoroutine(BlackScreenFadeIn_OutCoroutine(fadeTime, pauseTime, OnFadeInEvent, OnStartFadeOutEvent, OnFadeFinishEvent));
    }
    private IEnumerator BlackScreenFadeIn_OutCoroutine(
        float fadeTime, 
        float pauseTime, 
        Action OnFadeInEvent, 
        Action OnStartFadeOutEvent,
        Action OnFadeFinishEvent)
    {
        fadeImage.gameObject.SetActive(true);
        fadeImage.color = new Color(0, 0, 0, 0);

        float nowTime;
        nowTime = 0;
        while(nowTime < fadeTime)
        {
            float t = nowTime / fadeTime;
            Color color = new Color(0.02f, 0.02f, 0.02f, 0);
            color.a = Mathf.Lerp(-0.1f, 1.1f, t);//比1略大，防止奇怪的情况
            fadeImage.color = color;

            nowTime += Time.deltaTime;
            yield return null;
        }
        yield return waitForFixedUpdate;

        OnFadeInEvent?.Invoke();
        nowTime = 0; 
        
        while (nowTime < pauseTime)
        {
            nowTime += Time.deltaTime;
            yield return null;
        }
        OnStartFadeOutEvent?.Invoke();
        nowTime = 0;
        
        while (nowTime < fadeTime)
        {
            float t = nowTime / fadeTime;
            Color color = new Color(0.02f, 0.02f, 0.02f, 0);
            color.a = Mathf.Lerp(1.1f, -0.1f, t);
            fadeImage.color = color;

            nowTime += Time.deltaTime;
            yield return null;
        }
        OnFadeFinishEvent?.Invoke();
        fadeImage.gameObject.SetActive(false);
        finish = true;
    }

}
