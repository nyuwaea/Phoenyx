using Godot;
using Phoenyx;

namespace Menu;

public partial class MainMenu : Control
{
	private static Control Control;
	private static readonly PackedScene Lobby = GD.Load<PackedScene>("res://prefabs/lobby.tscn");
	private static readonly PackedScene ChatMessage = GD.Load<PackedScene>("res://prefabs/chat_message.tscn");

	private static Panel multiplayer;
	private static LineEdit ipLine;
	private static LineEdit portLine;
	private static LineEdit chatLine;
	private static Button host;
	private static Button join;
	private static ScrollContainer chatScrollContainer;
	private static VBoxContainer chatHolder;
	private static Node lobby;

	public override void _Ready()
	{
		Control = this;
		SceneManager.Scene = this;
		
		Util.Setup();
		Util.DiscordRPC.Call("Set", "app_id", 1272588732834254878);
		Util.DiscordRPC.Call("Set", "large_image", "wizardry");
		
		Input.MouseMode = Input.MouseModeEnum.Visible;
		DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
		DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Adaptive);

		// Map selection
		Button browse = GetNode<Button>("Browse");
		FileDialog fileDialog = GetNode<FileDialog>("FileDialog"); 
		
		browse.Pressed += () => fileDialog.Visible = true;
		fileDialog.FileSelected += (string path) => {
			if ((bool)lobby.Get("Connected"))
			{
				FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
				byte[] buffer = file.GetBuffer((long)file.GetLength());

				lobby.EmitSignal("Start", buffer, lobby.Get("Players"));
			}
			else
			{
				SceneManager.Load(GetTree(), "res://scenes/game.tscn");
				Game.Play(MapParser.Parse(path), 1, new string[]{"NoFail"});
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
		lobby = Lobby.Instantiate();

		AddChild(lobby);

		host.Pressed += () => {
			host.Disabled = true;
			join.Disabled = true;

			lobby.EmitSignal("CreateServer", ipLine.Text, portLine.Text);
		};
		join.Pressed += () => {
			host.Disabled = true;
			join.Disabled = true;
			browse.Disabled = true;

			lobby.EmitSignal("CreateClient", ipLine.Text, portLine.Text);
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
					if (!(bool)lobby.Get("Connected") || chatLine.Text.Replace(" ", "") == "")
					{
						return;
					}

					lobby.EmitSignal("Chat", $"[{lobby.Get("LocalName")}] {chatLine.Text}");
					chatLine.Text = "";
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

    private static void Quit()
	{
		Util.SaveSettings();
		Control.GetTree().Quit();
	}
}