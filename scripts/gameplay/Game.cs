using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Godot;
using Phoenyx;

public partial class Game : Node3D
{
	private static Node3D Node3D;
	private static readonly PackedScene PlayerScore = GD.Load<PackedScene>("res://prefabs/player_score.tscn");
	private static readonly PackedScene MissFeedbackIcon = GD.Load<PackedScene>("res://prefabs/miss_icon.tscn");

	private static Label FPSCounter;
	private static Camera3D Camera;
	private static Label3D TitleLabel;
	private static Label3D ComboLabel;
	private static Label3D SpeedLabel;
	private static Label3D SkipLabel;
	private static Label3D ProgressLabel;
	private static MeshInstance3D Cursor;
	private static MeshInstance3D Grid;
	private static MultiMeshInstance3D NotesMultimesh;
	private static TextureRect Health;
	private static TextureRect ProgressBar;
	private static SubViewport PanelLeft;
	private static SubViewport PanelRight;
	private static Label AccuracyLabel;
	private static Label HitsLabel;
	private static Label MissesLabel;
	private static Label SumLabel;
	private static AudioStreamPlayer Audio;
	private static AudioStreamPlayer HitSound;
	private static Tween HitTween;
	private static Tween MissTween;
	private static bool StopQueued = false;
	private static int MissIcons = 0;

	private double LastFrame = Time.GetTicksUsec(); 	// delta arg unreliable..
	private double LastSecond = Time.GetTicksUsec();	// better framerate calculation
	private int FrameCount = 0;
	private float SkipLabelAlpha = 0;
	private float TargetSkipLabelAlpha = 0;

	public static bool Playing = false;
	public static int ToProcess = 0;
	public static List<Note> ProcessNotes;
	public static Attempt CurrentAttempt;
	public static double MapLength;

	public struct Attempt
	{
		public double Progress = 0;	// ms
		public Map Map = new Map();
		public double Speed = 1;
		public Dictionary<string, bool> Mods;
		public string[] Players = Array.Empty<string>();
		public int Hits = 0;
		public int Misses = 0;
		public int Combo = 0;
		public int PassedNotes = 0;
		public double Accuracy = 100;
		public double Health = 100;
		public double HealthStep = 15;
		public Vector2 CursorPosition = Vector2.Zero;
		public bool Skippable = false;

		public Attempt(Map map, double speed, string[] mods, string[] players = null, bool replay = false)
		{
			Map = map;
			Speed = speed;
			Players = players ?? Array.Empty<string>();
			Progress = -1000;

			Mods = new(){
				["NoFail"] = mods.Contains("NoFail")
			};
		}

		public void Hit(int index)
		{
			Hits++;
			Combo++;
			HealthStep = Math.Max(HealthStep / 1.45f, 15);
			Health = Math.Min(100, Health + HealthStep / 1.75f);
			Map.Notes[index].Hit = true;

			HitsLabel.LabelSettings.FontColor = Color.FromHtml("#ffffffff");

			if (HitTween != null)
			{
				HitTween.Kill();
			}

			HitTween = HitsLabel.CreateTween();
			HitTween.TweenProperty(HitsLabel.LabelSettings, "font_color", Color.FromHtml("#ffffffa0"), 1);
			HitTween.Play();

			if (Lobby.PlayerCount > 1)
			{
				ServerManager.Node.Rpc("ValidateScore", Hits);
			}
		}

