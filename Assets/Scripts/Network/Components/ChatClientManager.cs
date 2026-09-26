using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class ChatClientManager : NetManager
{
    /*
    [Header("Chat Manager")]
    public GameObject loginUI;

    public override void OnStartServer()
    {
        base.OnStartServer();
        loginUI.SetActive(false);
    }
    public override void OnStartClient()
    {
        chatBehaviour.gameObject.SetActive(true);
        base.OnStartClient();
        loginUI.SetActive(false);

        StartCoroutine(Register());

    }
    public void Start()
    {
        chatBehaviour.gameObject.SetActive(false);
    }
    private void Update()
    {
        AddPlayer();
        RequestHistoricalMessage(); 
    }
    private bool hasAddPlayer = false;
    private void AddPlayer()
    {
        if (hasAddPlayer == false && NetServer.active == false)
        {
            if (NetClient.ready)
            {
                CallServerToAddPlayer();
                hasAddPlayer = true;

            }
        }
    }
    private void RequestHistoricalMessage()
    {
        if (hasAddPlayer &&
            chatBehaviour.netID != 0 &&
            chatBehaviour.isRequestingHistoricalMessage == false &&
            chatBehaviour.receivedHistoricalMessage == false)
        {
            chatBehaviour.CmdRequestHistoricalMessage();
        }
    }
    #region Register

    IEnumerator Register()
    {
        Task task = AddressablesDriver.RegisterClientPrefab("9-Sliced", "prefabs");
        while(task.IsCompleted == false)
            yield return null;
    }

    #endregion
    */
}

