using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct TransformSnapshot : Snapshot
{

    public double remoteTime { get; set; }
    public double localTime { get; set; }

    public Vector3 position;
    public Quaternion rotation;
    public Vector3 scale;

    public static TransformSnapshot Interpolate(TransformSnapshot from, TransformSnapshot to, float t)
    {
        TransformSnapshot Interpolation = new TransformSnapshot()
        {
            remoteTime = 0, 
            localTime = 0,
            position = Vector3.LerpUnclamped(from.position, to.position, t),
            rotation = Quaternion.SlerpUnclamped(from.rotation, to.rotation, t),
            scale = Vector3.LerpUnclamped(from.scale, to.scale, t)

        };
        return Interpolation;
    }
}