		public void Miss(int index)
		{
			Misses++;
			Combo = 0;
			Health = Math.Max(0, Health - HealthStep);
			HealthStep = Math.Min(HealthStep * 1.2f, 100);

			if (Health <= 0 && !CurrentAttempt.Mods["NoFail"])
			{
				QueueStop();
			}

			MissesLabel.LabelSettings.FontColor = Color.FromHtml("#ffffffff");

			if (MissTween != null)
			{
				MissTween.Kill();
			}

			MissTween = MissesLabel.CreateTween();
			MissTween.TweenProperty(MissesLabel.LabelSettings, "font_color", Color.FromHtml("#ffffffa0"), 1);
			MissTween.Play();

			if (MissIcons >= 64)
			{
				return;
			}

			MissIcons++;

			Sprite3D icon = MissFeedbackIcon.Instantiate<Sprite3D>();
			Node3D.AddChild(icon);
			icon.GlobalPosition = new Vector3(Map.Notes[index].X, -1.4f, 0);

			Tween tween = icon.CreateTween();
			tween.TweenProperty(icon, "transparency", 1, 0.25f);
			tween.Parallel().TweenProperty(icon, "position", icon.Position + Vector3.Up / 4f, 0.25f).SetTrans(Tween.TransitionType.Quint).SetEase(Tween.EaseType.Out);
			tween.TweenCallback(Callable.From(() => {
				MissIcons--;
				icon.QueueFree();
			}));
			tween.Play();
		}
	}
	
	public override void _Ready()
	{
		Node3D = this;

		FPSCounter = GetNode<Label>("FPSCounter");
		Camera = GetNode<Camera3D>("Camera3D");
		TitleLabel = GetNode<Label3D>("Title");
		ComboLabel = GetNode<Label3D>("Combo");
		SpeedLabel = GetNode<Label3D>("Speed");
		SkipLabel = GetNode<Label3D>("Skip");
		ProgressLabel = GetNode<Label3D>("Progress");
		Cursor = GetNode<MeshInstance3D>("Cursor");
		Grid = GetNode<MeshInstance3D>("Grid");
		NotesMultimesh = GetNode<MultiMeshInstance3D>("Notes");
		Health = GetNode("Health").GetNode("SubViewport").GetNode<TextureRect>("Main");
		ProgressBar = GetNode("ProgressBar").GetNode("SubViewport").GetNode<TextureRect>("Main");
		PanelLeft = GetNode("PanelLeft").GetNode<SubViewport>("SubViewport");
		PanelRight = GetNode("PanelRight").GetNode<SubViewport>("SubViewport");
		AccuracyLabel = PanelRight.GetNode<Label>("Accuracy");
		HitsLabel = PanelRight.GetNode<Label>("Hits");
		MissesLabel = PanelRight.GetNode<Label>("Misses");
		SumLabel = PanelRight.GetNode<Label>("Sum");
		Audio = Node3D.GetNode<AudioStreamPlayer>("SongPlayer");
		HitSound = Node3D.GetNode<AudioStreamPlayer>("HitSoundPlayer");

		//foreach (KeyValuePair<string, Player> entry in Lobby.Players)
		//{
		//	ColorRect playerScore = PlayerScore.Instantiate<ColorRect>();
		//	playerScore.GetNode<Label>("Name").Text = entry.Key;
		//	playerScore.Name = entry.Key;
		//	Leaderboard.GetNode("SubViewport").GetNode("Players").AddChild(playerScore);
		//}

		Camera.Fov = (float)Settings.FoV;
		TitleLabel.Text = CurrentAttempt.Map.PrettyTitle;
		HitsLabel.LabelSettings.FontColor = Color.FromHtml("#ffffffa0");
		MissesLabel.LabelSettings.FontColor = Color.FromHtml("#ffffffa0");

		Util.DiscordRPC.Call("Set", "details", "Playing a map");
		Util.DiscordRPC.Call("Set", "state", CurrentAttempt.Map.PrettyTitle);
		Util.DiscordRPC.Call("Set", "end_timestamp", Time.GetUnixTimeFromSystem() + CurrentAttempt.Map.Length / 1000 / CurrentAttempt.Speed);

		Input.MouseMode = Input.MouseModeEnum.Captured;
		Input.UseAccumulatedInput = false;
		DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);

