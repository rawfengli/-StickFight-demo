using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameTest : MonoBehaviour
{
    /*
    public NetInGameManager netInGame;
    public NetRoomManager roomManager;
    public bool joined = false;
    bool init = false;
    public void Update()
    {
        if(NetClient.ready)
        {
            if (!init)
            {
                if (!joined)
                {
                    roomManager.CmdCreateRoom();
                    StartCoroutine(Create());

                }
                else
                {
                    roomManager.CmdJoinRoom(1);
                    StartCoroutine(Join());
                }
                init = true;
            }
        }
    }
    private IEnumerator Create()
    {
        float time = 0;
        while(time < 3.0f)
        {
            time += Time.deltaTime;
            yield return null;
        }
        netInGame.CmdAnswerSceneLoaded();

    }
    private IEnumerator Join()
    {
        float time = 0;
        while (time < 3.0f)
        {
            time += Time.deltaTime;
            yield return null;
        }
        netInGame.CmdAnswerSceneLoaded();

    }
    */
    
}
