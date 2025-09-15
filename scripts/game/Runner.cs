using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Security.Cryptography;
using Godot;

public partial class Runner : Node3D
{
	private static Node3D Node3D;
	private static readonly PackedScene PlayerScore = GD.Load<PackedScene>("res://prefabs/player_score.tscn");
	private static readonly PackedScene HitFeedback = GD.Load<PackedScene>("res://prefabs/hit_popup.tscn");
	private static readonly PackedScene MissFeedback = GD.Load<PackedScene>("res://prefabs/miss_icon.tscn");
	private static readonly PackedScene ModifierIcon = GD.Load<PackedScene>("res://prefabs/modifier.tscn");

	private static Panel Menu;
	private static Label FPSCounter;
	private static Camera3D Camera;
	private static Label3D TitleLabel;
	private static Label3D ComboLabel;
	private static Label3D SpeedLabel;
	private static Label3D SkipLabel;
	private static Label3D ProgressLabel;
	private static TextureRect Jesus;
	private static MeshInstance3D Cursor;
	private static MeshInstance3D Grid;
	private static MeshInstance3D VideoQuad;
	private static MultiMeshInstance3D NotesMultimesh;
	private static MultiMeshInstance3D CursorTrailMultimesh;
	private static TextureRect HealthTexture;
	private static TextureRect ProgressBarTexture;
	private static SubViewport PanelLeft;
	private static SubViewport PanelRight;
	private static AudioStreamPlayer Bell;
	private static Panel ReplayViewer;
	private static TextureButton ReplayViewerPause;
	private static Label ReplayViewerLabel;
	private static HSlider ReplayViewerSeek;
	private static Label AccuracyLabel;
	private static Label HitsLabel;
	private static Label MissesLabel;
	private static Label SumLabel;
	private static Label SimpleMissesLabel;
	private static Label ScoreLabel;
	private static Label MultiplierLabel;
	private static Panel MultiplierProgressPanel;
	private static ShaderMaterial MultiplierProgressMaterial;
	private static float MultiplierProgress = 0;	// more efficient than spamming material.GetShaderParameter()
	private static Color MultiplierColour = Color.Color8(255, 255, 255);
	private static VideoStreamPlayer Video;
	private static Tween HitTween;
	private static Tween MissTween;
	private static bool StopQueued = false;
	private static int HitPopups = 0;
	private static int MissPopups = 0;
	private static bool ReplayViewerSeekHovered = false;
	private static bool LeftMouseButtonDown = false;

	private double LastFrame = Time.GetTicksUsec(); 	// delta arg unreliable..
	private double LastSecond = Time.GetTicksUsec();	// better framerate calculation
	private List<Dictionary<string, object>> LastCursorPositions = [];	// trail
	private int FrameCount = 0;
	private float SkipLabelAlpha = 0;
	private float TargetSkipLabelAlpha = 0;

	public static bool Playing = false;
	public static ulong Started = 0;
	public static bool MenuShown = false;
	public static bool SettingsShown = false;
	public static bool ReplayViewerShown = false;
	public static int ToProcess = 0;
	public static List<Note> ProcessNotes = [];
	public static Attempt CurrentAttempt;
	public static double MapLength;
	public static Tween JesusTween;
	public static MeshInstance3D[] Cursors;

	public struct Attempt
	{
		public string ID = "";
		public bool Stopped = false;
		public bool IsReplay = false;
		public Replay[] Replays;	// when reading replays
		public float LongestReplayLength = 0;
		public List<float[]> ReplayFrames = [];	// when writing replays
		public List<float> ReplaySkips = [];
		public ulong LastReplayFrame = 0;
		public uint ReplayFrameCountOffset = 0;
		public uint ReplayAttemptStatusOffset = 0;
		public Godot.FileAccess ReplayFile;
		public double Progress = 0;	// ms
		public Map Map = new();
		public double Speed = 1;
		public double StartFrom = 0;
		public ulong FirstNote = 0;
		public Dictionary<string, bool> Mods;
		public string[] Players = [];
		public bool Alive = true;
		public bool Skippable = false;
		public bool Qualifies = true;
		public uint Hits = 0;
		public float[] HitsInfo = [];
		public Color LastHitColour = Phoenyx.Skin.Colors[^1];
		public uint Misses = 0;
		public double DeathTime = -1;
		public uint Sum = 0;
		public uint Combo = 0;
		public uint ComboMultiplier = 1;
		public uint ComboMultiplierProgress = 0;
		public uint ComboMultiplierIncrement = 0;
		public double ModsMultiplier = 1;
		public uint Score = 0;
		public uint PassedNotes = 0;
		public double Accuracy = 100;
		public double Health = 100;
		public double HealthStep = 15;
		public Vector2 CursorPosition = Vector2.Zero;
		public Vector2 RawCursorPosition = Vector2.Zero;
		public double DistanceMM = 0;

