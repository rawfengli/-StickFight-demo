using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NameUpdater : MonoBehaviour
{

    [SerializeField] private TMP_InputField NewNameInputField;

    public void OnEndEditName()
    {
        string newName = NewNameInputField.text;
        NewNameInputField.text = string.Empty;

        PlayerInfo.SetName(newName);
    }
}
