using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using Phoenyx;

public partial class Runner : Node3D
{
	private static Node3D Node3D;
	private static readonly PackedScene PlayerScore = GD.Load<PackedScene>("res://prefabs/player_score.tscn");
	private static readonly PackedScene MissFeedback = GD.Load<PackedScene>("res://prefabs/miss_icon.tscn");

	private static Label FPSCounter;
	private static Camera3D Camera;
	private static Label3D TitleLabel;
	private static Label3D ComboLabel;
	private static Label3D SpeedLabel;
	private static Label3D SkipLabel;
	private static Label3D ProgressLabel;
	private static MeshInstance3D Cursor;
	private static MeshInstance3D Grid;
	private static MeshInstance3D VideoQuad;
	private static MultiMeshInstance3D NotesMultimesh;
	private static MultiMeshInstance3D CursorTrailMultimesh;
	private static TextureRect Health;
	private static TextureRect ProgressBar;
	private static SubViewport PanelLeft;
	private static SubViewport PanelRight;
	private static Label AccuracyLabel;
	private static Label HitsLabel;
	private static Label MissesLabel;
	private static Label SumLabel;
	private static Label SimpleMissesLabel;
	private static Label ScoreLabel;
	private static Label MultiplierLabel;
	private static AudioStreamPlayer Audio;
	private static AudioStreamPlayer HitSound;
	private static VideoStreamPlayer Video;
	private static Tween HitTween;
	private static Tween MissTween;
	private static bool StopQueued = false;
	private static int MissIcons = 0;

	private double LastFrame = Time.GetTicksUsec(); 	// delta arg unreliable..
	private double LastSecond = Time.GetTicksUsec();	// better framerate calculation
	private List<Dictionary<string, object>> LastCursorPositions = new();	// trail
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
		public int Sum = 0;
		public int Combo = 0;
		public int ComboMultiplier = 1;
		public int ComboMultiplierProgress = 0;
		public int ComboMultiplierIncrement = 0;
		public double ModsMultiplier = 1;
		public int Score = 0;
		public int PassedNotes = 0;
		public double Accuracy = 100;
		public double Health = 100;
		public double HealthStep = 15;
		public Vector2 CursorPosition = Vector2.Zero;
		public Vector2 RawCursorPosition = Vector2.Zero;
		public bool Skippable = false;

		public Attempt(Map map, double speed, string[] mods, string[] players = null, bool replay = false)
		{
			Map = map;
			Speed = speed;
			Players = players ?? Array.Empty<string>();
			Progress = -1000;
			ComboMultiplierIncrement = Map.Notes.Length / 200;
			Mods = new(){
				["NoFail"] = mods.Contains("NoFail"),
				["Ghost"] = mods.Contains("Ghost")
			};

			foreach (KeyValuePair<string, bool> entry in Mods)
			{
				if (entry.Value)
				{
					ModsMultiplier += Constants.ModsMultipliers[entry.Key];
				}
			}
		}

