using System;
using System.Net;
using System.Net.Sockets;

public static partial class Utils 
{
    public static bool HandleHostName(string hostName, out IPAddress[] addresses)
    {
        try
        {
            addresses = Dns.GetHostAddresses(hostName);
            return addresses.Length >= 1;
        }
        catch(Exception e)
        {
            UnityEngine.Debug.Log(e);
            addresses = null;
            return false;
        }
    }
    public static void ResizeSocketBuffer(Socket socket, int recvBufferSize, int sendBufferSize)
    {
        try
        {
            socket.ReceiveBufferSize = recvBufferSize;
            socket.SendBufferSize = sendBufferSize;
        }
        catch (Exception e)
        {
            UnityEngine.Debug.Log($"An Exception Happen On Resizing Socket Buffer,{e}" );
        }
    }
}
