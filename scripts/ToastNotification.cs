using System;
using Godot;

public class ToastNotification
{
    private static readonly PackedScene template = GD.Load<PackedScene>("res://prefabs/notification.tscn");

    public static async void Notify(string message, int severity = 0)
    {
        ColorRect notification = template.Instantiate<ColorRect>();
        SceneManager.Scene.GetNode("NotificationHolder").AddChild(notification);
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

        notification.GetNode<Label>("Label").Text = message;
		notification.GetNode<ColorRect>("Severity").Color = color;
        notification.Visible = true;

        await SceneManager.Scene.ToSignal(SceneManager.Scene.GetTree().CreateTimer(3), "timeout");

        notification.QueueFree();
    }
}