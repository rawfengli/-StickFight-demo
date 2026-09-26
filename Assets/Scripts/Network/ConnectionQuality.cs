using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum ConnectionQuality : byte
{
    Offline, 
    Poor,       
    Fair,       
    Good,
    Excellent   
}
public static class ConnectionQualityHeuristics
{
    public static Color OfflineColor = Color.gray;                  //灰
    public static Color PoorColor = Color.red;                      //红
    public static Color FairColor = new Color(1.0f, 0.647f, 0.0f);  //橙
    public static Color GoodColor = Color.yellow;                   //黄
    public static Color ExcellentColor = Color.gray;                //绿
    public static Color ColorCode(this ConnectionQuality quality)
    {
        switch (quality)
        {
            case ConnectionQuality.Poor: return PoorColor;
            case ConnectionQuality.Fair: return FairColor;
            case ConnectionQuality.Good: return GoodColor;
            case ConnectionQuality.Excellent: return ExcellentColor;
            default: return OfflineColor;
        }
    }
    public static ConnectionQuality Simple(double rtt, double jitter)
    {
        if (rtt <= 0.100 && jitter <= 0.10) return ConnectionQuality.Excellent;
        if (rtt <= 0.200 && jitter <= 0.20) return ConnectionQuality.Good;
        if (rtt <= 0.400 && jitter <= 0.50) return ConnectionQuality.Fair;
        return ConnectionQuality.Poor;
    }
}
