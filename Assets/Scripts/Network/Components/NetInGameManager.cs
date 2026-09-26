using System;
using UnityEngine;

public class NetInGameManager : NetBehaviour
{
    #region Network
    public struct AnswerSceneLoadedData : INetTransportData<AnswerSceneLoadedData>
    {
        public uint roomID;
        public int playerIndex;
        public Action<NetWriter, AnswerSceneLoadedData> write =>
            (writer, para) =>
            {
                writer.Write<uint>(para.roomID);
                writer.Write<int>(para.playerIndex);
            };
        public Func<NetReader, AnswerSceneLoadedData> read =>
            (reader) =>
            {
                AnswerSceneLoadedData para = new();

                para.roomID = reader.Read<uint>();
                para.playerIndex = reader.Read<int>();
                return para;
            };
    }
    public struct GameHasStartedNetData : INetTransportData<GameHasStartedNetData>
    {
        public Action<NetWriter, GameHasStartedNetData> write => (writer, para) => { };

        public Func<NetReader, GameHasStartedNetData> read => (reader) => new GameHasStartedNetData();
    }
    public struct GameFinishedNetData : INetTransportData<GameFinishedNetData>
    {
        public int winnerIndex;
        public Action<NetWriter, GameFinishedNetData> write =>
            (writer, para) =>
            {
                writer.Write<int>(para.winnerIndex);
            };

        public Func<NetReader, GameFinishedNetData> read =>
            (reader) =>
            {
                GameFinishedNetData para = new GameFinishedNetData();
                para.winnerIndex = reader.Read<int>();
                return para;
            };
    }
    [SerializeField] private bool OnServer;
    static NetInGameManager()
    {
        Type type = typeof(NetInGameManager);

#if UNITY_EDITOR || !UNITY_SERVER
        RemoteProcedureCalls.RegisterServerCommand(type, "GameHasStarted", InvokeGameHasStarted, false);
        RemoteProcedureCalls.RegisterServerCommand(type, "GameFinished", InvokeGameFinished, false);

#endif

#if UNITY_EDITOR || UNITY_SERVER
        RemoteProcedureCalls.RegisterClientCommand(type, "AnswerSceneLoaded", InvokeAnswerSceneLoaded);
#endif

    }

    private void Awake()
    {
        identity.SetNetManagerID(NetManagerType.NetInGame, OnServer);
    }
    private void OnDestroy()
    {
        identity.RemoveNetManagerID(NetManagerType.NetInGame, OnServer);
    }
#if UNITY_EDITOR || !UNITY_SERVER
    public void CmdAnswerSceneLoaded()
    {
        AnswerSceneLoadedData para = new();
        para.roomID = GameRoomData.room.roomID;
        para.playerIndex = GameRoomData.room.selfIndex;

        string functionName = "AnswerSceneLoaded";
        Type type = typeof(NetInGameManager);

        SendCommandToServer<AnswerSceneLoadedData>(type, functionName, para, (int)Channels.Reliable);
    }
    //---------------------------------------------------------------------------------------------------
    private static void InvokeGameHasStarted(NetBehaviour behaviour, NetReader reader, NetConnectionToClient conn)
    {
        NetTransportData.Read(reader, out GameHasStartedNetData para);
        ((NetInGameManager)behaviour).GameHasStarted(para);
    }
    void GameHasStarted(GameHasStartedNetData data)
    {
        EventBus.InvokeGameHasStarted(data);
    }
    //---------------------------------------------------------------------------------------------------
    private static void InvokeGameFinished(NetBehaviour behaviour, NetReader reader, NetConnectionToClient conn)
    {
        NetTransportData.Read(reader, out GameFinishedNetData para);
        ((NetInGameManager)behaviour).GameFinished(para);
    }
    void GameFinished(GameFinishedNetData para)
    {
        EventBus.InvokeGameFinishedEvents(para);
    }
#endif

#if UNITY_EDITOR || UNITY_SERVER

    public void CmdGameHasStarted(ServerRoom room)
    {
        GameHasStartedNetData data = new();
        Type type = typeof(NetInGameManager);
        string functionName = "GameHasStarted";
        foreach (var targetConn in room.players)
        {
            if (targetConn == null)
                continue;
            SendCommandToTargetClient<GameHasStartedNetData>(targetConn, type, functionName, data, (int)Channels.Reliable);
        }
    }
    //---------------------------------------------------------------------------------------------------
    public void CmdGameFinish(int winnerIndex, NetConnectionToClient conn)
    {
        GameFinishedNetData para = new();
        para.winnerIndex = winnerIndex;

        string functionName = "GameFinished";
        Type type = typeof(NetInGameManager);

        SendCommandToTargetClient<GameFinishedNetData>(conn, type, functionName, para, (int)Channels.Reliable);
    }
    //---------------------------------------------------------------------------------------------------

    private static void InvokeAnswerSceneLoaded(NetBehaviour behaviour, NetReader reader, NetConnectionToClient conn)
    {
        NetTransportData.Read(reader, out AnswerSceneLoadedData para);
        ((NetInGameManager)behaviour).AnswerSceneLoaded(para);
    }
    void AnswerSceneLoaded(AnswerSceneLoadedData data)
    {
        RoomServerManager.Instance.PlayerLoadSceneReady(data.roomID, data.playerIndex);
    }
#endif
    #endregion
}
