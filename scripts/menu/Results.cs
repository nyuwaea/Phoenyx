using Godot;
using Phoenyx;
using System;

public partial class Results : Control
{
	private static Control Control;

	private static TextureRect Cursor;
	private static Panel TopBar;

	public static Vector2 MousePosition = Vector2.Zero;

	public override void _Ready()
	{
		Control = this;
		
		Cursor = GetNode<TextureRect>("Cursor");
		TopBar = GetNode<Panel>("TopBar");

		Input.MouseMode = Input.MouseModeEnum.Visible;
		DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Mailbox);

		Cursor.Texture = Phoenyx.Skin.CursorImage;
		Cursor.Size = new Vector2(32 * (float)Settings.CursorScale, 32 * (float)Settings.CursorScale);
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
			}
		}
		else if (@event is InputEventMouseMotion eventMouseMotion)
		{
			MousePosition = eventMouseMotion.Position;
		}
	}

	public static void Stop()
	{
		SceneManager.Load("res://scenes/main_menu.tscn");
	}
}
