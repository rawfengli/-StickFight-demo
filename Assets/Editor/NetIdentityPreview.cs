using System.Collections.Generic;
using System.Configuration;
using UnityEditor;
using UnityEngine;
using static PlasticGui.WorkspaceWindow.Merge.MergeInProgress;

[CustomPreview(typeof(GameObject))]
public class NetIdentityPreview : ObjectPreview
{
    public GUIStyle labelStyle = new GUIStyle(EditorStyles.label);
    public GUIStyle valueStyle = new GUIStyle(EditorStyles.boldLabel);
    public GUIStyle disabledName = new GUIStyle(EditorStyles.miniLabel);

    private GUIContent title;
    public override GUIContent GetPreviewTitle()
    {
        if (title == null)
        {
            title = new GUIContent("Network Information");
        }
        return title;
    }
    public override bool HasPreviewGUI()
    {
        return target != null && target is GameObject gameObject && gameObject.GetComponent<NetIdentity>() != null;
    }
    public override void OnPreviewGUI(Rect r, GUIStyle background)
    {
        if (Event.current.type != EventType.Repaint)
            return;

        GameObject targetGameObject = target as GameObject;
        NetIdentity identity = targetGameObject.GetComponent<NetIdentity>();

        if (identity == null)
            return;

        float x = 30;
        float y = 30;

        y = DrawNetIdentityInfo(identity, x, y);

        y = DrawNetBehaviours(identity, x, y);

        y = DrawObservers(identity, x, y);

        y = DrawOwner(identity, x, y);
    }

    private float DrawNetIdentityInfo(NetIdentity identity, float x, float y)
    {
        Vector2 maxNameLabelSize = new Vector2(140, 16);
        Vector2 maxValueLabelSize = new Vector2(140, 16);

        Rect LabelRect = new Rect(x, y, maxNameLabelSize.x, maxNameLabelSize.y);
        Rect ValueRect = new Rect(x + maxNameLabelSize.x, y, maxValueLabelSize.x, maxValueLabelSize.y + 1);

        DrawNetIdentityProperty(ref LabelRect, ref ValueRect, "Asset ID", identity.assetID.ToString());
        DrawNetIdentityProperty(ref LabelRect, ref ValueRect, "Scene ID", identity.sceneID.ToString());
        DrawNetIdentityProperty(ref LabelRect, ref ValueRect, "Network ID", identity.netID.ToString());
        DrawNetIdentityProperty(ref LabelRect, ref ValueRect, "Is Client", identity.isClient.ToString());
        DrawNetIdentityProperty(ref LabelRect, ref ValueRect, "Is Server", identity.isServer.ToString());
        DrawNetIdentityProperty(ref LabelRect, ref ValueRect, "Is Owned", identity.isOwned.ToString());

        return LabelRect.y;
    }
    private void DrawNetIdentityProperty(ref Rect labelRect, ref Rect valueRect, string label, string value)
    {
        GUI.Label(labelRect, label, labelStyle);
        GUI.Label(valueRect, value, valueStyle);
        labelRect.y += labelRect.height;
        valueRect.y += valueRect.height;

    }

    private float DrawNetBehaviours(NetIdentity identity, float x, float y)
    {
        Rect behaviourRect = new Rect(x, y + 10, 200, 20);
        
        GUI.Label(behaviourRect, new GUIContent("Network Behaviours"), labelStyle);
        behaviourRect.x += 20;
        behaviourRect.y += behaviourRect.height;

        y = behaviourRect.y;
        if (identity.behaviours == null)
            return y;

        for(int i = 0; i < identity.behaviours.Length; i++)
        {
            if (identity.behaviours[i] == null)
                continue;

            GUI.Label(behaviourRect, identity.behaviours[i].GetType().FullName, valueStyle);

            behaviourRect.y += behaviourRect.height;
            y = behaviourRect.y;
        }

        return y;
    }

    private float DrawObservers(NetIdentity identity, float x, float y)
    {
        if (identity.observers.Count > 0)
        {
            Rect observerRect = new Rect(x, y + 10, 200, 20);

            GUI.Label(observerRect, "Network observers", labelStyle);
            
            observerRect.x += 20;
            observerRect.y += observerRect.height;

            foreach (KeyValuePair<int, NetConnectionToClient> kvp in identity.observers)
            {
                GUI.Label(observerRect, $"{kvp.Value.remoteAddress}:{kvp.Value}", valueStyle);
                observerRect.y += observerRect.height;
                y = observerRect.y;
            }
        }

        return y;
    }
    private float DrawOwner(NetIdentity identity, float x, float y)
    {
        if (identity.connectionToClient != null)
        {
            Rect ownerRect = new Rect(x, y + 10, 400, 20);
            GUI.Label(ownerRect, $"Client Connection: {identity.connectionToClient}", labelStyle);
            y += ownerRect.height;
        }
        return y;
    }

}
