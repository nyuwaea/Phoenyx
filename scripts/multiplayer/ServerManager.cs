using System;
using Godot;

public partial class ServerManager : Node
{
	public static Node Node;
	public static string IP;
	public static int Port;
	public static ENetMultiplayerPeer Peer;
	public static MultiplayerApi API;
	public static bool Instanced;

	public override void _Ready()
	{
		Node = this;
		IP = "127.0.0.1";
		Port = 44220;
		Peer = new();
		API = Multiplayer;
		Instanced = false;

		Multiplayer.PeerConnected += ClientConnected;
		Multiplayer.PeerDisconnected += ClientDisconnected;
	}

	public override string ToString() => $"IPv4: {IP}, Port: {Port}";

	public static void CreateServer(string ip, string port)
	{
		if (Instanced)
		{
			GD.Print($"Previous server detected, updating networking information\n{IP} => {ip}; {Port} => {port}");
			Peer.Close();
		}

		Instanced = true;
		IP = Networking.ValidateIP(ip);
		Port = Networking.ValidatePort(port);

        try
        {
            Error err = Peer.CreateServer(Port);

			if (err != Error.Ok)
			{
				throw Logger.Error(err.ToString());
			}

			API.MultiplayerPeer = Peer;
        }
        catch (Exception exception)
        {
            throw Logger.Error($"Could not create server: {exception.Message}");
        }
	}

	private static void ClientConnected(long id)
	{
		GD.Print($"[SERVER] Client connected: {id}");

		ClientManager.Node.Rpc("ReceivePlayer", id.ToString());
	}

	private static void ClientDisconnected(long id)
	{
		GD.Print($"[SERVER] Client disconnected: {id}");
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer)]
	public void MissingMap(string fileName)
	{
		long id = API.GetRemoteSenderId();

		if (id == 1)
		{
			return;
		}

		FileAccess file = FileAccess.Open($"{Phoenyx.Constants.UserFolder}/maps/{fileName}", FileAccess.ModeFlags.Read);

		ClientManager.Node.RpcId(id, "ReceiveMapBuffer", fileName, file.GetBuffer((long)file.GetLength()));

		file.Close();
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void ValidateChat(string message)
    {
		long id = API.GetRemoteSenderId();

		if (message.Contains("nigger"))
		{
			ClientManager.Node.RpcId(id, "ReceiveChat", "[SERVER]: don't say that");
		}
		else
		{
			ClientManager.Node.Rpc("ReceiveChat", $"[{(id == 1 ? "Host" : id)}] {message}");
		}
    }

	[Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void ValidateScore(int score)
    {
		long id = API.GetRemoteSenderId();
		
		ClientManager.Node.Rpc("ReceiveScore", id.ToString(), score);
    }
}