		public Attempt(Map map, double speed, double startFrom, Dictionary<string, bool> mods, string[] players = null, Replay[] replays = null)
		{
			ID = $"{map.ID}_{OS.GetUniqueId()}_{Time.GetDatetimeStringFromUnixTime((long)Time.GetUnixTimeFromSystem())}".Replace(":", "_");
			Replays = replays;
			IsReplay = Replays != null;
			Map = map;
			Speed = speed;
			StartFrom = startFrom;
			Players = players ?? [];
			Progress = -1000 - Phoenyx.Settings.ApproachTime * 1000 + StartFrom;
			ComboMultiplierIncrement = Math.Max(2, (uint)Map.Notes.Length / 200);
			Mods = [];
			HitsInfo = IsReplay ? Replays[0].Notes : new float[Map.Notes.Length];

			foreach (KeyValuePair<string, bool> mod in mods)
			{
				Mods[mod.Key] = mod.Value;
			}

			if (StartFrom > 0)
			{
				Qualifies = false;

				foreach (Note note in Map.Notes)
				{
					if (note.Millisecond < StartFrom)
					{
						FirstNote = (ulong)note.Index + 1;
					}
				}
			}

			if (!IsReplay && Phoenyx.Settings.RecordReplays && !Map.Ephemeral)
			{
				ReplayFile = Godot.FileAccess.Open($"{Phoenyx.Constants.UserFolder}/replays/{ID}.phxr", Godot.FileAccess.ModeFlags.Write);
				ReplayFile.StoreString("phxr");	// sig
				ReplayFile.Store8(1);	// replay file version

				string mapFileName = Map.FilePath.GetFile().GetBaseName();

				ReplayFile.StoreDouble(Speed);
				ReplayFile.StoreDouble(StartFrom);
				ReplayFile.StoreDouble(Phoenyx.Settings.ApproachRate);
				ReplayFile.StoreDouble(Phoenyx.Settings.ApproachDistance);
				ReplayFile.StoreDouble(Phoenyx.Settings.FadeIn);
				ReplayFile.Store8((byte)(Phoenyx.Settings.FadeOut ? 1 : 0));
				ReplayFile.Store8((byte)(Phoenyx.Settings.Pushback ? 1 : 0));
				ReplayFile.StoreDouble(Phoenyx.Settings.Parallax);
				ReplayFile.StoreDouble(Phoenyx.Settings.FoV);
				ReplayFile.StoreDouble(Phoenyx.Settings.NoteSize);
				ReplayFile.StoreDouble(Phoenyx.Settings.Sensitivity);

				ReplayAttemptStatusOffset = (uint)ReplayFile.GetPosition();

				ReplayFile.Store8(0);	// reserve attempt status

				string modifiers = "";
				string player = "You";

				foreach (KeyValuePair<string, bool> mod in Mods)
				{
					if (mod.Value)
					{
						modifiers += $"{mod.Key}_";
					}
				}

				modifiers = modifiers.TrimSuffix("_");

				ReplayFile.Store32((uint)modifiers.Length);
				ReplayFile.StoreString(modifiers);
				ReplayFile.Store32((uint)mapFileName.Length);
				ReplayFile.StoreString(mapFileName);
				ReplayFile.Store64((ulong)Map.Notes.Length);
				ReplayFile.Store32((uint)player.Length);
				ReplayFile.StoreString(player);

				ReplayFrameCountOffset = (uint)ReplayFile.GetPosition();

				ReplayFile.Store64(0);	// reserve frame count
			}
			else if (IsReplay)
			{
				foreach (Replay replay in Replays)
				{
					if (replay.Length > LongestReplayLength)
					{
						LongestReplayLength = replay.Length;
					}
				}
			}

			foreach (KeyValuePair<string, bool> entry in Mods)
			{
				if (entry.Value)
				{
					ModsMultiplier += Phoenyx.Constants.ModsMultiplierIncrement[entry.Key];
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

			LastHitColour = Phoenyx.Skin.Colors[index % Phoenyx.Skin.Colors.Length];

			float lateness = IsReplay ? HitsInfo[index] : (float)(((int)Progress - Map.Notes[index].Millisecond) / Speed);
			float factor = 1 - Math.Max(0, lateness - 25) / 150f;
			
			if (!IsReplay)
			{
				Phoenyx.Stats.NotesHit++;

				if (Combo > Phoenyx.Stats.HighestCombo)
				{
					Phoenyx.Stats.HighestCombo = Combo;
				}

				HitsInfo[index] = lateness;
			}

			if (ComboMultiplierProgress == ComboMultiplierIncrement)
			{
				if (ComboMultiplier < 8)
				{
					ComboMultiplierProgress = ComboMultiplier == 7 ? ComboMultiplierIncrement : 0;
					ComboMultiplier++;

					if (ComboMultiplier == 8)
					{
						MultiplierColour = Color.Color8(255, 140, 0);
					}
				}
			}

			uint hitScore = (uint)(100 * ComboMultiplier * ModsMultiplier * factor * ((Speed - 1) / 2.5 + 1));

			Score += hitScore;
			HealthStep = Math.Max(HealthStep / 1.45, 15);
			Health = Math.Min(100, Health + HealthStep / 1.75);
			Map.Notes[index].Hit = true;

			ScoreLabel.Text = Lib.String.PadMagnitude(Score.ToString());
			MultiplierLabel.Text = $"{ComboMultiplier}x";
			HitsLabel.Text = $"{Hits}";
			HitsLabel.LabelSettings.FontColor = Color.Color8(255, 255, 255, 255);
			SumLabel.Text = Lib.String.PadMagnitude(Sum.ToString());
			AccuracyLabel.Text = $"{(Hits + Misses == 0 ? "100.00" : Accuracy.ToString().PadDecimals(2))}%";
			ComboLabel.Text = Combo.ToString();

			if (!Phoenyx.Settings.AlwaysPlayHitSound)
			{
				SoundManager.HitSound.Play();
			}

			HitTween?.Kill();
			HitTween = HitsLabel.CreateTween();
			HitTween.TweenProperty(HitsLabel.LabelSettings, "font_color", Color.Color8(255, 255, 255, 160), 1);
			HitTween.Play();

			if (!Phoenyx.Settings.HitPopups || HitPopups >= 64)
			{
				return;
			}

			HitPopups++;

			Label3D popup = HitFeedback.Instantiate<Label3D>();
			Node3D.AddChild(popup);
			popup.GlobalPosition = new Vector3(Map.Notes[index].X, -1.4f, 0);
			popup.Text = hitScore.ToString();

			Tween tween = popup.CreateTween();
			tween.TweenProperty(popup, "transparency", 1, 0.25f);
			tween.Parallel().TweenProperty(popup, "position", popup.Position + Vector3.Up / 4f, 0.25f).SetTrans(Tween.TransitionType.Quint).SetEase(Tween.EaseType.Out);
			tween.TweenCallback(Callable.From(() => {
				HitPopups--;
				popup.QueueFree();
			}));
			tween.Play();
		}

		public void Miss(int index)
		{
			Misses++;
			Sum++;
			Accuracy = Mathf.Floor((float)Hits / Sum * 10000) / 100;
			Combo = 0;
			ComboMultiplierProgress = 0;
			ComboMultiplier = Math.Max(1, ComboMultiplier - 1);
			Health = Math.Max(0, Health - HealthStep);
			HealthStep = Math.Min(HealthStep * 1.2, 100);

			if (!IsReplay)
			{
				HitsInfo[index] = -1;
				Phoenyx.Stats.NotesMissed++;
			}

			//if (Health - HealthStep <= 0)
			//{
			//	Bell.Play();
			//	Jesus.Modulate = Color.Color8(255, 255, 255, 196);
			//
			//	JesusTween?.Kill();
			//	JesusTween = Jesus.CreateTween();
			//	JesusTween.TweenProperty(Jesus, "modulate", Color.Color8(255, 255, 255, 0), 1);
			//	JesusTween.Play();
			//}

			if (!IsReplay && Health <= 0)
			{
				if (Alive)
				{
					Alive = false;
					Qualifies = false;
					DeathTime = Progress;
					SoundManager.FailSound.Play();

					HealthTexture.Modulate = Color.Color8(255, 255, 255, 128);
					HealthTexture.GetParent().GetNode<TextureRect>("Background").Modulate = HealthTexture.Modulate;
				}

				if (!Mods["NoFail"])
				{
					QueueStop();
				}
			}

			MultiplierLabel.Text = $"{ComboMultiplier}x";
			MissesLabel.Text = $"{Misses}";
			SimpleMissesLabel.Text = $"{Misses}";
			MissesLabel.LabelSettings.FontColor = Color.Color8(255, 255, 255, 255);
			SumLabel.Text = Lib.String.PadMagnitude(Sum.ToString());
			AccuracyLabel.Text = $"{(Hits + Misses == 0 ? "100.00" : Accuracy.ToString().PadDecimals(2))}%";
			ComboLabel.Text = Combo.ToString();

			MissTween?.Kill();
			MissTween = MissesLabel.CreateTween();
			MissTween.TweenProperty(MissesLabel.LabelSettings, "font_color", Color.Color8(255, 255, 255, 160), 1);
			MissTween.Play();

			if (!Phoenyx.Settings.MissPopups || MissPopups >= 64)
			{
				return;
			}

			MissPopups++;

			Sprite3D icon = MissFeedback.Instantiate<Sprite3D>();
			Node3D.AddChild(icon);
			icon.GlobalPosition = new Vector3(Map.Notes[index].X, -1.4f, 0);
			icon.Texture = Phoenyx.Skin.MissFeedbackImage;

			Tween tween = icon.CreateTween();
			tween.TweenProperty(icon, "transparency", 1, 0.25f);
			tween.Parallel().TweenProperty(icon, "position", icon.Position + Vector3.Up / 4f, 0.25f).SetTrans(Tween.TransitionType.Quint).SetEase(Tween.EaseType.Out);
			tween.TweenCallback(Callable.From(() => {
				MissPopups--;
				icon.QueueFree();
			}));
			tween.Play();
		}

		public void Stop()
		{
			if (Stopped)
			{
				return;
			}

			Stopped = true;

			if (!IsReplay && ReplayFile != null)
			{
				ReplayFile.Seek(ReplayAttemptStatusOffset);
				ReplayFile.Store8((byte)(Alive ? (Qualifies ? 0 : 1) : 2));

				ReplayFile.Seek(ReplayFrameCountOffset);
				ReplayFile.Store64((ulong)ReplayFrames.Count);

				foreach (float[] frame in ReplayFrames)
				{
					ReplayFile.StoreFloat(frame[0]);
					ReplayFile.StoreFloat(frame[1]);
					ReplayFile.StoreFloat(frame[2]);
				}

				ReplayFile.Seek(ReplayFile.GetLength());
				ReplayFile.Store64(FirstNote);
				ReplayFile.Store64(Sum);

				for (ulong i = FirstNote; i < FirstNote + Sum; i++)
				{
					ReplayFile.Store8((byte)(HitsInfo[i] == -1 ? 255 : Math.Min(254, HitsInfo[i] * (254 / 55))));
				}

				ReplayFile.Store64((ulong)ReplaySkips.Count);

				foreach (float skip in ReplaySkips)
				{
					ReplayFile.StoreFloat(skip);
				}

				ReplayFile.Close();
				ReplayFile = Godot.FileAccess.Open($"{Phoenyx.Constants.UserFolder}/replays/{ID}.phxr", Godot.FileAccess.ModeFlags.ReadWrite);

				ulong length = ReplayFile.GetLength();
				byte[] hash = SHA256.HashData(ReplayFile.GetBuffer((long)length));

				ReplayFile.StoreBuffer(hash);
				ReplayFile.Close();
				
				CurrentAttempt.HitsInfo = CurrentAttempt.HitsInfo[0 .. (int)PassedNotes];
			}
			else if (IsReplay)
			{
				CurrentAttempt.HitsInfo = CurrentAttempt.HitsInfo[0 .. (int)CurrentAttempt.Replays[0].LastNote];
			}
		}
	}
	
	public override void _Ready()
	{
		Node3D = this;

		Menu = GetNode<Panel>("Menu");
		FPSCounter = GetNode<Label>("FPSCounter");
		Camera = GetNode<Camera3D>("Camera3D");
		TitleLabel = GetNode<Label3D>("Title");
		ComboLabel = GetNode<Label3D>("Combo");
		SpeedLabel = GetNode<Label3D>("Speed");
		SkipLabel = GetNode<Label3D>("Skip");
		ProgressLabel = GetNode<Label3D>("Progress");
		Jesus = GetNode<TextureRect>("Jesus");
		Cursor = GetNode<MeshInstance3D>("Cursor");
		Grid = GetNode<MeshInstance3D>("Grid");
		VideoQuad = GetNode<MeshInstance3D>("Video");
		NotesMultimesh = GetNode<MultiMeshInstance3D>("Notes");
		CursorTrailMultimesh = GetNode<MultiMeshInstance3D>("CursorTrail");
		HealthTexture = GetNode("HealthViewport").GetNode<TextureRect>("Main");
		ProgressBarTexture = GetNode("ProgressBarViewport").GetNode<TextureRect>("Main");
		PanelLeft = GetNode<SubViewport>("PanelLeftViewport");
		PanelRight = GetNode<SubViewport>("PanelRightViewport");
		Bell = GetNode<AudioStreamPlayer>("Bell");
		ReplayViewer = GetNode<Panel>("ReplayViewer");
		ReplayViewerPause = ReplayViewer.GetNode<TextureButton>("Pause");
		ReplayViewerLabel = ReplayViewer.GetNode<Label>("Time");
		ReplayViewerSeek = ReplayViewer.GetNode<HSlider>("Seek");
		AccuracyLabel = PanelRight.GetNode<Label>("Accuracy");
		HitsLabel = PanelRight.GetNode<Label>("Hits");
		MissesLabel = PanelRight.GetNode<Label>("Misses");
		SumLabel = PanelRight.GetNode<Label>("Sum");
		SimpleMissesLabel = PanelRight.GetNode<Label>("SimpleMisses");
		ScoreLabel = PanelLeft.GetNode<Label>("Score");
		MultiplierLabel = PanelLeft.GetNode<Label>("Multiplier");
		MultiplierProgressPanel = PanelLeft.GetNode<Panel>("MultiplierProgress");
		MultiplierProgressMaterial = MultiplierProgressPanel.Material as ShaderMaterial;
		Video = GetNode("VideoViewport").GetNode<VideoStreamPlayer>("VideoStreamPlayer");

		List<string> activeMods = [];

		foreach (KeyValuePair<string, bool> mod in CurrentAttempt.Mods)
		{
			if (mod.Value)
			{
				activeMods.Add(mod.Key);
			}
		}

		for (int i = 0; i < activeMods.Count; i++)
		{
			Sprite3D icon = ModifierIcon.Instantiate<Sprite3D>();

			AddChild(icon);

			icon.Position = new(i * 1.5f - activeMods.Count / 1.5f, -8.5f, -10f);
			icon.Texture = Phoenyx.Util.GetModIcon(activeMods[i]);
		}

		Panel menuButtonsHolder = Menu.GetNode<Panel>("Holder");

		Menu.GetNode<Button>("Button").Pressed += HideMenu;
		menuButtonsHolder.GetNode<Button>("Resume").Pressed += HideMenu;
		menuButtonsHolder.GetNode<Button>("Restart").Pressed += Restart;
		menuButtonsHolder.GetNode<Button>("Settings").Pressed += () => {
			SettingsManager.ShowSettings();
		};
		menuButtonsHolder.GetNode<Button>("Quit").Pressed += () => {
			if (CurrentAttempt.Alive)
			{
				SoundManager.FailSound.Play();
			}

			CurrentAttempt.Alive = false;
			CurrentAttempt.Qualifies = false;
			
			if (CurrentAttempt.DeathTime == -1)
			{
				CurrentAttempt.DeathTime = CurrentAttempt.Progress;
			}

			Stop();
		};

		ReplayViewerPause.Pressed += () => {
			Playing = !Playing;
			SoundManager.Song.PitchScale = Playing ? (float)CurrentAttempt.Speed : 0.00000000000001f;	// ooohh my goood
			ReplayViewerPause.TextureNormal = GD.Load<Texture2D>(Playing ? "res://textures/pause.png" : "res://textures/play.png");
		};

		ReplayViewerSeek.ValueChanged += (double value) => {
			ReplayViewerLabel.Text = $"{Lib.String.FormatTime(value * CurrentAttempt.LongestReplayLength / 1000)} / {Lib.String.FormatTime(CurrentAttempt.LongestReplayLength / 1000)}";
		};
		ReplayViewerSeek.DragEnded += (bool _) => {
			CurrentAttempt.Hits = 0;
			CurrentAttempt.Misses = 0;
			CurrentAttempt.Sum = 0;
			CurrentAttempt.Accuracy = 100;
			CurrentAttempt.Score = 0;
			CurrentAttempt.PassedNotes = 0;
			CurrentAttempt.Combo = 0;
			CurrentAttempt.ComboMultiplier = 1;
			CurrentAttempt.ComboMultiplierProgress = 0;
			CurrentAttempt.Health = 100;
			CurrentAttempt.HealthStep = 15;

			HitsLabel.Text = "0";
			MissesLabel.Text = "0";
			SimpleMissesLabel.Text = "0";
			SumLabel.Text = "0";
			AccuracyLabel.Text = "100.00%";
			ScoreLabel.Text = "0";
			ComboLabel.Text = "0";
			MultiplierLabel.Text = "1x";
			MultiplierProgress = 0;
			MultiplierColour = Color.Color8(255, 255, 255);

			for (int i = 0; i < CurrentAttempt.Map.Notes.Length; i++)
			{
				CurrentAttempt.Map.Notes[i].Hit = false;
			}

			CurrentAttempt.Progress = (float)ReplayViewerSeek.Value * CurrentAttempt.LongestReplayLength;

			for (int i = 0; i < CurrentAttempt.Replays.Length; i++)
			{
				Cursors[i].Transparency = 0;
				CurrentAttempt.Replays[i].Complete = false;

				for (int j = 0; j < CurrentAttempt.Replays[i].Frames.Length; j++)
				{
					if (CurrentAttempt.Progress < CurrentAttempt.Replays[i].Frames[j].Progress)
					{
						CurrentAttempt.Replays[i].FrameIndex = Math.Max(0, j - 1);
						break;
					}
				}
			}

			if (!SoundManager.Song.Playing)
			{
				SoundManager.Song.Play();
			}

			SoundManager.Song.Seek((float)CurrentAttempt.Progress / 1000);
		};
		ReplayViewerSeek.FocusEntered += () => {
			ReplayViewerSeekHovered = true;
		};
		ReplayViewerSeek.FocusExited += () => {
			ReplayViewerSeekHovered = false;
		};

		if (Phoenyx.Settings.SimpleHUD)
		{
			Godot.Collections.Array<Node> widgets = PanelLeft.GetChildren();
			widgets.AddRange(PanelRight.GetChildren());

			foreach (Node widget in widgets)
			{
				(widget as CanvasItem).Visible = false;
			}

			SimpleMissesLabel.Visible = true;
		}

		float fov = (float)(CurrentAttempt.IsReplay ? CurrentAttempt.Replays[0].FoV : Phoenyx.Settings.FoV);

		MenuShown = false;
		Camera.Fov = fov;
		VideoQuad.Transparency = 1;
		TitleLabel.Text = CurrentAttempt.Map.PrettyTitle;
		HitsLabel.LabelSettings.FontColor = Color.Color8(255, 255, 255, 160);
		MissesLabel.LabelSettings.FontColor = Color.Color8(255, 255, 255, 160);
		SpeedLabel.Text = $"{CurrentAttempt.Speed.ToString().PadDecimals(2)}x";
		SpeedLabel.Modulate = Color.Color8(255, 255, 255, (byte)(CurrentAttempt.Speed == 1 ? 0 : 32));

		float videoHeight = 2 * (float)Math.Sqrt(Math.Pow(103.75 / Math.Cos(Mathf.DegToRad(fov / 2)), 2) - Math.Pow(103.75, 2));

		(VideoQuad.Mesh as QuadMesh).Size = new(videoHeight / 0.5625f, videoHeight);	// don't use 16:9? too bad lol
		Video.GetParent<SubViewport>().Size = new((int)(1920 * Phoenyx.Settings.VideoRenderScale / 100), (int)(1080 * Phoenyx.Settings.VideoRenderScale / 100));

		MultiplierProgress = 0;
		MultiplierColour = Color.Color8(255, 255, 255);

		MultiplierProgressMaterial.SetShaderParameter("progress", 0);
		MultiplierProgressMaterial.SetShaderParameter("colour", MultiplierColour);
		MultiplierProgressMaterial.SetShaderParameter("sides", Math.Clamp(CurrentAttempt.ComboMultiplierIncrement, 3, 32));

		Phoenyx.Util.DiscordRPC.Call("Set", "details", "Playing a Map");
		Phoenyx.Util.DiscordRPC.Call("Set", "state", CurrentAttempt.Map.PrettyTitle);
		Phoenyx.Util.DiscordRPC.Call("Set", "end_timestamp", Time.GetUnixTimeFromSystem() + CurrentAttempt.Map.Length / 1000 / CurrentAttempt.Speed);

		Input.MouseMode = Phoenyx.Settings.AbsoluteInput || CurrentAttempt.IsReplay ? Input.MouseModeEnum.ConfinedHidden : Input.MouseModeEnum.Captured;
		Input.UseAccumulatedInput = false;
		DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);

		(Cursor.Mesh as QuadMesh).Size = new Vector2((float)(Phoenyx.Constants.CursorSize * Phoenyx.Settings.CursorScale), (float)(Phoenyx.Constants.CursorSize * Phoenyx.Settings.CursorScale));

		try
		{
			(Cursor.GetActiveMaterial(0) as StandardMaterial3D).AlbedoTexture = Phoenyx.Skin.CursorImage;
			(CursorTrailMultimesh.MaterialOverride as StandardMaterial3D).AlbedoTexture = Phoenyx.Skin.CursorImage;
			(Grid.GetActiveMaterial(0) as StandardMaterial3D).AlbedoTexture = Phoenyx.Skin.GridImage;
			PanelLeft.GetNode<TextureRect>("Background").Texture = Phoenyx.Skin.PanelLeftBackgroundImage;
			PanelRight.GetNode<TextureRect>("Background").Texture = Phoenyx.Skin.PanelRightBackgroundImage;
			HealthTexture.Texture = Phoenyx.Skin.HealthImage;
			HealthTexture.GetParent().GetNode<TextureRect>("Background").Texture = Phoenyx.Skin.HealthBackgroundImage;
			ProgressBarTexture.Texture = Phoenyx.Skin.ProgressImage;
			ProgressBarTexture.GetParent().GetNode<TextureRect>("Background").Texture = Phoenyx.Skin.ProgressBackgroundImage;
			PanelRight.GetNode<TextureRect>("HitsIcon").Texture = Phoenyx.Skin.HitsImage;
			PanelRight.GetNode<TextureRect>("MissesIcon").Texture = Phoenyx.Skin.MissesImage;
			NotesMultimesh.Multimesh.Mesh = Phoenyx.Skin.NoteMesh;
		}
		catch (Exception exception)
		{
			ToastNotification.Notify("Could not load skin", 2);
			throw Logger.Error($"Could not load skin; {exception.Message}");
		}

		string space = Phoenyx.Settings.Space == "skin" ? Phoenyx.Skin.Space : Phoenyx.Settings.Space;

		if (space != "void")
		{
			Node3D.AddChild(GD.Load<PackedScene>($"res://prefabs/spaces/{space}.tscn").Instantiate<Node3D>());
		}

		SoundManager.UpdateSounds();

		if (CurrentAttempt.Map.AudioBuffer != null)
		{
			SoundManager.Song.Stream = Lib.Audio.LoadStream(CurrentAttempt.Map.AudioBuffer);
			SoundManager.Song.PitchScale = (float)CurrentAttempt.Speed;
			MapLength = (float)SoundManager.Song.Stream.GetLength() * 1000;
		}
		else
		{
			MapLength = CurrentAttempt.Map.Length + 1000;
		}

		MapLength += Phoenyx.Constants.HitWindow;
		
		if (Phoenyx.Settings.VideoDim < 100 && CurrentAttempt.Map.VideoBuffer != null)
		{
			if (CurrentAttempt.Speed != 1)
			{
				ToastNotification.Notify("Videos currently only sync on 1x", 1);
			}
			else
			{
				File.WriteAllBytes($"{Phoenyx.Constants.UserFolder}/cache/video.mp4", CurrentAttempt.Map.VideoBuffer);
				Video.Stream.File = $"{Phoenyx.Constants.UserFolder}/cache/video.mp4";
			}
		}
		if (CurrentAttempt.Replays != null)
		{
			if (CurrentAttempt.Replays.Length > 1)
			{
				CurrentAttempt.Replays[0].Pushback = false;
			}

			CurrentAttempt.Mods["Spin"] = true;
			Cursors = new MeshInstance3D[CurrentAttempt.Replays.Length];

			for (int i = 0; i < CurrentAttempt.Replays.Length; i++)
			{
				if (!CurrentAttempt.Replays[i].Modifiers["Spin"])
				{
					CurrentAttempt.Mods["Spin"] = false;
				}

				MeshInstance3D cursor = Cursor.Duplicate() as MeshInstance3D;
				cursor.Name = $"_cursor{i}";
				Node3D.AddChild(cursor);
				Cursors[i] = cursor;
			}
			
			Cursor.Visible = false;
			ShowReplayViewer();
		}

		UpdateVolume();
	}

