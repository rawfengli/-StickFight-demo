using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HomePlayerInfoLayout : MonoBehaviour
{

    public TextMeshProUGUI playerNameText;
    public Image avatarImage;

    private void OnEnable()
    {
        PlayerInfo.Register_SetPlayerName_Event(OnNameChange);
        PlayerInfo.Register_SetAvatar_Event(OnAvatarChange);
    }
    private void OnDisable()
    {
        PlayerInfo.Unregister_SetPlayerName_Event(OnNameChange);
        PlayerInfo.Unregister_SetAvatar_Event(OnAvatarChange);
    }
    private void OnNameChange(string name)
        => playerNameText.text = name;
    private void OnAvatarChange(Sprite sprite)
        => avatarImage.sprite = sprite;
}
