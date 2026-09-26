using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface Snapshot
{
    double remoteTime { get; set; }
    double localTime { get; set; }
}

public struct TimeSnapshot : Snapshot
{
    public double remoteTime { get; set; }
    public double localTime { get; set; }
    public TimeSnapshot(double remoteTime, double localTime)
    {
        this.remoteTime = remoteTime;
        this.localTime = localTime;
    }
}
