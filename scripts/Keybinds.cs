using Godot;
using Menu;
using Phoenyx;

public partial class Keybinds : Node
{
    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey eventKey && eventKey.Pressed)
		{
            if (eventKey.Keycode == Key.F11 || (eventKey.AltPressed && (eventKey.Keycode == Key.Enter || eventKey.Keycode == Key.KpEnter)))
            {
                bool value = DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Windowed;

                Settings.Fullscreen = value;
                DisplayServer.WindowSetMode(value ? DisplayServer.WindowMode.ExclusiveFullscreen : DisplayServer.WindowMode.Windowed);
                MainMenu.UpdateSettings();
            }
        }
    }
}