using System;
using Godot;
using Menu;

public partial class KeybindsManager : Node
{
    private static bool PopupShown = false;
    private static ulong LastVolumeChange = 0;
    private static Node LastVolumeChangeScene;

    public override void _Process(double delta)
    {
        if (LastVolumeChangeScene == SceneManager.Scene && PopupShown && Time.GetTicksMsec() - LastVolumeChange >= 1000)
        {
            PopupShown = false;

            Panel volumePopup = SceneManager.Scene.GetNode<Panel>("Volume");
            Label label = volumePopup.GetNode<Label>("Label");
            Tween tween = volumePopup.CreateTween();
            tween.TweenProperty(volumePopup, "modulate", Color.FromHtml("ffffff00"), 0.25).SetTrans(Tween.TransitionType.Quad);
            tween.Parallel().TweenProperty(label, "anchor_bottom", 1, 0.35).SetTrans(Tween.TransitionType.Quad);
            tween.Play();
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey eventKey && eventKey.Pressed)
		{
            if (eventKey.Keycode == Key.F11 || (eventKey.AltPressed && (eventKey.Keycode == Key.Enter || eventKey.Keycode == Key.KpEnter)))
            {
                bool value = DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Windowed;

                Phoenyx.Settings.Fullscreen = value;
                DisplayServer.WindowSetMode(value ? DisplayServer.WindowMode.ExclusiveFullscreen : DisplayServer.WindowMode.Windowed);
                MainMenu.UpdateSettings();
            }
        }
    }

    public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton eventMouseButton && eventMouseButton.Pressed)
		{
			if (eventMouseButton.CtrlPressed && (eventMouseButton.ButtonIndex == MouseButton.WheelUp || eventMouseButton.ButtonIndex == MouseButton.WheelDown))
			{
				switch (eventMouseButton.ButtonIndex)
				{
					case MouseButton.WheelUp:
						Phoenyx.Settings.VolumeMaster = Math.Min(100, Phoenyx.Settings.VolumeMaster + 5f);
						break;
					case MouseButton.WheelDown:
						Phoenyx.Settings.VolumeMaster = Math.Max(0, Phoenyx.Settings.VolumeMaster - 5f);
						break;
				}

                Panel volumePopup = SceneManager.Scene.GetNode<Panel>("Volume");
                Label label = volumePopup.GetNode<Label>("Label");
                label.Text = Phoenyx.Settings.VolumeMaster.ToString();
                Tween tween = volumePopup.CreateTween();
                tween.TweenProperty(volumePopup, "modulate", Color.FromHtml("ffffffff"), 0.25).SetTrans(Tween.TransitionType.Quad);
                tween.Parallel().TweenProperty(volumePopup.GetNode<ColorRect>("Main"), "anchor_right", Phoenyx.Settings.VolumeMaster / 100, 0.15).SetTrans(Tween.TransitionType.Quad);
                tween.Parallel().TweenProperty(label, "anchor_bottom", 0, 0.15).SetTrans(Tween.TransitionType.Quad);
                tween.Play();

                PopupShown = true;
                LastVolumeChange = Time.GetTicksMsec();
                LastVolumeChangeScene = SceneManager.Scene;

                switch (SceneManager.Scene.Name)
                {
                    case "SceneMenu":
                        MainMenu.UpdateVolume();
                        break;
                    case "SceneGame":
                        Runner.UpdateVolume();
                        break;
                    default:
                        break;
                }
			}
		}
	}
}