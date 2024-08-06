using Godot;

public partial class MainMenu : Control
{
	public override void _Ready()
	{
		Button button = GetNode<Button>("Button");
		FileDialog fileDialog = GetNode<FileDialog>("FileDialog");
		
		button.Pressed += () => fileDialog.Visible = true;
		fileDialog.FileSelected += (string path) => OpenFile(path);
	}

	public void OpenFile(string path)
	{
		var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);

		GetTree().ChangeSceneToFile("res://scenes/game.tscn");

		Game.Run(MapParser.SSMapV1(file.GetLine()));
	}
}
