using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AvatarIdentity : MonoBehaviour
{
    [SerializeField] public WRButton button;
    [SerializeField] public int avatarIndex;
    public Image image;
    private Sprite sprite;
    private void OnEnable()
    {
        button.OnReleased += OnReleased;
    }
    private void OnDisable()
    {
        button.OnReleased -= OnReleased;
    }
    public void Init(int avatarIndex, Sprite sprite)
    {
        this.sprite = sprite;
        image.sprite = this.sprite;
        image.color = Color.white;
    }
    private void OnReleased(WRButton _)
    {
        PlayerInfo.SetAvatar(avatarIndex, sprite);
    }

}
