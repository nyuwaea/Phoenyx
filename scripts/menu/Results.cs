using Godot;
using Phoenyx;
using System;
using System.Collections.Generic;

public partial class Results : Control
{
	private static Control Control;
	private static readonly PackedScene GraphMissTemplate = GD.Load<PackedScene>("res://prefabs/graph_miss.tscn");
	private static readonly PackedScene GraphHitTemplate = GD.Load<PackedScene>("res://prefabs/graph_hit.tscn");

	private static TextureRect Cursor;
	private static Panel TopBar;
	private static Panel Footer;
	private static Panel Holder;
	private static TextureRect Cover;
	private static ColorRect Graph;

	public static double LastFrame = 0;
	public static Vector2 MousePosition = Vector2.Zero;

	public override void _Ready()
	{
		Control = this;
		
		Cursor = GetNode<TextureRect>("Cursor");
		TopBar = GetNode<Panel>("TopBar");
		Footer = GetNode<Panel>("Footer");
		Holder = GetNode<Panel>("Holder");
		Cover = GetNode<TextureRect>("Cover");
		Graph = GetNode<ColorRect>("Graph");

		Input.MouseMode = Input.MouseModeEnum.Visible;
		DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Mailbox);

		Cursor.Texture = Phoenyx.Skin.CursorImage;
		Cursor.Size = new Vector2(32 * (float)Settings.CursorScale, 32 * (float)Settings.CursorScale);

		Holder.GetNode<Label>("Title").Text = Runner.CurrentAttempt.Map.PrettyTitle;
		Holder.GetNode<Label>("Difficulty").Text = Runner.CurrentAttempt.Map.DifficultyName;
		Holder.GetNode<Label>("Mappers").Text = $"by {Runner.CurrentAttempt.Map.PrettyMappers}";
		Holder.GetNode<Label>("Accuracy").Text = $"{Runner.CurrentAttempt.Accuracy.ToString().PadDecimals(2)}%";
		Holder.GetNode<Label>("Score").Text = $"{Lib.String.PadMagnitude(Runner.CurrentAttempt.Score.ToString())}";
		Holder.GetNode<Label>("Hits").Text = $"{Lib.String.PadMagnitude(Runner.CurrentAttempt.Hits.ToString())} / {Lib.String.PadMagnitude(Runner.CurrentAttempt.Sum.ToString())}";
		Holder.GetNode<Label>("Status").Text = Runner.CurrentAttempt.Alive ? (Runner.CurrentAttempt.Qualifies ? "PASSED" : "DISQUALIFIED") : "FAILED";

		if (Runner.CurrentAttempt.Map.CoverBuffer != null)
		{
			FileAccess file = FileAccess.Open($"{Constants.UserFolder}/cache/cover.png", FileAccess.ModeFlags.Write);
			file.StoreBuffer(Runner.CurrentAttempt.Map.CoverBuffer);
			file.Close();

			Cover.Texture = ImageTexture.CreateFromImage(Image.LoadFromFile($"{Constants.UserFolder}/cache/cover.png"));
			GetNode<TextureRect>("CoverBackground").Texture = Cover.Texture;
		}

		foreach (int miss in Runner.CurrentAttempt.MissesInfo)
		{
			ColorRect graphMiss = GraphMissTemplate.Instantiate<ColorRect>();
			Graph.AddChild(graphMiss);
			graphMiss.AnchorLeft = miss / (float)Runner.CurrentAttempt.Map.Length;
			graphMiss.AnchorRight = graphMiss.AnchorLeft;
		}
		
		foreach (Dictionary<string, int> hit in Runner.CurrentAttempt.HitsInfo)
		{
			ColorRect graphHit = GraphHitTemplate.Instantiate<ColorRect>();
			Graph.AddChild(graphHit);
			graphHit.AnchorLeft = hit["Time"] / (float)Runner.CurrentAttempt.Map.Length;
			graphHit.AnchorRight = graphHit.AnchorLeft;
			graphHit.AnchorTop = hit["Offset"] / 55f;
			graphHit.AnchorBottom = graphHit.AnchorTop;
		}

		Footer.GetNode<Button>("Back").Pressed += () => {
			Stop();
		};

		Footer.GetNode<Button>("Play").Pressed += () => {
			Replay();
		};
	}

	public override void _Process(double delta)
	{
		ulong now = Time.GetTicksUsec();
		delta = (now - LastFrame) / 1000000;
		LastFrame = now;

		Cursor.Position = MousePosition - new Vector2(Cursor.Size.X / 2, Cursor.Size.Y / 2);
		Holder.Position = Holder.Position.Lerp((Size / 2 - MousePosition) * (8 / Size.Y), Math.Min(1, (float)delta * 16));
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