using Godot;

public partial class Keybinds : Node
{
    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey eventKey && eventKey.Pressed)
		{
            if (eventKey.Keycode == Key.F11 || (eventKey.AltPressed && eventKey.Keycode == Key.Enter))
            {
                if (DisplayServer.WindowGetMode() == DisplayServer.WindowMode.ExclusiveFullscreen)
                {
                    DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
                }
                else
                {
                    DisplayServer.WindowSetMode(DisplayServer.WindowMode.ExclusiveFullscreen);
                }
            }
        }
    }
}