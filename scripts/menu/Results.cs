using Godot;
using Phoenyx;
using System;

public partial class Results : Control
{
	private static Control Control;

	private static TextureRect Cursor;
	private static Panel TopBar;
	private static Panel Footer;

	public static Vector2 MousePosition = Vector2.Zero;

	public override void _Ready()
	{
		Control = this;
		
		Cursor = GetNode<TextureRect>("Cursor");
		TopBar = GetNode<Panel>("TopBar");
		Footer = GetNode<Panel>("Footer");

		Input.MouseMode = Input.MouseModeEnum.Visible;
		DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Mailbox);

		Cursor.Texture = Phoenyx.Skin.CursorImage;
		Cursor.Size = new Vector2(32 * (float)Settings.CursorScale, 32 * (float)Settings.CursorScale);

		GetNode<Label>("Title").Text = Runner.CurrentAttempt.Map.PrettyTitle;
		GetNode<Label>("Difficulty").Text = Runner.CurrentAttempt.Map.DifficultyName;
		GetNode<Label>("Mappers").Text = $"By: {Runner.CurrentAttempt.Map.PrettyMappers}";

		Footer.GetNode<Button>("Back").Pressed += () => {
			Stop();
		};

		Footer.GetNode<Button>("Play").Pressed += () => {
			Replay();
		};
	}

	public override void _Process(double delta)
	{
		Cursor.Position = MousePosition - new Vector2(Cursor.Size.X / 2, Cursor.Size.Y / 2);
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventKey eventKey && eventKey.Pressed)
		{
			switch (eventKey.Keycode)
			{
				case Key.Escape:
					Stop();
					break;
				case Key.Space:
					Replay();
					break;
			}
		}
		else if (@event is InputEventMouseMotion eventMouseMotion)
		{
			MousePosition = eventMouseMotion.Position;
		}
		else if (@event is InputEventMouseButton eventMouseButton && eventMouseButton.Pressed)
		{
			switch (eventMouseButton.ButtonIndex)
			{
				case MouseButton.Xbutton1:
					Stop();
					break;
			}
		}
	}

	public static void Replay()
	{
		SceneManager.Load("res://scenes/game.tscn");
		Runner.Play(MapParser.Decode(Runner.CurrentAttempt.Map.FilePath), Runner.CurrentAttempt.Speed, Runner.CurrentAttempt.RawMods);
	}

	public static void Stop()
	{
		SceneManager.Load("res://scenes/main_menu.tscn");
	}
}
