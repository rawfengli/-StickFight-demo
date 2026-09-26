using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.U2D;

public class AvatarSelector : MonoBehaviour
{
    [SerializeField]
    private GameObject AvatarPrefab;
    private Vector2 step = new(200f, 0f);
    [SerializeField]
    private Vector2 starPos;
    public List<AvatarIdentity> avatars = new();
    public void CreateAvatarList()
    {
        foreach (var avatarButton in avatars)
        {
            if(avatarButton != null)
                DestroyImmediate(avatarButton.gameObject);
        }
        avatars.Clear();

        foreach (Avatars avatar in Enum.GetValues(typeof(Avatars)).Cast<Avatars>())
        {
            int index = (int)avatar;
            if (index == 0)
                continue;

            GameObject newObj = Instantiate(AvatarPrefab);

            AvatarIdentity avatarButton = newObj.GetComponent<AvatarIdentity>();
            avatarButton.avatarIndex = index;
            avatars.Add(avatarButton);

            newObj.transform.SetParent(transform);
            newObj.transform.localPosition = starPos + step * (index - 1);
        }
    }
}