		public void Hit(int index)
		{
			Hits++;
			Sum++;
			Accuracy = Math.Floor((float)Hits / Sum * 10000) / 100;
			Combo++;
			ComboMultiplierProgress++;

			if (ComboMultiplierProgress >= ComboMultiplierIncrement && ComboMultiplier < 8)
			{
				ComboMultiplierProgress = 0;
				ComboMultiplier = Math.Min(8, ComboMultiplier + 1);
			}

			Score += (int)(100 * ComboMultiplier * ModsMultiplier);
			HealthStep = Math.Max(HealthStep / 1.45f, 15);
			Health = Math.Min(100, Health + HealthStep / 1.75f);
			Map.Notes[index].Hit = true;

			ScoreLabel.Text = Util.PadMagnitude(Score.ToString());
			MultiplierLabel.Text = $"{ComboMultiplier}x";
			HitsLabel.Text = $"{Hits}";
			HitsLabel.LabelSettings.FontColor = Color.FromHtml("#ffffffff");
			SumLabel.Text = Util.PadMagnitude(Sum.ToString());
			AccuracyLabel.Text = $"{(Hits + Misses == 0 ? "100.00" : Accuracy.ToString().PadDecimals(2))}%";
			ComboLabel.Text = Combo.ToString();

			if (!Settings.AlwaysPlayHitSound)
			{
				HitSound.Play();
			}

			HitTween?.Kill();
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
			Sum++;
			Accuracy = Math.Floor((float)Hits / Sum * 10000) / 100;
			Combo = 0;
			ComboMultiplierProgress = 0;
			ComboMultiplier = Math.Max(1, ComboMultiplier - 1);
			Health = Math.Max(0, Health - HealthStep);
			HealthStep = Math.Min(HealthStep * 1.2f, 100);

			if (Health <= 0 && !Mods["NoFail"])
			{
				QueueStop();
			}

			MultiplierLabel.Text = $"{ComboMultiplier}x";
			MissesLabel.Text = $"{Misses}";
			SimpleMissesLabel.Text = $"{Misses}";
			MissesLabel.LabelSettings.FontColor = Color.FromHtml("#ffffffff");
			SumLabel.Text = Util.PadMagnitude(Sum.ToString());
			AccuracyLabel.Text = $"{(Hits + Misses == 0 ? "100.00" : Accuracy.ToString().PadDecimals(2))}%";
			ComboLabel.Text = Combo.ToString();

			MissTween?.Kill();
			MissTween = MissesLabel.CreateTween();
			MissTween.TweenProperty(MissesLabel.LabelSettings, "font_color", Color.FromHtml("#ffffffa0"), 1);
			MissTween.Play();

			if (MissIcons >= 64)
			{
				return;
			}

			MissIcons++;

			Sprite3D icon = MissFeedback.Instantiate<Sprite3D>();
			Node3D.AddChild(icon);
			icon.GlobalPosition = new Vector3(Map.Notes[index].X, -1.4f, 0);
			icon.Texture = Phoenyx.Skin.MissFeedbackImage;

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
		VideoQuad = GetNode<MeshInstance3D>("Video");
		NotesMultimesh = GetNode<MultiMeshInstance3D>("Notes");
		CursorTrailMultimesh = GetNode<MultiMeshInstance3D>("CursorTrail");
		Health = GetNode("HealthViewport").GetNode<TextureRect>("Main");
		ProgressBar = GetNode("ProgressBarViewport").GetNode<TextureRect>("Main");
		PanelLeft = GetNode<SubViewport>("PanelLeftViewport");
		PanelRight = GetNode<SubViewport>("PanelRightViewport");
		AccuracyLabel = PanelRight.GetNode<Label>("Accuracy");
		HitsLabel = PanelRight.GetNode<Label>("Hits");
		MissesLabel = PanelRight.GetNode<Label>("Misses");
		SumLabel = PanelRight.GetNode<Label>("Sum");
		SimpleMissesLabel = PanelRight.GetNode<Label>("SimpleMisses");
		ScoreLabel = PanelLeft.GetNode<Label>("Score");
		MultiplierLabel = PanelLeft.GetNode<Label>("Multiplier");
		Audio = GetNode<AudioStreamPlayer>("SongPlayer");
		HitSound = GetNode<AudioStreamPlayer>("HitSoundPlayer");
		Video = GetNode("VideoViewport").GetNode<VideoStreamPlayer>("VideoStreamPlayer");

		if (Settings.SimpleHUD)
		{
			Godot.Collections.Array<Node> widgets = PanelLeft.GetChildren();
			widgets.AddRange(PanelRight.GetChildren());

			foreach (Node widget in widgets)
			{
				(widget as CanvasItem).Visible = false;
			}

			SimpleMissesLabel.Visible = true;
		}

		//foreach (KeyValuePair<string, Player> entry in Lobby.Players)
		//{
		//	ColorRect playerScore = PlayerScore.Instantiate<ColorRect>();
		//	playerScore.GetNode<Label>("Name").Text = entry.Key;
		//	playerScore.Name = entry.Key;
		//	Leaderboard.GetNode("SubViewport").GetNode("Players").AddChild(playerScore);
		//}

		Camera.Fov = (float)Settings.FoV;
		VideoQuad.Transparency = 1;
		TitleLabel.Text = CurrentAttempt.Map.PrettyTitle;
		HitsLabel.LabelSettings.FontColor = Color.FromHtml("#ffffffa0");
		MissesLabel.LabelSettings.FontColor = Color.FromHtml("#ffffffa0");

		Util.DiscordRPC.Call("Set", "details", "Playing a Map");
		Util.DiscordRPC.Call("Set", "state", CurrentAttempt.Map.PrettyTitle);
		Util.DiscordRPC.Call("Set", "end_timestamp", Time.GetUnixTimeFromSystem() + CurrentAttempt.Map.Length / 1000 / CurrentAttempt.Speed);

		Input.MouseMode = Input.MouseModeEnum.Captured;
		Input.UseAccumulatedInput = false;
		DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);

		(Cursor.Mesh as QuadMesh).Size = new Vector2((float)(Constants.CursorSize * Settings.CursorScale), (float)(Constants.CursorSize * Settings.CursorScale));

