using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(HomeButtonManager))]
public class HomeButtonManagerDrawer : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        if(GUILayout.Button("Create Object Instance"))
        {
            ((HomeButtonManager)target).CreateButton();
        }
    }
}