	public override void _PhysicsProcess(double delta)
	{
		MultiplierProgress = Mathf.Lerp(MultiplierProgress, (float)CurrentAttempt.ComboMultiplierProgress / CurrentAttempt.ComboMultiplierIncrement, Math.Min(1, (float)delta * 16));
		MultiplierColour = MultiplierColour.Lerp(Color.Color8(255, 255, 255), (float)delta * 2);
		MultiplierProgressMaterial.SetShaderParameter("progress", MultiplierProgress);
	
		if (MultiplierColour.B < 255)	// fuck
		{
			MultiplierProgressMaterial.SetShaderParameter("colour", MultiplierColour);	// this loves causing lag spikes, keep track
		}
	}

	public override void _Process(double delta)
	{
		ulong now = Time.GetTicksUsec();
		delta = (now - LastFrame) / 1000000;	// more reliable
		LastFrame = now;
		FrameCount++;
		SkipLabelAlpha = Mathf.Lerp(SkipLabelAlpha, TargetSkipLabelAlpha, Math.Min(1, (float)delta * 20));

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

		if (CurrentAttempt.IsReplay)
		{
			if (!ReplayViewerSeekHovered || !LeftMouseButtonDown)
			{
				ReplayViewerSeek.Value = CurrentAttempt.Progress / CurrentAttempt.LongestReplayLength;
			}

			Vector2 positionSum = new();

			for (int i = 0; i < CurrentAttempt.Replays.Length; i++)
			{
				for (int j = CurrentAttempt.Replays[i].FrameIndex; j < CurrentAttempt.Replays[i].Frames.Length; j++)
				{
					if (CurrentAttempt.Progress < CurrentAttempt.Replays[i].Frames[j].Progress)
					{
						CurrentAttempt.Replays[i].FrameIndex = Math.Max(0, j - 1);
						break;
					}
				}

				int next = Math.Min(CurrentAttempt.Replays[i].FrameIndex + 1, CurrentAttempt.Replays[i].Frames.Length - 2);

				if (!CurrentAttempt.Replays[i].Complete && CurrentAttempt.Progress >= CurrentAttempt.Replays[i].Length)
				{
					CurrentAttempt.Replays[i].Complete = true;
					CurrentAttempt.Replays[i].LastNote = CurrentAttempt.PassedNotes;

					Tween tween = Cursors[i].CreateTween();
					tween.TweenProperty(Cursors[i], "transparency", 1, 1).SetTrans(Tween.TransitionType.Quad);
					tween.Play();
				}

				double inverse = Mathf.InverseLerp(CurrentAttempt.Replays[i].Frames[CurrentAttempt.Replays[i].FrameIndex].Progress, CurrentAttempt.Replays[i].Frames[next].Progress, CurrentAttempt.Progress);
				Vector2 cursorPos = CurrentAttempt.Replays[i].Frames[CurrentAttempt.Replays[i].FrameIndex].CursorPosition.Lerp(CurrentAttempt.Replays[i].Frames[next].CursorPosition, (float)Math.Clamp(inverse, 0, 1));
				
				try
				{
					Cursors[i].Position = new(cursorPos.X, cursorPos.Y, 0);
				}
				catch {}	// dnc

				CurrentAttempt.Replays[i].CurrentPosition = cursorPos;
				positionSum += cursorPos;
			}

			Vector2 averagePosition = positionSum / CurrentAttempt.Replays.Length;
			Vector2 mouseDelta = averagePosition - CurrentAttempt.CursorPosition;

			if (CurrentAttempt.Mods["Spin"])
			{
				mouseDelta *= new Vector2(1, -1) / (float)CurrentAttempt.Replays[0].Sensitivity * 106;	// idk lol
			}

			UpdateCursor(mouseDelta);

			CurrentAttempt.CursorPosition = averagePosition;

			if (CurrentAttempt.Replays.Length == 1 && CurrentAttempt.Replays[0].SkipIndex < CurrentAttempt.Replays[0].Skips.Length && CurrentAttempt.Progress >= CurrentAttempt.Replays[0].Skips[CurrentAttempt.Replays[0].SkipIndex])
			{
				CurrentAttempt.Replays[0].SkipIndex++;
				Skip();
			}

			int complete = 0;

			foreach (Replay replay in CurrentAttempt.Replays)
			{
				if (replay.Complete)
				{
					complete++;
				}
			}

			if (complete == CurrentAttempt.Replays.Length)
			{
				QueueStop();
			}
		}
		else if (!CurrentAttempt.Stopped && Phoenyx.Settings.RecordReplays && !CurrentAttempt.Map.Ephemeral && now - CurrentAttempt.LastReplayFrame >= 1000000/60)	// 60hz
		{
			if (CurrentAttempt.ReplayFrames.Count == 0 || (CurrentAttempt.ReplayFrames[^1][1 .. 2] != new float[]{CurrentAttempt.CursorPosition.X, CurrentAttempt.CursorPosition.Y}))
			{
				CurrentAttempt.LastReplayFrame = now;
				CurrentAttempt.ReplayFrames.Add([
					(float)CurrentAttempt.Progress,
					CurrentAttempt.CursorPosition.X,
					CurrentAttempt.CursorPosition.Y
				]);
			}
		}
		
		CurrentAttempt.Progress += delta * 1000 * CurrentAttempt.Speed;
		CurrentAttempt.Skippable = false;

		if (CurrentAttempt.Map.AudioBuffer != null)
		{
			if (CurrentAttempt.Progress >= MapLength - Phoenyx.Constants.HitWindow)
			{
				if (SoundManager.Song.Playing)
				{
					SoundManager.Song.Stop();
				}
			}
			else if (!SoundManager.Song.Playing && CurrentAttempt.Progress >= 0)
			{
				SoundManager.Song.Play();
				SoundManager.Song.Seek((float)CurrentAttempt.Progress / 1000);
			}
		}

		if (CurrentAttempt.Map.VideoBuffer != null)
		{
			if (Phoenyx.Settings.VideoDim < 100 && !Video.IsPlaying() && CurrentAttempt.Progress >= 0)
			{
				Video.Play();
				
				Tween videoInTween = VideoQuad.CreateTween();
				videoInTween.TweenProperty(VideoQuad, "transparency", (float)Phoenyx.Settings.VideoDim / 100, 0.5);
				videoInTween.Play();
			}
		}
		
		int nextNoteMillisecond = CurrentAttempt.PassedNotes >= CurrentAttempt.Map.Notes.Length ? (int)MapLength + Phoenyx.Constants.BreakTime : CurrentAttempt.Map.Notes[CurrentAttempt.PassedNotes].Millisecond;
		
		if (nextNoteMillisecond - CurrentAttempt.Progress >= Phoenyx.Constants.BreakTime * CurrentAttempt.Speed)
		{
			int lastNoteMillisecond = CurrentAttempt.PassedNotes > 0 ? CurrentAttempt.Map.Notes[CurrentAttempt.PassedNotes - 1].Millisecond : 0;
			int skipWindow = nextNoteMillisecond - Phoenyx.Constants.BreakTime - lastNoteMillisecond;
			
			if (skipWindow >= 1000 * CurrentAttempt.Speed) // only allow skipping if i'm gonna allow it for at least 1 second
			{
				CurrentAttempt.Skippable = true;
			}
		}

		ToProcess = 0;
		ProcessNotes.Clear();

		// note process check
		double at = CurrentAttempt.IsReplay ? CurrentAttempt.Replays[0].ApproachTime : Phoenyx.Settings.ApproachTime;

		for (uint i = CurrentAttempt.PassedNotes; i < CurrentAttempt.Map.Notes.Length; i++)
		{
			Note note = CurrentAttempt.Map.Notes[i];

			if (note.Millisecond < CurrentAttempt.StartFrom)
			{
				continue;
			}

			if (note.Millisecond + Phoenyx.Constants.HitWindow * CurrentAttempt.Speed < CurrentAttempt.Progress)	// past hit window
			{
				if (i + 1 > CurrentAttempt.PassedNotes)
				{
					if (CurrentAttempt.IsReplay && CurrentAttempt.Replays.Length <= 1 && CurrentAttempt.Replays[0].Notes[note.Index] == -1 || !CurrentAttempt.IsReplay && !note.Hit)
					{
						CurrentAttempt.Miss(note.Index);
					}

					CurrentAttempt.PassedNotes = i + 1;
				}

				if (!CurrentAttempt.IsReplay)
				{
					continue;
				}
			}
			else if (note.Millisecond > CurrentAttempt.Progress + at * 1000 * CurrentAttempt.Speed)	// past approach distance
			{
				break;
			}
			else if (note.Hit)	// no point
			{
				continue;
			}

			if (Phoenyx.Settings.AlwaysPlayHitSound && !CurrentAttempt.Map.Notes[i].Hittable && note.Millisecond < CurrentAttempt.Progress)
			{
				CurrentAttempt.Map.Notes[i].Hittable = true;
				
				SoundManager.HitSound.Play();
			}
			
			ToProcess++;
			ProcessNotes.Add(note);
		}

		// hitreg check
		for (int i = 0; i < ToProcess; i++)
		{
			Note note = ProcessNotes[i];

			if (note.Hit)
			{
				continue;
			}

			if (!CurrentAttempt.IsReplay)
			{
				if (note.Millisecond - CurrentAttempt.Progress > 0)
				{
					continue;
				}
				else if (CurrentAttempt.CursorPosition.X + Phoenyx.Constants.HitBoxSize >= note.X - 0.5f && CurrentAttempt.CursorPosition.X - Phoenyx.Constants.HitBoxSize <= note.X + 0.5f && CurrentAttempt.CursorPosition.Y + Phoenyx.Constants.HitBoxSize >= note.Y - 0.5f && CurrentAttempt.CursorPosition.Y - Phoenyx.Constants.HitBoxSize <= note.Y + 0.5f)
				{
					CurrentAttempt.Hit(note.Index);
				}
			}
			else if (CurrentAttempt.Replays.Length > 1 && note.Millisecond - CurrentAttempt.Progress <= 0 || CurrentAttempt.Replays[0].Notes[note.Index] != -1 && note.Millisecond - CurrentAttempt.Progress + CurrentAttempt.Replays[0].Notes[note.Index] * CurrentAttempt.Speed <= 0)
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
			ProgressLabel.Modulate = Color.Color8(255, 255, 255, (byte)(96 + (int)(140 * (Math.Sin(Math.PI * now / 750000) / 2 + 0.5))));
		}
		else
		{
			TargetSkipLabelAlpha = 0;
			ProgressLabel.Modulate = Color.Color8(255, 255, 255, 96);
		}

		ProgressLabel.Text = $"{Lib.String.FormatTime(Math.Max(0, CurrentAttempt.Progress) / 1000)} / {Lib.String.FormatTime(MapLength / 1000)}";
		HealthTexture.Size = HealthTexture.Size.Lerp(new Vector2(32 + (float)CurrentAttempt.Health * 10.24f, 80), Math.Min(1, (float)delta * 64));
		ProgressBarTexture.Size = new Vector2(32 + (float)(CurrentAttempt.Progress / MapLength) * 1024, 80);
		SkipLabel.Modulate = Color.Color8(255, 255, 255, (byte)(SkipLabelAlpha * 255));

		if (StopQueued)
		{
			StopQueued = false;
			Stop();
			return;
		}

		// trail stuff
		if (Phoenyx.Settings.CursorTrail)
		{
			List<Dictionary<string, object>> culledList = [];

			LastCursorPositions.Add(new(){
				["Time"] = now,
				["Position"] = CurrentAttempt.CursorPosition
			});

			foreach (Dictionary<string, object> entry in LastCursorPositions)
			{
				if (now - (ulong)entry["Time"] >= (Phoenyx.Settings.TrailTime * 1000000))
				{
					continue;
				}
				
				if (CurrentAttempt.CursorPosition.DistanceTo((Vector2)entry["Position"]) == 0)
				{
					continue;
				}
				
				culledList.Add(entry);
			}
			
			int count = culledList.Count;
			float size = ((Vector2)Cursor.Mesh.Get("size")).X;
			Transform3D transform = new Transform3D(new Vector3(size, 0, 0), new Vector3(0, size, 0), new Vector3(0, 0, size), Vector3.Zero);
			int j = 0;
			
			CursorTrailMultimesh.Multimesh.InstanceCount = count;
			
			foreach (Dictionary<string, object> entry in culledList)
			{
				ulong difference = now - (ulong)entry["Time"];
				uint alpha = (uint)(difference / (Phoenyx.Settings.TrailTime * 1000000) * 255);
				
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
		if (@event is InputEventMouseMotion eventMouseMotion && Playing && !CurrentAttempt.IsReplay)
		{
			UpdateCursor(eventMouseMotion.Relative);
			
			CurrentAttempt.DistanceMM += eventMouseMotion.Relative.Length() / Phoenyx.Settings.Sensitivity / 57.5;
		}
		else if (@event is InputEventKey eventKey && eventKey.Pressed)
		{
			switch (eventKey.PhysicalKeycode)
			{
				case Key.Escape:
					CurrentAttempt.Qualifies = false;
					
					if (SettingsManager.Shown)
					{
						SettingsManager.HideSettings();
					}
					else
					{
						ShowMenu(!MenuShown);	
					}
					break;
				case Key.Quoteleft:
					Restart();
					break;
				case Key.F1:
					if (CurrentAttempt.IsReplay)
					{
						ShowReplayViewer(!ReplayViewerShown);
					}
					break;
				case Key.Space:
					if (CurrentAttempt.IsReplay)
					{
						Playing = !Playing;
						SoundManager.Song.PitchScale = Playing ? (float)CurrentAttempt.Speed : 0.00000000000001f;	// ooohh my goood
						ReplayViewerPause.TextureNormal = GD.Load<Texture2D>(Playing ? "res://textures/pause.png" : "res://textures/play.png");
					}
					else
					{
						if (Lobby.PlayerCount > 1)
						{
							break;
						}
						
						Skip();
					}
					break;
				case Key.F:
					Phoenyx.Settings.FadeOut = !Phoenyx.Settings.FadeOut;
					break;
				case Key.P:
					Phoenyx.Settings.Pushback = !Phoenyx.Settings.Pushback;
					break;
			}
		}
		else if (@event is InputEventMouseButton eventMouseButton)
		{
			switch (eventMouseButton.ButtonIndex)
			{
				case MouseButton.Left:
					LeftMouseButtonDown = eventMouseButton.Pressed;
					break;
			}
		}
	}
	
	public static void Play(Map map, double speed = 1, double startFrom = 0, Dictionary<string, bool> mods = null, string[] players = null, Replay[] replays = null)
	{
		CurrentAttempt = new(map, speed, startFrom, mods ?? [], players, replays);
		Playing = true;
		StopQueued = false;
		Started = Time.GetTicksUsec();
		ProcessNotes = [];
		
		if (!CurrentAttempt.IsReplay)
		{
			Phoenyx.Stats.Attempts++;
			
			if (!Phoenyx.Stats.FavouriteMaps.ContainsKey(map.ID))
			{
				Phoenyx.Stats.FavouriteMaps[map.ID] = 1;
			}
			else
			{
				Phoenyx.Stats.FavouriteMaps[map.ID]++;
			}
		}
	}
	
	public static void Restart()
	{
		CurrentAttempt.Alive = false;
		CurrentAttempt.Qualifies = false;
		Stop(false);
		Node3D.GetTree().ReloadCurrentScene();
		Play(MapParser.Decode(CurrentAttempt.Map.FilePath), CurrentAttempt.Speed, CurrentAttempt.StartFrom, CurrentAttempt.Mods, CurrentAttempt.Players, CurrentAttempt.Replays);
	}
	
	public static void Skip()
	{
		if (CurrentAttempt.Skippable)
		{
			CurrentAttempt.ReplaySkips.Add((float)CurrentAttempt.Progress);
			
			if (CurrentAttempt.PassedNotes >= CurrentAttempt.Map.Notes.Length)
			{
				CurrentAttempt.Progress = SoundManager.Song.Stream.GetLength() * 1000;
			}
			else
			{
				CurrentAttempt.Progress = CurrentAttempt.Map.Notes[CurrentAttempt.PassedNotes].Millisecond - Phoenyx.Settings.ApproachTime * 1500 * CurrentAttempt.Speed; // turn AT to ms and multiply by 1.5x
				
				Phoenyx.Util.DiscordRPC.Call("Set", "end_timestamp", Time.GetUnixTimeFromSystem() + (CurrentAttempt.Map.Length - CurrentAttempt.Progress) / 1000 / CurrentAttempt.Speed);
				
				if (CurrentAttempt.Map.AudioBuffer != null)
				{
					if (!SoundManager.Song.Playing)
					{
						SoundManager.Song.Play();
					}
					
					SoundManager.Song.Seek((float)CurrentAttempt.Progress / 1000);
					Video.StreamPosition = (float)CurrentAttempt.Progress / 1000;
				}
			}
		}
	}
	
	public static void QueueStop()
	{
		if (!Playing)
		{
			return;
		}
		
		Playing = false;
		StopQueued = true;
	}
	
	public static void Stop(bool results = true)
	{
		if (CurrentAttempt.Stopped)
		{
			return;
		}
		
		CurrentAttempt.Stop();
		
		if (!CurrentAttempt.IsReplay)
		{
			Phoenyx.Stats.GamePlaytime += (Time.GetTicksUsec() - Started) / 1000000;
			Phoenyx.Stats.TotalDistance += (ulong)CurrentAttempt.DistanceMM;
				
			if (CurrentAttempt.StartFrom == 0)
			{
				if (!File.Exists($"{Phoenyx.Constants.UserFolder}/pbs/{CurrentAttempt.Map.ID}"))
				{
					List<byte> bytes = [0, 0, 0, 0];
					bytes.AddRange(SHA256.HashData([0, 0, 0, 0]));
					File.WriteAllBytes($"{Phoenyx.Constants.UserFolder}/pbs/{CurrentAttempt.Map.ID}", [.. bytes]);
				}
				
				Leaderboard leaderboard = new(CurrentAttempt.Map.ID, $"{Phoenyx.Constants.UserFolder}/pbs/{CurrentAttempt.Map.ID}");
				
				leaderboard.Add(new(CurrentAttempt.ID, "You", CurrentAttempt.Qualifies, CurrentAttempt.Score, CurrentAttempt.Accuracy, Time.GetUnixTimeFromSystem(), CurrentAttempt.DeathTime, CurrentAttempt.Map.Length, CurrentAttempt.Speed, CurrentAttempt.Mods));
				leaderboard.Save();
				
				if (CurrentAttempt.Qualifies)
				{
					Phoenyx.Stats.Passes++;
					Phoenyx.Stats.TotalScore += CurrentAttempt.Score;
					
					if (CurrentAttempt.Accuracy == 100)
					{
						Phoenyx.Stats.FullCombos++;
					}
					
					if (CurrentAttempt.Score > Phoenyx.Stats.HighestScore)
					{
						Phoenyx.Stats.HighestScore = CurrentAttempt.Score;
					}
					
					Phoenyx.Stats.PassAccuracies.Add(CurrentAttempt.Accuracy);
				}
			}
		}
		
		if (results)
		{
			SceneManager.Load("res://scenes/results.tscn");
		}
	}
	
	public static void ShowMenu(bool show = true)
	{
		MenuShown = show;
		Playing = !MenuShown;
		SoundManager.Song.PitchScale = Playing ? (float)CurrentAttempt.Speed : 0.00000000000001f;	// not again
		Input.MouseMode = MenuShown ? Input.MouseModeEnum.Visible : (Phoenyx.Settings.AbsoluteInput || CurrentAttempt.IsReplay ? Input.MouseModeEnum.ConfinedHidden : Input.MouseModeEnum.Captured);
		
		if (MenuShown)
		{
			Menu.Visible = true;
			Input.WarpMouse(Node3D.GetViewport().GetWindow().Size / 2);
		}
		
		Tween tween = Menu.CreateTween();
		tween.TweenProperty(Menu, "modulate", Color.Color8(255, 255, 255, (byte)(MenuShown ? 255 : 0)), 0.25).SetTrans(Tween.TransitionType.Quad);
		tween.TweenCallback(Callable.From(() => {
			Menu.Visible = MenuShown;
		}));
		tween.Play();
	}
	
	public static void HideMenu()
	{
		ShowMenu(false);
	}
	
	public static void ShowReplayViewer(bool show = true)
	{
		ReplayViewerShown = CurrentAttempt.IsReplay && show;
		ReplayViewer.Visible = ReplayViewerShown;
		
		Input.MouseMode = ReplayViewerShown ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Hidden;
	}
	
	public static void HideReplayViewer()
	{
		ShowReplayViewer(false);
	}
	
	public static void UpdateVolume()
	{
		SoundManager.Song.VolumeDb = -80 + 70 * (float)Math.Pow(Phoenyx.Settings.VolumeMusic / 100, 0.1) * (float)Math.Pow(Phoenyx.Settings.VolumeMaster / 100, 0.1);
		SoundManager.HitSound.VolumeDb = -80 + 80 * (float)Math.Pow(Phoenyx.Settings.VolumeSFX / 100, 0.1) * (float)Math.Pow(Phoenyx.Settings.VolumeMaster / 100, 0.1);
		SoundManager.FailSound.VolumeDb = -80 + 80 * (float)Math.Pow(Phoenyx.Settings.VolumeSFX / 100, 0.1) * (float)Math.Pow(Phoenyx.Settings.VolumeMaster / 100, 0.1);
	}
	
	public static void UpdateCursor(Vector2 mouseDelta)
	{
		float sensitivity = (float)(CurrentAttempt.IsReplay ? CurrentAttempt.Replays[0].Sensitivity : Phoenyx.Settings.Sensitivity);
		sensitivity *= (float)Phoenyx.Settings.FoV / 70f;
		
		if (!CurrentAttempt.Mods["Spin"])
		{
			if (Phoenyx.Settings.CursorDrift)
			{
				CurrentAttempt.CursorPosition = (CurrentAttempt.CursorPosition + new Vector2(1, -1) * mouseDelta / 120 * sensitivity).Clamp(-Phoenyx.Constants.Bounds, Phoenyx.Constants.Bounds);
			}
			else
			{
				CurrentAttempt.RawCursorPosition += new Vector2(1, -1) * mouseDelta / 120 * sensitivity;
				CurrentAttempt.CursorPosition = CurrentAttempt.RawCursorPosition.Clamp(-Phoenyx.Constants.Bounds, Phoenyx.Constants.Bounds);
			}
			
			Cursor.Position = new Vector3(CurrentAttempt.CursorPosition.X, CurrentAttempt.CursorPosition.Y, 0);
			Camera.Position = new Vector3(0, 0, 3.75f) + new Vector3(CurrentAttempt.CursorPosition.X, CurrentAttempt.CursorPosition.Y, 0) * (float)(CurrentAttempt.IsReplay ? CurrentAttempt.Replays[0].Parallax : Phoenyx.Settings.Parallax);
			Camera.Rotation = Vector3.Zero;
			
			VideoQuad.Position = new Vector3(Camera.Position.X, Camera.Position.Y, -100);
		}
		else
		{
			Camera.Rotation += new Vector3(-mouseDelta.Y / 120 * sensitivity / (float)Math.PI, -mouseDelta.X / 120 * sensitivity / (float)Math.PI, 0);
			Camera.Rotation = new Vector3((float)Math.Clamp(Camera.Rotation.X, Mathf.DegToRad(-90), Mathf.DegToRad(90)), Camera.Rotation.Y, Camera.Rotation.Z);
			Camera.Position = new Vector3(CurrentAttempt.CursorPosition.X * 0.25f, CurrentAttempt.CursorPosition.Y * 0.25f, 3.5f) + Camera.Basis.Z / 4;
			
			float wtf = 0.95f;
			float hypotenuse = (wtf + Camera.Position.Z) / Camera.Basis.Z.Z;
			float distance = (float)Math.Sqrt(Math.Pow(hypotenuse, 2) - Math.Pow(wtf + Camera.Position.Z, 2));
			
			CurrentAttempt.RawCursorPosition = new Vector2(Camera.Basis.Z.X, Camera.Basis.Z.Y).Normalized() * -distance;
			CurrentAttempt.CursorPosition = CurrentAttempt.RawCursorPosition.Clamp(-Phoenyx.Constants.Bounds, Phoenyx.Constants.Bounds);
			Cursor.Position = new Vector3(CurrentAttempt.CursorPosition.X, CurrentAttempt.CursorPosition.Y, 0);
			
			VideoQuad.Position = Camera.Position - Camera.Basis.Z * 103.75f;
			VideoQuad.Rotation = Camera.Rotation;
		}
	}
	
	public static void UpdateScore(string player, int score)
	{
		//ColorRect playerScore = Leaderboard.GetNode("SubViewport").GetNode("Players").GetNode<ColorRect>(player);
		//Label scoreLabel = playerScore.GetNode<Label>("Score");
		//playerScore.Position = new Vector2(playerScore.Position.X, score);
		//scoreLabel.Text = score.ToString();
	}
}
