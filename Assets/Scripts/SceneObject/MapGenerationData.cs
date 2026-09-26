using System.Collections.Generic;
using UnityEngine;

public class SceneGenerationData : MonoBehaviour
{
    public List<Vector2> PlayerGenerationPosition = new List<Vector2>();
    public int activePlayerCount;

    public void OnValidate()
    {
        if(PlayerGenerationPosition == null || PlayerGenerationPosition.Count != activePlayerCount)
        {
            Debug.LogWarning("Player Generation Position Must Be Same With ActivePlayerCount!");
        }
    }
}
