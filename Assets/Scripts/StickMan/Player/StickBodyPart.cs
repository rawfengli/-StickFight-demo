using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(Rigidbody2D))]
public abstract class StickBodyPart : MonoBehaviour
{
    public Fight owner;
    public PlayerData data;
}
