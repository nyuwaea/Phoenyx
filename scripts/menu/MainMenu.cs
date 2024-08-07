using Godot;

public partial class MainMenu : Control
{
	public override void _Ready()
	{
		Phoenix.Util.SetupUserFolder();

		Input.MouseMode = Input.MouseModeEnum.Visible;

		Button button = GetNode<Button>("Button");
		FileDialog fileDialog = GetNode<FileDialog>("FileDialog");
		
		button.Pressed += () => fileDialog.Visible = true;
		fileDialog.FileSelected += (string path) => OpenFile(path);
	}

	public void OpenFile(string path)
	{
		var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);

		Game.Play(MapParser.SSMapV1(file.GetLine()));

		GetTree().ChangeSceneToFile("res://scenes/game.tscn");
	}
}
