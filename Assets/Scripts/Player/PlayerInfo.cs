using System;
using System.Collections.Generic;
using UnityEngine;
public enum Avatars
{
    Unknown = 0,
    Default = 1,
    Crown = 2,
}

public static class PlayerInfo
{
    public static void GetPlayerColor(int playerIndex, out Color color)
    {
        switch (playerIndex)
        {
            case 1:
                ColorUtility.TryParseHtmlString("#FF3200", out color);
                break;
            case 2:
                ColorUtility.TryParseHtmlString("#64B4FF", out color);
                break;
            case 3:
                ColorUtility.TryParseHtmlString("#7DFF00", out color);
                break;
            case 4:
                ColorUtility.TryParseHtmlString("#FFC800", out color);
                break;
            default:
                ColorUtility.TryParseHtmlString("#FFFFFF", out color);
                break;
        }
    }
    public static void DrawTextWithPlayerColor(int playerIndex, ref string content)
    {
        switch (playerIndex)
        {
            case 1:
                content = "<color=#FF3200>" + content + "</color=#FF3200>";
                break;
            case 2:
                content = "<color=#64B4FF>" + content + "</color=#64B4FF>";
                break;
            case 3:
                content = "<color=#7DFF00>" + content + "</color=#7DFF00>";
                break;
            case 4:
                content = "<color=#FFC800>" + content + "</color=#FFC800>";
                break;
            default:
                content = "<color=white>" + content + "</color>";
                break;
        }
    }
    public static int avatarIndex { get; private set; } = 1;
    public static string playerName { get; private set; } = "玩家";
    private static Sprite avatarSprite;

    private static List<Sprite> avatarsSprite = new();

    private static Action<Sprite> OnSetAvatar;
    private static Action<string> OnSetPlayerName;

    public static void SetAvatar(int index, Sprite sprite)
    {
        avatarIndex = index;
        avatarSprite = sprite;
        OnSetAvatar?.Invoke(avatarSprite);
    }

    public static void SetName(string name)
    {
        playerName = name;
        OnSetPlayerName?.Invoke(playerName);
    }

    public static void ClearRegisteredAvatars()
        => avatarsSprite.Clear();
    public static void RegisterAvatar(Sprite sprite)
        => avatarsSprite.Add(sprite);
    public static Sprite GetAvatar(Avatars avatar)
    {
        if (avatar != Avatars.Unknown)
            return avatarsSprite[(int)avatar - 1];
        else
            return null; 
    }
    public static Sprite GetAvatar(int avatar)
    {
        if (avatar != 0)
            return avatarsSprite[avatar - 1];
        else
        {
            Debug.LogWarning("Avatar index is 0");
            return null;
        }
    }
    public static void Register_SetAvatar_Event(Action<Sprite> action)
        => OnSetAvatar += action;
    public static void Register_SetPlayerName_Event(Action<string> action)
        => OnSetPlayerName += action;

    public static void Unregister_SetAvatar_Event(Action<Sprite> action)
    => OnSetAvatar -= action;
    public static void Unregister_SetPlayerName_Event(Action<string> action)
        => OnSetPlayerName -= action;
}
