using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Hand : MonoBehaviour
{
    public Transform LowerArm;

    public void Update()
    {
        Vector3 armUp = LowerArm.up;
        Vector3 handUp = transform.up;
        transform.rotation *= Quaternion.FromToRotation(handUp, armUp);
    }
}
