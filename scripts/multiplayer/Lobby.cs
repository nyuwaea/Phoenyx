using System;
using System.Collections.Generic;
using Godot;
using Menu;
using Phoenyx;

public partial class Lobby : Node
{
	[Export]
	public bool Server = false;
	[Export]
	public bool Connected = false;
	[Export]
	public string IP = "127.0.0.1";
	[Export]
	public int Port = 44220;
	[Export]
	public ENetMultiplayerPeer Peer = new ENetMultiplayerPeer();
	[Export]
	public string LocalName = "";
	//public List<Player> Players = new List<Player>();
	[Export]
	public string[] Players = Array.Empty<string>();

	[Signal]
	public delegate void CreateServerEventHandler(string ip, string port);
	[Signal]
	public delegate void CreateClientEventHandler(string ip, string port);
	[Signal]
	public delegate void ChatEventHandler(string ip, string port);
	[Signal]
	public delegate void StartEventHandler(byte[] buffer);
	[Signal]
	public delegate void ShareScoreEventHandler(string player, int score);
	
	public override void _Ready()
	{
		Multiplayer.PeerConnected += ServerConnected;
		Multiplayer.PeerDisconnected += ServerDisconnected;
		Multiplayer.ConnectedToServer += ClientConnected;
		Multiplayer.ConnectionFailed += ClientFailed;

		Connect("CreateServer", Callable.From((string ip, string port) => {
			CreatePeer(true, ip, port);
		}));
		Connect("CreateClient", Callable.From((string ip, string port) => {
			CreatePeer(false, ip, port);
		}));
		Connect("Chat", Callable.From((string message) => {
			Rpc("SendChat", message);
			SendChat(message);
		}));
		Connect("Start", Callable.From((byte[] buffer, string[] players) => {
			Rpc("LoadMap", buffer, players);
			LoadMap(buffer, players);
		}));
		Connect("ShareScore", Callable.From((string player, int score) => {
			Rpc("GetScore", player, score);
			GetScore(player, score);
		}));
	}

	private string ValidateIP(string ip)
	{
		return ip;  // nothing to do so far
	}

	private int ValidatePort(string port)
	{
		try
		{
			if (port != "")
			{
				return port.ToInt();
			}
		}
		catch
		{
			ToastNotification.Notify("Could not set port, defaulting to 44220", 2);
		}
		
		return Port;
	}

	private void CreatePeer(bool server, string ip, string port)
	{
		Connected = true;
		Server = server;
		IP = ValidateIP(ip);
		Port = ValidatePort(port);

		if (Server)
		{
			Error err = Peer.CreateServer(Port);

			if (err != Error.Ok)
			{
				ToastNotification.Notify("Could not create server", 2);
				throw Logger.Error("Could not create server");
			}

			LocalName = "Host";
			Players = new string[1]{LocalName};
		}
		else
		{
			Error err = Peer.CreateClient(IP, Port);

			if (err != Error.Ok)
			{
				ToastNotification.Notify("Could not create client", 2);
				throw Logger.Error("Could not create client");
			}

			LocalName = Multiplayer.GetUniqueId().ToString();
		}
		
		Peer.Host.Compress(ENetConnection.CompressionMode.RangeCoder);
		Multiplayer.MultiplayerPeer = Peer;
	}
	
	private void ServerConnected(long id)
	{
		if (!Server)
		{
			return;
		}
		
		//Players.Add(new Player(id.ToString()));

		string[] newPlayers = new string[Players.Length + 1];

		for (int i = 0; i < Players.Length; i++)
		{
			newPlayers[i] = Players[i];
		}

		newPlayers[Players.Length] = id.ToString();
		Players = newPlayers;
		
		Rpc("SendChat", $"[{id}] has entered the lobby");
		SendChat($"[{id}] has entered the lobby");
	}

	private void ServerDisconnected(long id)
	{
		
	}

	private void ClientConnected()
	{
		
	}

	private void ClientFailed()
	{
		GD.Print("fail");
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer)]
	private void SendChat(string message)
	{
		MainMenu.Chat(message);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer)]
	private void LoadMap(byte[] buffer, string[] players)
	{
		FileAccess file = FileAccess.Open($"{Constants.UserFolder}/cache/multimap.sspm", FileAccess.ModeFlags.WriteRead);
		file.StoreBuffer(buffer);
		file.Close();

		Map map = MapParser.Parse($"{Constants.UserFolder}/cache/multimap.sspm");

		SceneManager.Load(GetTree(), "res://scenes/game.tscn");
		Game.Play(map, 1, new string[]{"NoFail"}, players);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer)]
	private void GetScore(string player, int score)
	{
		Game.SetPlayerScore(player, score);
	}
	
	public override string ToString() => $"IPv4: {IP}, Port: {Port}";
}
