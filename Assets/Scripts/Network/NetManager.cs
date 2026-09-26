using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Principal;
using Unity.VisualScripting;
using UnityEngine;

public class NetManager : MonoBehaviour
{
    public enum NetManagerMode
    {
        Offline, 
        Server, 
        Client
    }
    private static NetManager instance;
    public static NetManager Instance => instance;

    public NetManagerMode mode { get; private set; }
    [Header("Common")]
    [SerializeField] private Transport transport;
    [SerializeField] private bool dontDestroyOnLoad;
    [SerializeField] public int sendRate = 60;
    [SerializeField] public int unreliableBaselineRate = 1;
    [SerializeField] public bool runInBackground;
    [SerializeField] public string networkAddress = "localhost";
    [SerializeField] private GameObject playerPrefab;

    [Header("Server")]
    [SerializeField] public bool disconnectInactiveConnections = true;
    [SerializeField] public int disconnectInactiveTimeout = 180;
    [SerializeField] public bool exceptionsDisconnect = true;
    [SerializeField] public int maxConnections = 50;

    #region common
    protected virtual void Awake()
    {
        Init();
        ApplyConfiguration();

    }
    private void ApplyConfiguration()
    {
        NetServer.sendRate = sendRate;
        NetServer.unreliableBaselineRate = unreliableBaselineRate;
        //NetServer.unreliableRedundancy = unreliableRedundancy;
    }
    private void Init()
    {
        if (instance != null && instance == this)
            return;

        if (dontDestroyOnLoad)
        {
            if (instance != null)
            {
                Debug.Log("Multiple NetworkManagers detected in the scene. Only one NetworkManager can exist at a time." +
                    "it is ok, Multiple NetworkManagers could exist during Unit Test");
                Destroy(gameObject);
                return;
            }

            instance = this;
            if (Application.isPlaying)
            {
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
            }
        }
        else
        {
            instance = this;
        }

        if (transport == null)
        {
            if (TryGetComponent(out Transport newTransport))
            {
                transport = newTransport;
            }
            else
            {
                Debug.LogError("Please add Transport on Network Manager Before Start Game.");
                return;
            }
        }

        Transport.instance = transport;
    }

    #endregion

    #region Server Manager
    public void StartServer()
    {
        if (NetServer.active)
        {
            Debug.LogWarning("Server already started.");
            return;
        }
        mode = NetManagerMode.Server;
        SetupServer();
        OnStartServer();

        NetServer.SpawnObjectsInNowScene();
    }
    private void SetupServer()
    {
        Init();

        NetServer.disconnectInactiveConnections = disconnectInactiveConnections;
        NetServer.disconnectInactiveTimeout = disconnectInactiveTimeout;
        NetServer.exceptionsDisconnect = exceptionsDisconnect;

        if (runInBackground)
            Application.runInBackground = true;

        //Set Server FrameRate 
        Application.targetFrameRate = sendRate;

        NetServer.StartListen(maxConnections);

        RegisterServerMessages();
    }
    private void RegisterServerMessages()
    {
        NetServer.OnConnectedEvent = OnServerConnectInternal;
        NetServer.OnConnectionReadyEvent = OnServerConnectionReady;
        NetServer.OnDisconnectedEvent = OnServerDisconnectInternal;
        NetServer.OnErrorEvent = OnServerError;
        NetServer.OnTransportExceptionEvent = OnServerTransportException;

        NetServer.RegisterHandler<AddPlayerMessage>(OnServerAddPlayerMessage);

    }

    #endregion

    #region Client Manager
    public void CallServerToAddPlayer()
        => NetClient.AddPlayer();
    public void StartClient()
    {
        if (NetClient.active)
        {
            Debug.LogWarning("Client already started.");
            return;
        }

        if (string.IsNullOrWhiteSpace(networkAddress))
        {
            Debug.LogError("Must set the Network Address field in the manager");
            return;
        }
        mode = NetManagerMode.Client;
        SetupClient();

        NetClient.Connect(networkAddress);
        OnStartClient();
    }
    private void SetupClient()
    {
        Init();

        NetClient.exceptionsDisconnect = exceptionsDisconnect;

        if (runInBackground)
            Application.runInBackground = true;

        RegisterClientMessages();
    }

    private void DisconnectClient()
    {
        if (mode == NetManagerMode.Offline)
            return;

        NetClient.Disconnect();
    }

    public virtual void RegisterClientMessages()
    {
        NetClient.OnConnectedEvent = OnClientConnectInternal;
        NetClient.OnDisconnectedEvent = OnClientDisconnectInternal;
        NetClient.OnErrorEvent = OnClientError;
        NetClient.OnTransportExceptionEvent = OnClientTransportException;

        if (playerPrefab != null)
            NetClient.RegisterPrefab(playerPrefab);
    }

    #endregion

    #region Callback
    private void OnServerConnectInternal(NetConnectionToClient conn)
    {
        /*
        if (networkSceneName != "" && networkSceneName != offlineScene)
        {
            SceneMessage msg = new SceneMessage()
            {
                sceneName = networkSceneName
            };
            conn.Send(msg);
        }
        */

        OnServerConnect(conn);
    }
    private void OnServerDisconnectInternal(NetConnectionToClient conn)
    {
        //NetServer.DestroyClientForConnection(conn);
        OnServerDisconnect(conn);
    }
    private void OnServerAddPlayerMessage(NetConnectionToClient conn, AddPlayerMessage msg)
    {
        if (conn.playerIdentity != null)
        {
            Debug.LogError("There is already a player identity for this connection.");
            return;
        }
        OnServerAddPlayer(conn);
    }
    public virtual void OnServerConnect(NetConnectionToClient conn) { }
    public virtual void OnServerConnectionReady(NetConnectionToClient conn) { }

    public virtual void OnServerAddPlayer(NetConnectionToClient conn)
    {
        if(playerPrefab == null)
        {
            Debug.LogWarning("PlayerPrefab is null, default AddPlayer behaviour will be ignored.");
            return;
        }
        return;
        /*
        GameObject player = Instantiate(playerPrefab);
        player.name = $"Player_{conn.connectionID}";

        NetServer.AddPlayerToConnection(conn, player);
        */
    }

    public virtual void OnServerDisconnect(NetConnectionToClient conn) { }
    public virtual void OnStartServer() { }
    public virtual void OnStopServer() { }
    public virtual void OnServerError(NetConnectionToClient conn, TransportError error, string reason) { }
    public virtual void OnServerTransportException(NetConnectionToClient conn, Exception exception) { }


    // client -----------------------
    private void OnClientConnectInternal()
    {
        OnClientConnect();
    }
    private void OnClientDisconnectInternal()
    {
        mode = NetManagerMode.Offline;
        NetClient.ShutDown();
        OnClientDisconnect();
    }
    public virtual void OnClientConnect() { }
    public virtual void OnClientDisconnect() { }
    public virtual void OnStartClient() { }
    public virtual void OnStopClient() { }
    public virtual void OnClientError(TransportError error, string reason) { }
    public virtual void OnClientTransportException(Exception exception) { }
    #endregion

}
