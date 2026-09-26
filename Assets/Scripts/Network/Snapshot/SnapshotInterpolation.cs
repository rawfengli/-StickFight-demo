using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SnapshotInterpolationSettings
{
    [Header("Buffering")]
    public int bufferLimit = 32;

    public double bufferTimeMultiplier = 2;

    [Header("Catchup / Slowdown")]
    public float catchupNegativeThreshold = -1;
    public float catchupPositiveThreshold = 1;

    [Range(0, 1)]
    public double catchupSpeed = 0.02f;
    [Range(0, 1)]
    public double slowdownSpeed = 0.04f;

    public int driftEmaDuration = 1;
    public int deliveryTimeEmaDuration = 2;
}
public static class SnapshotInterpolation 
{
    //insert
    public static bool InsertIfNotExists<T>(SortedList<double, T> buffer, int bufferLimit, T snapshot)
        where T : Snapshot
    {
        if (buffer.Count >= bufferLimit) 
            return false;

        int beforeCount = buffer.Count;
        buffer[snapshot.remoteTime] = snapshot;
        if (buffer.Count > beforeCount)
            return true;
        else
            return false;
    }

    public static void InsertAndAdjust<T>(
        SortedList<double, T> buffer,
        T snapshot,
        int bufferLimit,
        ref double localTimeline,
        ref double localTimescale,
        ref ExponentialMovingAverage driftEma,
        ref ExponentialMovingAverage deliveryTimeEma,//快照间隔插值
        double bufferTime,
        double sendInterval,
        double catchupSpeed,
        double slowdownSpeed,
        float catchupNegativeThreshold,
        float catchupPositiveThreshold)
        where T : Snapshot
    {
        if (buffer.Count == 0)
            localTimeline = snapshot.remoteTime - bufferTime;

        if (InsertIfNotExists(buffer, bufferLimit, snapshot))
        {
            if(buffer.Count > 2)
            {
                int count = buffer.Count;
                double t0 = buffer.Values[count - 2].localTime;
                double t1 = buffer.Values[count - 1].localTime;//如果成功插入，这个是最新的快照
                double dt = t1 - t0;

                deliveryTimeEma.Add(dt);
            }

            localTimeline = TimelineClamp(localTimeline, bufferTime, snapshot.remoteTime);

            double timeDiff = snapshot.remoteTime - localTimeline;
            driftEma.Add(timeDiff);
            double drift = driftEma.Value - bufferTime;
            double absoluteNegativeThreshold = sendInterval * catchupNegativeThreshold;
            double absolutePositiveThreshold = sendInterval * catchupPositiveThreshold;

            localTimescale = Timescale(
                drift, 
                catchupSpeed, slowdownSpeed, 
                absoluteNegativeThreshold, absolutePositiveThreshold);

        }
    }
    public static double Timescale(
        double drift,                    
        double catchupSpeed,             
        double slowdownSpeed,            
        double absoluteCatchupNegativeThreshold, 
        double absoluteCatchupPositiveThreshold) 
    {
        if (drift > absoluteCatchupPositiveThreshold)
        {
            return 1 + catchupSpeed; 
        }

        if (drift < absoluteCatchupNegativeThreshold)
        {
            return 1 - slowdownSpeed; 
        }
        return 1;
    }
    //我们希望最后能到达latestRemoteTime - buffer的这个时间，同时存在bufferTime的上下缓冲区间
    private static double TimelineClamp(double localTimeline, double bufferTime, double latestRemoteTime)
    {
        double targetTime = latestRemoteTime - bufferTime;

        double lowerBound = targetTime - bufferTime; 
        double upperBound = targetTime + bufferTime; 
        return Math.Clamp(localTimeline, lowerBound, upperBound);
    }

    //update
    private static void Sample<T>(
        SortedList<double, T> buffer, double localTimeline, 
        out int from, out int to, out double t)
        where T : Snapshot
    {
        from = -1;
        to = -1;
        t = 0;
        for (int i = 0; i < buffer.Count - 1; i++)
        {
            T first = buffer.Values[i];
            T second = buffer.Values[i + 1];
            if(first.remoteTime <= localTimeline && localTimeline <= second.remoteTime)
            {
                from = i;
                to = i + 1;
                //a != b ? Clamp01((value - a) / (b - a)) : 0;
                if(first.remoteTime - second.remoteTime != 0)
                {
                    t = (localTimeline - first.remoteTime) / (second.remoteTime - first.remoteTime);
                    t = Math.Clamp(t, 0.0, 1.0);
                }
                else
                {
                    t = 0;
                }    
                return;
            }
        }

        if (buffer.Values[0].remoteTime > localTimeline)
        {
            from = to = 0;
            t = 0;
        }
        else
        {
            from = to = buffer.Count - 1;
            t = 0;
        }
    }


    public static void UpdateTime(double deltaTime, ref double localTimeline, double localTimescale)
        => localTimeline += deltaTime * localTimescale;


    public static void StepInterpolation<T>(
        SortedList<double, T> buffer, double localTimeline,
        out T fromSnapshot, out T toSnapshot, out double t)
        where T : Snapshot
    {
        Sample(buffer, localTimeline, out int from, out int to, out t);

        fromSnapshot = buffer.Values[from];
        toSnapshot = buffer.Values[to];

        for (int i = 0; i < from && i < buffer.Count; i++)
            buffer.RemoveAt(0);
    }
}
