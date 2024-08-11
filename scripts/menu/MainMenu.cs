using System;
using System.Collections.Generic;
using Godot;

namespace Menu;

public partial class MainMenu : Control
{
	public static Control Control;

	public static List<ColorRect> ActiveNotifications = new List<ColorRect>();

	public override void _Ready()
	{
		Control = this;

		Phoenix.Util.Setup();
		Input.MouseMode = Input.MouseModeEnum.Visible;
		DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
		DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Adaptive);

		Button button = GetNode<Button>("Button");
		FileDialog fileDialog = GetNode<FileDialog>("FileDialog");
		
		button.Pressed += () => fileDialog.Visible = true;
		fileDialog.FileSelected += (string path) => OpenFile(path);
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
    }

    private void OpenFile(string path)
	{
		Map map = MapParser.Parse(path);
		
		GetTree().ChangeSceneToFile("res://scenes/game.tscn");

		Game.Play(map, 1, new string[]{"NoFail"});
	}

	public static async void Notify(string message, int severity = 0)
	{
		Color color = new Color();

		switch (Math.Clamp(severity, 0, 2))
		{
			case 0:
				color = Color.Color8(0, 255, 0);
				break;
			case 1:
				color = Color.Color8(255, 255, 0);
				break;
			case 2:
				color = Color.Color8(255, 0, 0);
				break;
		}

		ColorRect notification = Phoenix.Util.Clone(Control.GetNode<ColorRect>("NotificationTemplate"));

		for (int i = 0; i < ActiveNotifications.Count; i++)
		{
			Tween upTween = ActiveNotifications[i].CreateTween();
			upTween.TweenProperty(ActiveNotifications[i], "position", ActiveNotifications[i].Position - new Vector2(0, ActiveNotifications[i].Size.Y * 1.2f), 0.5).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
		}

		ActiveNotifications.Add(notification);

		notification.Visible = true;
		notification.GetNode<Label>("Label").Text = message;
		notification.GetNode<ColorRect>("Severity").Color = color;

		Control.AddChild(notification);

		float offset = notification.Size.X * 1.1f;

		notification.Position += new Vector2(offset, 0);

		Tween tween = notification.CreateTween();
		tween.TweenProperty(notification, "position", notification.Position - new Vector2(offset, 0), 0.5).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);

		await Control.ToSignal(Control.GetTree().CreateTimer(3), "timeout");

		tween = notification.CreateTween();
		tween.TweenProperty(notification, "position", notification.Position + new Vector2(offset, 0), 0.5).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
		
		await Control.ToSignal(tween, "finished");

		ActiveNotifications.Remove(notification);
		notification.QueueFree();
	}

	public static void Quit()
	{
		Phoenix.Util.SaveSettings();
		Control.GetTree().Quit();
	}
}