using System;
using Godot;
using Phoenyx;

namespace Menu;

public partial class MainMenu : Control
{
	private static Control Control;
	private static readonly PackedScene ChatMessage = GD.Load<PackedScene>("res://prefabs/chat_message.tscn");

	private static Panel multiplayer;
	private static LineEdit ipLine;
	private static LineEdit portLine;
	private static LineEdit chatLine;
	private static Button host;
	private static Button join;
	private static ScrollContainer chatScrollContainer;
	private static VBoxContainer chatHolder;

	public override void _Ready()
	{
		Control = this;
		SceneManager.Scene = this;
		
		Util.Setup();

		Util.DiscordRPC.Call("Set", "details", "In the menu");
		Util.DiscordRPC.Call("Set", "state", "");
		Util.DiscordRPC.Call("Set", "end_timestamp", 0);
		
		Input.MouseMode = Input.MouseModeEnum.Visible;
		DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Adaptive);

		// Map selection
		Button browse = GetNode<Button>("Browse");
		FileDialog fileDialog = GetNode<FileDialog>("FileDialog"); 
		
		browse.Pressed += () => fileDialog.Visible = true;
		fileDialog.FileSelected += (string path) => {
			if (Lobby.PlayerCount > 0)
			{
				Lobby.Map = MapParser.Parse(path);
				string[] split = path.Split("\\");
				
				ClientManager.Node.Rpc("ReceiveMapName", split[split.Length - 1]);

				Lobby.Ready("1");

				Lobby.AllReady += () => {
					ClientManager.Node.Rpc("ReceiveAllReady", split[split.Length - 1], Lobby.Speed, Lobby.Mods);
				};
			}
			else
			{
				Map map = MapParser.Parse(path);
				
				SceneManager.Load(Control.GetTree(), "res://scenes/game.tscn");
				Game.Play(map, Lobby.Speed, Lobby.Mods);
			}
		};

		// Multiplayer
		multiplayer = GetNode<Panel>("Multiplayer");
		ipLine = multiplayer.GetNode<LineEdit>("IP");
		portLine = multiplayer.GetNode<LineEdit>("Port");
		chatLine = multiplayer.GetNode<LineEdit>("ChatInput");
		host = multiplayer.GetNode<Button>("Host");
		join = multiplayer.GetNode<Button>("Join");
		chatScrollContainer = multiplayer.GetNode<ScrollContainer>("Chat");
		chatHolder = chatScrollContainer.GetNode<VBoxContainer>("Holder");

		host.Pressed += () => {
			try
			{
				ServerManager.CreateServer(ipLine.Text, portLine.Text);
			}
			catch (Exception exception)
			{
				ToastNotification.Notify($"{exception.Message}", 2);
				return;
			}

			host.Disabled = true;
			join.Disabled = true;
		};
		join.Pressed += () => {
			try
			{
				ClientManager.CreateClient(ipLine.Text, portLine.Text);
			}
			catch (Exception exception)
			{
				ToastNotification.Notify($"{exception.Message}", 2);
				return;
			}

			host.Disabled = true;
			join.Disabled = true;
			browse.Disabled = true;
		};
	}

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey eventKey && eventKey.Pressed)
		{
			switch (eventKey.Keycode)
			{
				case Key.Escape:
				{
					Quit();
					break;
				}
				case Key.Enter:
				{
					SendMessage();
					break;
				}
				case Key.KpEnter:
				{
					SendMessage();
					break;
				}
			}
		}
    }

	public static void Chat(string message)
	{
		Label chatMessage = ChatMessage.Instantiate<Label>();
		chatMessage.Text = message;
		chatHolder.AddChild(chatMessage);
		chatScrollContainer.ScrollVertical += 100;
	}

	private static void SendMessage()
	{
		if (chatLine.Text.Replace(" ", "") == "")
		{
			return;
		}
	
		ServerManager.Node.Rpc("ValidateChat", chatLine.Text);
		chatLine.Text = "";
	}

    private static void Quit()
	{
		Util.SaveSettings();
		Control.GetTree().Quit();
	}
}