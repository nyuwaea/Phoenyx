using System;
using System.Collections.Generic;
using Godot;
using Phoenyx;

namespace Menu;

public partial class MainMenu : Control
{
	public static Control Control;

	public static List<ColorRect> ActiveNotifications = new List<ColorRect>();

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

		Button button = GetNode<Button>("Button");
		FileDialog fileDialog = GetNode<FileDialog>("FileDialog"); 
		
		button.Pressed += () => fileDialog.Visible = true;
		fileDialog.FileSelected += (string path) => {
			SceneManager.Load(GetTree(), "res://scenes/game.tscn");
			Game.Runner.Play(MapParser.Parse(path), 1, new string[]{"NoFail"});
		};
	}

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey eventKey && eventKey.Pressed)
		{
			if (eventKey.Keycode == Key.Escape)
			{
				Quit();
			}
		}
		else if (@event is InputEventMouseButton eventMouseButton)
		{
			//GD.Print(eventMouseButton.ButtonIndex);
		}
    }

	public static void Quit()
	{
		Util.SaveSettings();
		Control.GetTree().Quit();
	}
}