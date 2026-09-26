using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
[CustomEditor(typeof(AvatarSelector))]
public class AvatarSelectorDrawer : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        if (GUILayout.Button("Create Object Instance"))
        {
            ((AvatarSelector)target).CreateAvatarList();
        }
    }
}
