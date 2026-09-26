using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// Begin + End 一组算一次时间采样
/// </summary>
public struct TimeSample
{
    private readonly Stopwatch stopwatch;
    private double beginTime;
    public double average;
    ExponentialMovingAverage ema;
    public TimeSample(int n)
    {
        stopwatch = new Stopwatch();
        stopwatch.Start();
        ema = new ExponentialMovingAverage(n);
        beginTime = 0;
        average = 0;
    }
    public void Begin()
    {
        beginTime = stopwatch.Elapsed.TotalSeconds;
    }
    public void End()
    {
        double elapsed = stopwatch.Elapsed.TotalSeconds - beginTime;
        ema.Add(elapsed);

        Interlocked.Exchange(ref average, ema.Value);
    }
}
