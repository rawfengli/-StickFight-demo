using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SettingContentList : MonoBehaviour
{
    public GameObject this[SettingOptionType index]
    {
        get => contents[(int)index];
    }
    public GameObject this[int index]
    {
        get => contents[index];
    }
    [SerializeField]
    public List<GameObject> contents = new List<GameObject>();
    public void EnableContent(int index)
    {
        contents[index].SetActive(true);
    }
    public void DisableContent(int index)
    {
        contents[index].SetActive(false);
    }
}
