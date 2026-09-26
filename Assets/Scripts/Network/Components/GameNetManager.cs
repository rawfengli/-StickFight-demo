using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-101)]
public class GameNetManager : NetManager
{
    [Header("Game")]
    [SerializeField]
    private float TryToConnectInterval = 10f;
    private float NowTime = 0;
    protected override void Awake()
    {
        NowTime = TryToConnectInterval;
    }
    private void Update()
    {
        if(NetClient.ready == false)
        {
            NowTime += Time.deltaTime;
            if(NowTime >= TryToConnectInterval)
            {
                StartClient();
                NowTime = 0;
            }
        }
        else
        {
            NowTime = 0;
        }
    }
}
