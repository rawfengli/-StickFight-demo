using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public enum RoomOptionType
{
    CreateRoom = 0,
    JoinRoom = 1,
    AvailableRoom = 2
}
public class RoomOptionList : MonoBehaviour
{
    [Header("Option Content")]
    [SerializeField]
    private RoomContentList contentList;

    [Header("Buttons")]
    [SerializeField] private List<RoomOptionButton> buttons = new();
    public int NowActiveIndex = 0;
    private int ActiveIndexCache { get; set; }

    [Header("Audio")]
    public AudioClip releasedClip;

    public void Awake()
    {
        for (int i = 0; i < buttons.Count; i++)
        {
            buttons[i].index = i;
        }
    }

    private void OnEnable()
    {
        NowActiveIndex = 0;
        foreach (var button in buttons)
        {
            button.OnReleased += OnButtonReleased;
            button.OnReleased += OnReleased_Audio;
            button.OnReleased += OnReleased_EnableContent;

            button.OnOtherReleased += OnOtherButtonReleased_DisableContent;
        }
    }
    private void OnDisable()
    {
        foreach (var button in buttons)
        {
            button.OnReleased -= OnButtonReleased;
            button.OnReleased -= OnReleased_Audio;
            button.OnReleased -= OnReleased_EnableContent;

            button.OnOtherReleased -= OnOtherButtonReleased_DisableContent;
        }

    }
    private void Start()
    {
        SetOptionActive(RoomOptionType.AvailableRoom, true);
    }

    public void SetOptionActive(RoomOptionType option, bool NotInvokeAudio, bool selfReturn = true)
    {
        if (NowActiveIndex == (int)option && selfReturn)
            return;

        if (NotInvokeAudio == true)
            buttons[(int)option].OnReleased -= OnReleased_Audio;

        buttons[(int)option].OnReleased.Invoke(buttons[(int)option]);
        NowActiveIndex = (int)option;

        if (NotInvokeAudio == true)
            buttons[(int)option].OnReleased += OnReleased_Audio;
    }

    private void OnButtonReleased(WRButton button)
    {
        if (button is RoomOptionButton targetButton)
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
    private void OnOtherButtonReleased_DisableContent(WRButton button)
    {
        if (button is RoomOptionButton targetButton)
        {
            int index = ActiveIndexCache;
            contentList.DisableContent(index);
        }
        else
        {
            Debug.LogError("type of button not match while button event invoke");
        }
    }
    private void OnReleased_EnableContent(WRButton button)
    {
        if (button is RoomOptionButton targetButton)
        {
            int index = targetButton.index;
            contentList.EnableContent(index);
        }
        else
        {
            Debug.LogError("type of button not match while button event invoke");
        }
    }

    private void OnReleased_Audio(WRButton _)
    {

    }
}