		Cursor.Mesh.Set("size", new Vector2((float)(Constants.CursorSize * Settings.CursorScale), (float)(Constants.CursorSize * Settings.CursorScale)));

		try
		{
			(Cursor.GetActiveMaterial(0) as StandardMaterial3D).AlbedoTexture = ImageTexture.CreateFromImage(Image.LoadFromFile($"{Constants.UserFolder}/skins/{Settings.Skin}/cursor.png"));
			(Grid.GetActiveMaterial(0) as StandardMaterial3D).AlbedoTexture = ImageTexture.CreateFromImage(Image.LoadFromFile($"{Constants.UserFolder}/skins/{Settings.Skin}/grid.png"));
			Health.Texture = ImageTexture.CreateFromImage(Image.LoadFromFile($"{Constants.UserFolder}/skins/{Settings.Skin}/health.png"));
			Health.GetParent().GetNode<TextureRect>("Background").Texture = ImageTexture.CreateFromImage(Image.LoadFromFile($"{Constants.UserFolder}/skins/{Settings.Skin}/health_background.png"));
			ProgressBar.Texture = ImageTexture.CreateFromImage(Image.LoadFromFile($"{Constants.UserFolder}/skins/{Settings.Skin}/progress.png"));
			ProgressBar.GetParent().GetNode<TextureRect>("Background").Texture = ImageTexture.CreateFromImage(Image.LoadFromFile($"{Constants.UserFolder}/skins/{Settings.Skin}/progress_background.png"));
			
			if (File.Exists($"{Constants.UserFolder}/skins/{Settings.Skin}/note.obj"))
			{
				NotesMultimesh.Multimesh.Mesh = (ArrayMesh)Util.OBJParser.Call("load_obj", $"{Constants.UserFolder}/skins/{Settings.Skin}/note.obj");
			}
			else
			{
				NotesMultimesh.Multimesh.Mesh = GD.Load<ArrayMesh>($"res://skin/note.obj");
			}
		}
		catch (Exception exception)
		{
			ToastNotification.Notify("Could not load skin", 2);
			throw Logger.Error($"Could not load skin; {exception.Message}");
		}

		if (CurrentAttempt.Map.AudioBuffer != null)
		{
			Audio.Stream = LoadAudioStream(CurrentAttempt.Map.AudioBuffer);
			Audio.PitchScale = (float)CurrentAttempt.Speed;
			MapLength = (float)Audio.Stream.GetLength() * 1000;
		}
		else
		{
			MapLength = CurrentAttempt.Map.Length + 1000;
		}

		MapLength += Constants.HitWindow;

		if (File.Exists($"{Constants.UserFolder}/skins/{Settings.Skin}/hit.mp3"))
		{
			Godot.FileAccess hitSoundFile = Godot.FileAccess.Open($"{Constants.UserFolder}/skins/{Settings.Skin}/hit.mp3", Godot.FileAccess.ModeFlags.Read);
			HitSound.Stream = LoadAudioStream(hitSoundFile.GetBuffer((long)hitSoundFile.GetLength()));
			hitSoundFile.Close();
		}

