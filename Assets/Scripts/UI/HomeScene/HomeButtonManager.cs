using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.PlayerLoop;
using UnityEngine.UI;
public enum HomeOptionIndex : int
{
    Home = 0,
    Setting = 1,
    Room = 2
}
[RequireComponent(typeof(AudioSource))]
public class HomeButtonManager : MonoBehaviour
{
    public HomeButton this[HomeOptionIndex index]
    {
        get => buttons[(int)index];
    }

    private static HomeButtonManager instance;
    public static HomeButtonManager Instance
    {
        get
        {
            if (instance != null)
                return instance;
            else
                throw new Exception("Instance not exist or not yet initialized");
        }
    }

    [Header("Prefabs")]
    public GameObject parentObj;
    public List<GameObject> prefabs = new();
    public Sprite border_1;
    public Sprite border_2;
    public Sprite border_3;

    [Header("Run Time")]
    public List<HomeButton> buttons;
    public int NowActiveIndex = 0;
    /// <summary>
    /// 这个是为了保证在OnOtherButtonRelease触发时，相关的事件也可以访问到正确的上一个active的index
    /// 从而可以忽视Release事件和Other Release事件之间可能存在的顺序要求
    /// </summary>
    private int ActiveIndexCache { get; set; }

    [Header("Audio")]
    public AudioSource source;
    public AudioClip hoverClip;
    public AudioClip releasedClip;

    public void Awake()
    {
        source = GetComponent<AudioSource>();
        instance = this;
    }
    public void CreateButton()
    {
        int index = 0;
        float x = 0;


        foreach (var button in buttons)
        {
            if (button != null)
                DestroyImmediate(button.gameObject);
        }
        buttons.Clear();

        foreach (var prefab in prefabs)
        {
            Vector3 pos = parentObj.transform.position;
            pos.x += x * index;

            GameObject obj = GameObject.Instantiate(prefab, pos, Quaternion.identity);
            x = obj.GetComponent<RectTransform>().rect.width;
            obj.transform.SetParent(parentObj.transform, true);

            HomeButton button = obj.GetComponent<HomeButton>();
            button.index = index;
            buttons.Add(button);
            index++;
            if (index == 1)
            {
                obj.GetComponent<Image>().sprite = border_1;
            }
            else if (index == prefabs.Count)
            {
                obj.GetComponent<Image>().sprite = border_3;
            }
            else
            {
                obj.GetComponent<Image>().sprite = border_2;
            }
        }
    }
    private void OnEnable()
    {
        NowActiveIndex = 0;

        foreach (var button in buttons)
        {
            button.OnReleased += OnButtonReleased;
            button.OnReleased += OnReleased_Audio;
            button.OnHover += OnHover_Audio;
        }
    }
    private void OnDisable()
    {
        foreach (var button in buttons)
        {
            button.OnReleased -= OnButtonReleased;
            button.OnReleased -= OnReleased_Audio;
            button.OnHover -= OnHover_Audio;
        }

    }
    private void Start()
    {
        SetOptionActive(0, true, false);
    }
    public void SetOptionActive(int index, bool NotInvokeAudio, bool selfReturn = true)
    {
        if (NowActiveIndex == index && selfReturn)
            return;

        if (NotInvokeAudio == true)
            buttons[index].OnReleased -= OnReleased_Audio;

        buttons[index].OnReleased.Invoke(buttons[index]);
        NowActiveIndex = index;

        if (NotInvokeAudio == true)
            buttons[index].OnReleased += OnReleased_Audio;
    }

    public void OnButtonReleased(WRButton button)
    {
        if (button is HomeButton targetButton)
        {
            if (NowActiveIndex == targetButton.index)
                return;

            ActiveIndexCache = NowActiveIndex;

            buttons[NowActiveIndex].OnOtherReleased(buttons[targetButton.index]);//target
            NowActiveIndex = targetButton.index;

            ActiveIndexCache = -1;
        }
        else
        {
            Debug.LogError("type of button not match while button event invoke");
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
}