		try
		{
			(Cursor.GetActiveMaterial(0) as StandardMaterial3D).AlbedoTexture = Phoenyx.Skin.CursorImage;
			(CursorTrailMultimesh.MaterialOverride as StandardMaterial3D).AlbedoTexture = Phoenyx.Skin.CursorImage;
			(Grid.GetActiveMaterial(0) as StandardMaterial3D).AlbedoTexture = Phoenyx.Skin.GridImage;
			Health.Texture = Phoenyx.Skin.HealthImage;
			Health.GetParent().GetNode<TextureRect>("Background").Texture = Phoenyx.Skin.HealthBackgroundImage;
			ProgressBar.Texture = Phoenyx.Skin.ProgressImage;
			ProgressBar.GetParent().GetNode<TextureRect>("Background").Texture = Phoenyx.Skin.ProgressBackgroundImage;
			PanelRight.GetNode<TextureRect>("HitsIcon").Texture = Phoenyx.Skin.HitsImage;
			PanelRight.GetNode<TextureRect>("MissesIcon").Texture = Phoenyx.Skin.MissesImage;
			NotesMultimesh.Multimesh.Mesh = Phoenyx.Skin.NoteMesh;
			HitSound.Stream = Lib.Audio.LoadStream(Phoenyx.Skin.HitSoundBuffer);
		}
		catch (Exception exception)
		{
			ToastNotification.Notify("Could not load skin", 2);
			throw Logger.Error($"Could not load skin; {exception.Message}");
		}

		if (CurrentAttempt.Map.AudioBuffer != null)
		{
			Audio.Stream = Lib.Audio.LoadStream(CurrentAttempt.Map.AudioBuffer);
			Audio.PitchScale = (float)CurrentAttempt.Speed;
			MapLength = (float)Audio.Stream.GetLength() * 1000;
		}
		else
		{
			MapLength = CurrentAttempt.Map.Length + 1000;
		}
		
		if (CurrentAttempt.Map.VideoBuffer != null)
		{
			File.WriteAllBytes($"{Constants.UserFolder}/cache/video.mp4", CurrentAttempt.Map.VideoBuffer);

			Video.Stream.File = $"{Constants.UserFolder}/cache/video.mp4";

			if (CurrentAttempt.Speed != 1)
			{
				ToastNotification.Notify("Videos currently only sync on 1x", 1);
			}
		}

		MapLength += Constants.HitWindow;

