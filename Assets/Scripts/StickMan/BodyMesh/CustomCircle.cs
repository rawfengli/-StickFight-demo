using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CustomCircle : BodyMeshCreator
{
    public float radius;

    private MeshFilter _meshFilter;

    public MeshFilter meshFilter
    {
        get
        {
            if (_meshFilter == null)
                _meshFilter = GetComponent<MeshFilter>();
            return _meshFilter;
        }
    }
}
