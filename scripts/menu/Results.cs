using Godot;
using System;
using System.IO;

public partial class Results : Control
{
	private static TextureRect Cursor;
	private static Panel Footer;
	private static Panel Holder;
	private static TextureRect Cover;

	public static double LastFrame = 0;
	public static Vector2 MousePosition = Vector2.Zero;

	public override void _Ready()
	{
		Cursor = GetNode<TextureRect>("Cursor");
		Footer = GetNode<Panel>("Footer");
		Holder = GetNode<Panel>("Holder");
		Cover = GetNode<TextureRect>("Cover");

		Input.MouseMode = Input.MouseModeEnum.Hidden;
		DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Mailbox);

		Cursor.Texture = Phoenyx.Skin.CursorImage;
		Cursor.Size = new Vector2(32 * (float)Phoenyx.Settings.CursorScale, 32 * (float)Phoenyx.Settings.CursorScale);

		Holder.GetNode<Label>("Title").Text = (Runner.CurrentAttempt.IsReplay ? "[REPLAY] " : "") + Runner.CurrentAttempt.Map.PrettyTitle;
		Holder.GetNode<Label>("Difficulty").Text = Runner.CurrentAttempt.Map.DifficultyName;
		Holder.GetNode<Label>("Mappers").Text = $"by {Runner.CurrentAttempt.Map.PrettyMappers}";
		Holder.GetNode<Label>("Accuracy").Text = $"{Runner.CurrentAttempt.Accuracy.ToString().PadDecimals(2)}%";
		Holder.GetNode<Label>("Score").Text = $"{Lib.String.PadMagnitude(Runner.CurrentAttempt.Score.ToString())}";
		Holder.GetNode<Label>("Hits").Text = $"{Lib.String.PadMagnitude(Runner.CurrentAttempt.Hits.ToString())} / {Lib.String.PadMagnitude(Runner.CurrentAttempt.Sum.ToString())}";
		Holder.GetNode<Label>("Status").Text = Runner.CurrentAttempt.IsReplay ? Runner.CurrentAttempt.Replays[0].Status : Runner.CurrentAttempt.Alive ? (Runner.CurrentAttempt.Qualifies ? "PASSED" : "DISQUALIFIED") : "FAILED";
		Holder.GetNode<Label>("Speed").Text = $"{Runner.CurrentAttempt.Speed.ToString().PadDecimals(2)}x";

		if (Runner.CurrentAttempt.Map.CoverBuffer != null)
		{
			Godot.FileAccess file = Godot.FileAccess.Open($"{Phoenyx.Constants.UserFolder}/cache/cover.png", Godot.FileAccess.ModeFlags.Write);
			file.StoreBuffer(Runner.CurrentAttempt.Map.CoverBuffer);
			file.Close();

			Cover.Texture = ImageTexture.CreateFromImage(Image.LoadFromFile($"{Phoenyx.Constants.UserFolder}/cache/cover.png"));
			GetNode<TextureRect>("CoverBackground").Texture = Cover.Texture;
		}

		if (Runner.CurrentAttempt.Map.AudioBuffer != null)
		{
			if (!SoundManager.Song.Playing)
			{
				SoundManager.Song.Play();
			}
		}

		SoundManager.Song.PitchScale = (float)Runner.CurrentAttempt.Speed;
		
		if (!Runner.CurrentAttempt.Map.Ephemeral)
		{
			SoundManager.JukeboxIndex = SoundManager.JukeboxQueueInverse[Runner.CurrentAttempt.Map.ID];
		}

		Button replayButton = Footer.GetNode<Button>("Replay");

		Footer.GetNode<Button>("Back").Pressed += Stop;
		Footer.GetNode<Button>("Play").Pressed += Replay;
		replayButton.Visible = !Runner.CurrentAttempt.Map.Ephemeral;
		replayButton.Pressed += () => {
			string path;

			if (Runner.CurrentAttempt.IsReplay)
			{
				path = $"{Phoenyx.Constants.UserFolder}/replays/{Runner.CurrentAttempt.Replays[0].ID}.phxr";
			}
			else
			{
				path = Runner.CurrentAttempt.ReplayFile.GetPath();
			}
			
			if (File.Exists(path))
			{
				Replay replay = new(path);
				SoundManager.Song.Stop();
				SceneManager.Load("res://scenes/game.tscn");
				Runner.Play(MapParser.Decode(replay.MapFilePath), replay.Speed, replay.Modifiers, null, [replay]);
			}
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
			switch (eventKey.PhysicalKeycode)
			{
				case Key.Escape:
					Stop();
					break;
				case Key.Quoteleft:
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

	public static void UpdateVolume()
	{
		SoundManager.Song.VolumeDb = -80 + 70 * (float)Math.Pow(Phoenyx.Settings.VolumeMusic / 100, 0.1) * (float)Math.Pow(Phoenyx.Settings.VolumeMaster / 100, 0.1);
	}

	public static void Replay()
	{
		Map map = MapParser.Decode(Runner.CurrentAttempt.Map.FilePath);
		map.Ephemeral = Runner.CurrentAttempt.Map.Ephemeral;
		SoundManager.Song.Stop();
		SceneManager.Load("res://scenes/game.tscn");
		Runner.Play(map, Runner.CurrentAttempt.Speed, Runner.CurrentAttempt.RawMods);
	}

	public static void Stop()
	{
		SceneManager.Load("res://scenes/main_menu.tscn");
	}
}