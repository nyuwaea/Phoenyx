using System;
using System.IO;
using Godot;
using Menu;
using Phoenyx;

public partial class ClientManager : Node
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

		Multiplayer.ConnectedToServer += ConnectedToServer;
		Multiplayer.ConnectionFailed += ConnectionFailed;
	}

    public override string ToString() => $"IPv4: {IP}, Port: {Port}";

	public static void CreateClient(string ip, string port)
	{
		if (Instanced)
		{
			GD.Print($"Previous client detected, updating networking information\n{IP} => {ip}; {Port} => {port}");
            Peer.Close();
		}

		Instanced = true;
		IP = Networking.ValidateIP(ip);
		Port = Networking.ValidatePort(port);

        try
        {
            Error err = Peer.CreateClient(IP, Port);

			if (err != Error.Ok)
			{
				throw Logger.Error(err.ToString());
			}

            API.MultiplayerPeer = Peer;
        }
        catch (Exception exception)
        {
            throw Logger.Error($"Could not create client: {exception.Message}");
        }
	}

    private static void ConnectedToServer()
    {
        ServerManager.Node.Rpc("ValidateChat", "has entered the lobby");
    }

    private static void ConnectionFailed()
    {
        ToastNotification.Notify($"Connection failed, {IP}, {Port}", 2);
        Logger.Error($"Connection failed, {IP}, {Port}");
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void ReceivePlayer(string name)
    {
        Lobby.AddPlayer(new Player(name));
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferChannel = 1, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReceiveChat(string message)
    {
        MainMenu.Chat(message);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority)]
    public void ReceiveMapName(string fileName)
    {
        if (false && File.Exists($"{Constants.UserFolder}/maps/{fileName}"))
        {
            Node.Rpc("ReceiveClientReady", true);
        }
        else
        {
            ServerManager.Node.Rpc("MissingMap", fileName);
        }
    }

    [Rpc]
    public void ReceiveMapBuffer(string fileName, byte[] buffer)
    {
        Godot.FileAccess file = Godot.FileAccess.Open($"{Constants.UserFolder}/maps/{fileName}", Godot.FileAccess.ModeFlags.Write);

        file.StoreBuffer(buffer);
        file.Close();

        Node.Rpc("ReceiveClientReady", true);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    public void ReceiveClientReady(bool ready = true)
    {
        Lobby.Ready(API.GetRemoteSenderId().ToString(), ready);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    public void ReceiveAllReady(string fileName, float speed = 1, string[] mods = null)
    {
        SceneManager.Load("res://scenes/game.tscn");
		Runner.Play(MapParser.Decode($"{Constants.UserFolder}/maps/{fileName}"), speed, mods ?? Array.Empty<string>());
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    public void ReceiveScore(string name, int score)
    {
        Runner.UpdateScore(name, score);
    }
}