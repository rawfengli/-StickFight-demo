using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerInRoom : MonoBehaviour
{
    public Image avatarImage;
    public TextMeshProUGUI playerName;

    public void UpdateInfo(int avatarIndex, string name)
    {
        if(IsIndexVaild(avatarIndex))
        {
            avatarImage.color = Color.white;
            avatarImage.sprite = PlayerInfo.GetAvatar(avatarIndex);
            playerName.text = name;
        }
        else
        {
            avatarImage.color = new Color(0, 0, 0, 0);
            avatarImage.sprite = null;
            playerName.text = "空位";
        }
    }
    private bool IsIndexVaild(int index)
        => index >= 1;
}