		UpdateVolume();
	}

	public override void _Process(double delta)
	{
		ulong now = Time.GetTicksUsec();

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

		if (CurrentAttempt.Map.VideoBuffer != null)
		{
			if (!Video.IsPlaying() && CurrentAttempt.Progress >= 0)
			{
				Video.Play();
				
				Tween videoInTween = VideoQuad.CreateTween();
				videoInTween.TweenProperty(VideoQuad, "transparency", (float)Settings.VideoDim / 100, 0.5);
				videoInTween.Play();
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

			if (Settings.AlwaysPlayHitSound && !CurrentAttempt.Map.Notes[i].Hittable && note.Millisecond < CurrentAttempt.Progress)
			{
				CurrentAttempt.Map.Notes[i].Hittable = true;
				
				HitSound.Play();
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
			ProgressLabel.Modulate = Color.FromHtml("ffffff" + (64 + (int)(172 * (Math.Sin(Math.PI * now / 750000) / 2 + 0.5f))).ToString("X2"));
		}
		else
		{
			TargetSkipLabelAlpha = 0;
			ProgressLabel.Modulate = Color.FromHtml("ffffff40");
		}

		SpeedLabel.Text = $"{CurrentAttempt.Speed.ToString().PadDecimals(2)}x";
		SpeedLabel.Modulate = Color.FromHtml($"#ffffff{(CurrentAttempt.Speed == 1 ? "00" : "20")}");
		ProgressLabel.Text = $"{FormatTime(Math.Max(0, CurrentAttempt.Progress) / 1000)} / {FormatTime(MapLength / 1000)}";
		Health.Size = new Vector2(32 + (float)CurrentAttempt.Health * 10.24f, 80);
		ProgressBar.Size = new Vector2(1088 * (float)(CurrentAttempt.Progress / MapLength), 80);
		SkipLabel.Modulate = Color.FromHtml("#ffffff" + Math.Min(255, (int)(255 * SkipLabelAlpha)).ToString("X2"));

		if (StopQueued)
		{
			StopQueued = false;
			Stop();
			return;
		}

		// trail stuff
		
		if (Settings.CursorTrail)
		{
			List<Dictionary<string, object>> newList = new();

			LastCursorPositions.Add(new(){
				["Time"] = now,
				["Position"] = CurrentAttempt.CursorPosition
			});

			foreach (Dictionary<string, object> entry in LastCursorPositions)
			{
				if (now - (ulong)entry["Time"] >= (Settings.TrailTime * 1000000))
				{
					continue;
				}

				newList.Add(entry);
			}

			LastCursorPositions = newList;
			
			int count = LastCursorPositions.Count;
			float size = ((Vector2)Cursor.Mesh.Get("size")).X;
			Transform3D transform = new Transform3D(new Vector3(size, 0, 0), new Vector3(0, size, 0), new Vector3(0, 0, size), Vector3.Zero);
			int j = 0;

			CursorTrailMultimesh.Multimesh.InstanceCount = count;

			foreach (Dictionary<string, object> entry in LastCursorPositions)
			{
				ulong difference = now - (ulong)entry["Time"];
				uint alpha = (uint)((float)(difference) / (Settings.TrailTime * 1000000) * 255);

				transform.Origin = new Vector3(((Vector2)entry["Position"]).X, ((Vector2)entry["Position"]).Y, 0);

				CursorTrailMultimesh.Multimesh.SetInstanceTransform(j, transform);
				CursorTrailMultimesh.Multimesh.SetInstanceColor(j, Color.FromHtml($"ffffff{255 - alpha:X2}"));
				j++;
			}
		}
		else
		{
			CursorTrailMultimesh.Multimesh.InstanceCount = 0;
		}
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseMotion eventMouseMotion)
		{
			float videoHeight = 2 * (float)Math.Sqrt(Math.Pow(103.75 / Math.Cos(Mathf.DegToRad(Settings.FoV / 2)), 2) - Math.Pow(103.75, 2));
			(VideoQuad.Mesh as QuadMesh).Size = new Vector2(videoHeight / 0.5625f, videoHeight);	// don't use 16:9? too bad lol

			if (Settings.CameraLock)
			{
				if (Settings.CursorDrift)
				{
					CurrentAttempt.CursorPosition = (CurrentAttempt.CursorPosition + new Vector2(1, -1) * eventMouseMotion.Relative / 100 * (float)Settings.Sensitivity).Clamp(-Constants.Bounds, Constants.Bounds);
				}
				else
				{
					CurrentAttempt.RawCursorPosition += new Vector2(1, -1) * eventMouseMotion.Relative / 100 * (float)Settings.Sensitivity;
					CurrentAttempt.CursorPosition = CurrentAttempt.RawCursorPosition.Clamp(-Constants.Bounds, Constants.Bounds);
				}

				Cursor.Position = new Vector3(CurrentAttempt.CursorPosition.X, CurrentAttempt.CursorPosition.Y, 0);
				Camera.Position = new Vector3(0, 0, 3.75f) + new Vector3(CurrentAttempt.CursorPosition.X, CurrentAttempt.CursorPosition.Y, 0) * (float)Settings.Parallax;
				Camera.Rotation = Vector3.Zero;
				
				VideoQuad.Position = new Vector3(Camera.Position.X, Camera.Position.Y, -100);
			}
			else
			{
				Camera.Rotation += new Vector3(-eventMouseMotion.Relative.Y / 120 * (float)Settings.Sensitivity / (float)Math.PI, -eventMouseMotion.Relative.X / 120 * (float)Settings.Sensitivity / (float)Math.PI, 0);
				Camera.Position = new Vector3(0, 0, 3.5f) + Camera.Basis.Z / 4;
				
				float hypotenuse = 3.5f / Camera.Basis.Z.Z;
				float distance = (float)Math.Sqrt(Math.Pow(hypotenuse, 2) - Math.Pow(3.5f, 2));
				
				CurrentAttempt.RawCursorPosition = new Vector2(Camera.Basis.Z.X, Camera.Basis.Z.Y).Normalized() * -distance;
				CurrentAttempt.CursorPosition = CurrentAttempt.RawCursorPosition.Clamp(-Constants.Bounds, Constants.Bounds);
				Cursor.Position = new Vector3(CurrentAttempt.CursorPosition.X, CurrentAttempt.CursorPosition.Y, 0);

				VideoQuad.Position = Camera.Position - Camera.Basis.Z * 103.75f;
				VideoQuad.Rotation = Camera.Rotation;
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
		SceneManager.Load("res://scenes/main_menu.tscn");
	}

	public static void UpdateVolume()
	{
		Audio.VolumeDb = -80 + 70 * (float)Math.Pow(Settings.VolumeMusic / 100, 0.1) * (float)Math.Pow(Settings.VolumeMaster / 100, 0.1);
		HitSound.VolumeDb = -80 + 80 * (float)Math.Pow(Settings.VolumeSFX / 100, 0.1) * (float)Math.Pow(Settings.VolumeMaster / 100, 0.1);
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
}