		UpdateVolume();
	}

	public override void _Process(double delta)
	{
		double now = Time.GetTicksUsec();

		delta = (now - LastFrame) / 1000000;	// more reliable
		LastFrame = now;
		FrameCount++;
		SkipLabelAlpha = Mathf.Lerp(SkipLabelAlpha, TargetSkipLabelAlpha, (float)delta * 20);

		if (LastSecond + 1000000 <= now)
		{
			FPSCounter.Text = $"{FrameCount} FPS";
			FrameCount = 0;
			LastSecond += 1000000;
		}

		if (!Playing)
		{
			return;
		}
		
		CurrentAttempt.Progress += delta * 1000 * CurrentAttempt.Speed;
		CurrentAttempt.Skippable = false;

		if (CurrentAttempt.Map.AudioBuffer != null)
		{
			if (CurrentAttempt.Progress >= MapLength - Constants.HitWindow)
			{
				if (Audio.Playing)
				{
					Audio.Stop();
				}
			}
			else if (!Audio.Playing && CurrentAttempt.Progress >= 0)
			{
				Audio.Play();
			}
		}
		
		int nextNoteMillisecond = CurrentAttempt.PassedNotes >= CurrentAttempt.Map.Notes.Length ? (int)MapLength + Constants.BreakTime : CurrentAttempt.Map.Notes[CurrentAttempt.PassedNotes].Millisecond;
		
		if (nextNoteMillisecond - CurrentAttempt.Progress >= Constants.BreakTime * CurrentAttempt.Speed)
		{
			int lastNoteMillisecond = CurrentAttempt.PassedNotes > 0 ? CurrentAttempt.Map.Notes[CurrentAttempt.PassedNotes - 1].Millisecond : 0;
			int skipWindow = nextNoteMillisecond - Constants.BreakTime - lastNoteMillisecond;
			
			if (skipWindow >= 1000) // only allow skipping if i'm gonna allow it for at least 1 second
			{
				CurrentAttempt.Skippable = true;
			}
		}

		ToProcess = 0;
		ProcessNotes = new List<Note>();

		// note process check
		for (int i = CurrentAttempt.PassedNotes; i < CurrentAttempt.Map.Notes.Length; i++)
		{
			Note note = CurrentAttempt.Map.Notes[i];

			if (note.Millisecond + Constants.HitWindow * CurrentAttempt.Speed < CurrentAttempt.Progress)	// past hit window
			{
				if (i + 1 > CurrentAttempt.PassedNotes)
				{
					if (!note.Hit)
					{
						CurrentAttempt.Miss(note.Index);
					}

					CurrentAttempt.PassedNotes = i + 1;
				}

				continue;
			}
			else if (note.Millisecond > CurrentAttempt.Progress + Settings.ApproachTime * 1000 * CurrentAttempt.Speed)	// past approach distance
			{
				break;
			}
			else if (note.Hit)	// no point
			{
				continue;
			}
			
			ToProcess++;
			ProcessNotes.Add(note);
		}

		// hitreg check
		for (int i = 0; i < ToProcess; i++)
		{
			Note note = ProcessNotes[i];

			if (note.Hit || note.Millisecond - CurrentAttempt.Progress > 0)
			{
				continue;
			}

			if (CurrentAttempt.CursorPosition.X + Constants.HitBoxSize >= note.X - 0.5f && CurrentAttempt.CursorPosition.X - Constants.HitBoxSize <= note.X + 0.5f && CurrentAttempt.CursorPosition.Y + Constants.HitBoxSize >= note.Y - 0.5f && CurrentAttempt.CursorPosition.Y - Constants.HitBoxSize <= note.Y + 0.5f)
			{
				CurrentAttempt.Hit(note.Index);
				HitSound.Play();
			}
		}

		if (CurrentAttempt.Progress >= MapLength)
		{
			Stop();
			return;
		}

		if (CurrentAttempt.Skippable)
		{
			TargetSkipLabelAlpha = 32f / 255f;
			ProgressLabel.Modulate = Color.FromHtml("ffffff" + (64 + (int)(172 * (Math.Sin(now / 250000) / 2 + 0.5f))).ToString("X2"));
		}
		else
		{
			TargetSkipLabelAlpha = 0;
			ProgressLabel.Modulate = Color.FromHtml("ffffff40");
		}

		int sum = CurrentAttempt.Hits + CurrentAttempt.Misses;
		string accuracy = (Math.Floor((float)CurrentAttempt.Hits / sum * 10000) / 100).ToString().PadDecimals(2);

		HitsLabel.Text = $"{CurrentAttempt.Hits}";
		MissesLabel.Text = $"{CurrentAttempt.Misses}";
		SumLabel.Text = $"{sum}";
		AccuracyLabel.Text = $"{(CurrentAttempt.Hits + CurrentAttempt.Misses == 0 ? "100.00" : accuracy)}%";
		ComboLabel.Text = CurrentAttempt.Combo.ToString();
		SpeedLabel.Text = $"{CurrentAttempt.Speed.ToString().PadDecimals(2)}x";
		SpeedLabel.Modulate = Color.FromHtml($"#ffffff{(CurrentAttempt.Speed == 1 ? "00" : "20")}");
		ProgressLabel.Text = $"{FormatTime(Math.Max(0, CurrentAttempt.Progress) / 1000)} / {FormatTime(MapLength / 1000)}";
		Health.Size = new Vector2((float)CurrentAttempt.Health * 10.88f, 80);
		ProgressBar.Size = new Vector2(1088 * (float)(CurrentAttempt.Progress / MapLength), 80);
		SkipLabel.Modulate = Color.FromHtml("#ffffff" + Math.Min(255, (int)(255 * SkipLabelAlpha)).ToString("X2"));

		if (StopQueued)
		{
			StopQueued = false;
			Stop();
			return;
		}
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseMotion eventMouseMotion)
		{
			if (Settings.CameraLock)
			{
				CurrentAttempt.CursorPosition += new Vector2(1, -1) * eventMouseMotion.Relative / 100 * (float)Settings.Sensitivity;
				CurrentAttempt.CursorPosition = CurrentAttempt.CursorPosition.Clamp(-Constants.Bounds, Constants.Bounds);

				Cursor.GlobalPosition = new Vector3(CurrentAttempt.CursorPosition.X, CurrentAttempt.CursorPosition.Y, 0);
				Camera.GlobalPosition = new Vector3(0, 0, 3.75f) + new Vector3(CurrentAttempt.CursorPosition.X, CurrentAttempt.CursorPosition.Y, 0) * (float)Settings.Parallax;
				Camera.GlobalRotation = Vector3.Zero;
			}
			else
			{
				Camera.GlobalRotation += new Vector3(-eventMouseMotion.Relative.Y / 120 * (float)Settings.Sensitivity / (float)Math.PI, -eventMouseMotion.Relative.X / 120 * (float)Settings.Sensitivity / (float)Math.PI, 0);
				Camera.GlobalPosition = new Vector3(0, 0, 3.5f) + Camera.Basis.Z / 4;
				
				float hypotenuse = 3.5f / Camera.Basis.Z.Z;
				float distance = (float)Math.Sqrt(Math.Pow(hypotenuse, 2) - Math.Pow(3.5f, 2));
				
				CurrentAttempt.CursorPosition = (new Vector2(Camera.Basis.Z.X, Camera.Basis.Z.Y).Normalized() * -distance).Clamp(-Constants.Bounds, Constants.Bounds);
				Cursor.GlobalPosition = new Vector3(CurrentAttempt.CursorPosition.X, CurrentAttempt.CursorPosition.Y, 0);
			}
		}
		else if (@event is InputEventKey eventKey && eventKey.Pressed)
		{
			switch (eventKey.Keycode)
			{
				case Key.Escape:
					Stop();
					break;
				case Key.Space:
					if (Lobby.PlayerCount > 1)
					{
						break;
					}
					
					Skip();
					break;
				case Key.F:
					Settings.FadeOut = !Settings.FadeOut;
					break;
				case Key.P:
					Settings.Pushback = !Settings.Pushback;
					break;
				case Key.Equal:
					if (Lobby.PlayerCount > 1)
					{
						break;
					}
					
					CurrentAttempt.Speed = Math.Round((CurrentAttempt.Speed + 0.05) * 100) / 100;
					Audio.PitchScale = (float)CurrentAttempt.Speed;
					break;
				case Key.Minus:
					if (Lobby.PlayerCount > 1)
					{
						break;
					}
					
					CurrentAttempt.Speed = Math.Max(0.05, Math.Round((CurrentAttempt.Speed - 0.05) * 100) / 100);
					Audio.PitchScale = (float)CurrentAttempt.Speed;
					break;
			}
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton eventMouseButton && eventMouseButton.Pressed)
		{
			if (eventMouseButton.CtrlPressed)
			{
				switch (eventMouseButton.ButtonIndex)
				{
					case MouseButton.WheelUp:
						Settings.VolumeMaster = Math.Min(100, Settings.VolumeMaster + 2.5f);
						break;
					case MouseButton.WheelDown:
						Settings.VolumeMaster = Math.Max(0, Settings.VolumeMaster - 2.5f);
						break;
				}

				UpdateVolume();
			}
		}
	}

	public static void Play(Map map, double speed = 1, string[] mods = null, string[] players = null)
	{
		CurrentAttempt = new Attempt(map, speed, mods, players);
		Playing = true;
		ProcessNotes = null;
	}

	public static void Skip()
	{
		if (CurrentAttempt.Skippable)
		{
			if (CurrentAttempt.PassedNotes >= CurrentAttempt.Map.Notes.Length)
			{
				CurrentAttempt.Progress = Audio.Stream.GetLength() * 1000;
			}
			else
			{
				CurrentAttempt.Progress = CurrentAttempt.Map.Notes[CurrentAttempt.PassedNotes].Millisecond - Settings.ApproachTime * 1500 * CurrentAttempt.Speed; // turn AT to ms and multiply by 1.5x

				Util.DiscordRPC.Call("Set", "end_timestamp", Time.GetUnixTimeFromSystem() + (CurrentAttempt.Map.Length - CurrentAttempt.Progress) / 1000 / CurrentAttempt.Speed);
		
				if (CurrentAttempt.Map.AudioBuffer != null)
				{
					if (!Audio.Playing)
					{
						Audio.Play();
					}

					Audio.Seek((float)CurrentAttempt.Progress / 1000);
				}
			}
		}
	}

	public static void QueueStop()
	{
		StopQueued = true;
	}

	public static void Stop()
	{
		Playing = false;
		ProcessNotes = null;
		CurrentAttempt = new Attempt();

		SceneManager.Load( "res://scenes/main_menu.tscn");
	}

	private static void UpdateVolume()
	{
		Audio.VolumeDb = -80 + 70 * (float)Math.Pow(Settings.VolumeMusic / 100, 0.1) * (float)Math.Pow(Settings.VolumeMaster / 100, 0.5);
		HitSound.VolumeDb = -80 + 80 * (float)Math.Pow(Settings.VolumeSFX / 100, 0.1) * (float)Math.Pow(Settings.VolumeMaster / 100, 0.5);
	}

	public static void UpdateScore(string player, int score)
	{
		//ColorRect playerScore = Leaderboard.GetNode("SubViewport").GetNode("Players").GetNode<ColorRect>(player);
		//Label scoreLabel = playerScore.GetNode<Label>("Score");
		//playerScore.Position = new Vector2(playerScore.Position.X, score);
		//scoreLabel.Text = score.ToString();
	}

	private static string FormatTime(double seconds, bool padMinutes = false)
	{
		int minutes = (int)Math.Floor(seconds / 60);

		seconds -= minutes * 60;
		seconds = Math.Floor(seconds);

		return $"{(seconds < 0 ? "-" : "")}{(padMinutes ? minutes.ToString().PadZeros(2) : minutes)}:{seconds.ToString().PadZeros(2)}";
	}

	private static AudioStream LoadAudioStream(byte[] buffer)
	{
		AudioStream stream;

		if (Encoding.UTF8.GetString(buffer[0..4]) == "OggS")
		{
			stream = AudioStreamOggVorbis.LoadFromBuffer(buffer);
		}
		else
		{
			stream = new AudioStreamMP3(){Data = buffer};
		}

		return stream;
	}
}