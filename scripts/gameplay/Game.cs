using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;
using Phoenyx;

public partial class Game : Node3D
{
	private static Node3D Node3D;
	private static readonly PackedScene PlayerScore = GD.Load<PackedScene>("res://prefabs/player_score.tscn");

	private static Label FPSCounter;
	private static Camera3D Camera;
	private static Label3D Title;
	private static Label3D Hits;
	private static Label3D Accuracy;
	private static Label3D Combo;
	private static Label3D Speed;
	private static Label3D Progress;
	private static MeshInstance3D Cursor;
	private static MeshInstance3D Grid;
	private static MeshInstance3D Health;
	private static MeshInstance3D ProgressBar;
	private static MeshInstance3D Leaderboard;
	private static AudioStreamPlayer Audio;
	private static AudioStreamPlayer HitSound;
	double LastFrame = Time.GetTicksUsec(); // delta arg unreliable..
	public static bool StopQueued = false;

	public static bool Playing = false;
	public static int ToProcess = 0;
	public static List<Note> ProcessNotes;
	public static Attempt CurrentAttempt;
	public static float MapLength;

	public struct Attempt
	{
		public double Progress = 0;	// ms
		public Map Map = new Map();
		public float Speed = 1;
		public string[] Mods = Array.Empty<string>();
		public string[] Players = Array.Empty<string>();
		public int Hits = 0;
		public int Misses = 0;
		public int Combo = 0;
		public int PassedNotes = 0;
		public float Accuracy = 100;
		public float Health = 100;
		public float HealthStep = 15;
		public Vector2 CursorPosition = Vector2.Zero;
		public bool Skippable = false;

		public Attempt(Map map, float speed, string[] mods, string[] players = null, bool replay = false)
		{
			Map = map;
			Speed = speed;
			Mods = mods;
			Players = players ?? Array.Empty<string>();
			Progress = -1000;
		}

		public void Hit(int index)
		{
			Hits++;
			Combo++;
			HealthStep = Math.Max(HealthStep / 1.45f, 15);
			Health = Math.Min(100, Health + HealthStep / 1.75f);
			Map.Notes[index].Hit = true;

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

			if (Health <= 0 && !CurrentAttempt.Mods.Contains("NoFail"))
			{
				QueueStop();
			}
		}
	}
	
	public override void _Ready()
	{
		Node3D = this;

		FPSCounter = GetNode<Label>("FPSCounter");
		Camera = GetNode<Camera3D>("Camera3D");
		Title = GetNode<Label3D>("Title");
		Hits = GetNode<Label3D>("Hits");
		Accuracy = GetNode<Label3D>("Accuracy");
		Combo = GetNode<Label3D>("Combo");
		Speed = GetNode<Label3D>("Speed");
		Progress = GetNode<Label3D>("Progress");
		Cursor = GetNode<MeshInstance3D>("Cursor");
		Grid = GetNode<MeshInstance3D>("Grid");
		Health = GetNode<MeshInstance3D>("Health");
		ProgressBar = GetNode<MeshInstance3D>("ProgressBar");
		Leaderboard = GetNode<MeshInstance3D>("Leaderboard");
		Audio = Node3D.GetNode<AudioStreamPlayer>("SongPlayer");
		HitSound = Node3D.GetNode<AudioStreamPlayer>("HitSoundPlayer");

		foreach (KeyValuePair<string, Player> entry in Lobby.Players)
		{
			ColorRect playerScore = PlayerScore.Instantiate<ColorRect>();
			playerScore.GetNode<Label>("Name").Text = entry.Key;
			playerScore.Name = entry.Key;
			Leaderboard.GetNode("SubViewport").GetNode("Players").AddChild(playerScore);
		}

		Camera.Fov = Settings.FoV;
		Title.Text = CurrentAttempt.Map.PrettyTitle;

		Input.MouseMode = Input.MouseModeEnum.Captured;
		Input.UseAccumulatedInput = false;
		DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);

		try
		{
			(Cursor.GetActiveMaterial(0) as StandardMaterial3D).AlbedoTexture = ImageTexture.CreateFromImage(Image.LoadFromFile($"{Constants.UserFolder}/skins/{Settings.Skin}/cursor.png"));
			(Grid.GetActiveMaterial(0) as StandardMaterial3D).AlbedoTexture = ImageTexture.CreateFromImage(Image.LoadFromFile($"{Constants.UserFolder}/skins/{Settings.Skin}/grid.png"));
			Health.GetNode<SubViewport>("SubViewport").GetNode<TextureRect>("Main").Texture = ImageTexture.CreateFromImage(Image.LoadFromFile($"{Constants.UserFolder}/skins/{Settings.Skin}/health.png"));
			Health.GetNode<SubViewport>("SubViewport").GetNode<TextureRect>("Background").Texture = ImageTexture.CreateFromImage(Image.LoadFromFile($"{Constants.UserFolder}/skins/{Settings.Skin}/health_background.png"));
			ProgressBar.GetNode<SubViewport>("SubViewport").GetNode<TextureRect>("Main").Texture = ImageTexture.CreateFromImage(Image.LoadFromFile($"{Constants.UserFolder}/skins/{Settings.Skin}/progress.png"));
			ProgressBar.GetNode<SubViewport>("SubViewport").GetNode<TextureRect>("Background").Texture = ImageTexture.CreateFromImage(Image.LoadFromFile($"{Constants.UserFolder}/skins/{Settings.Skin}/progress_background.png"));
			//NotesMultimesh.Multimesh.Mesh = 
			
		}
		catch (Exception exception)
		{
			ToastNotification.Notify("Could not load skin", 2);
			throw Logger.Error($"Could not load skin; {exception.Message}");
		}

		if (CurrentAttempt.Map.AudioBuffer != null)
		{
			Audio.Stream = LoadAudioStream(CurrentAttempt.Map.AudioBuffer);
			Audio.PitchScale = CurrentAttempt.Speed;
			MapLength = (float)Audio.Stream.GetLength() * 1000;
		}
		else
		{
			MapLength = CurrentAttempt.Map.Length + 1000;
		}

		MapLength += Constants.HitWindow;

		FileAccess hitSoundFile = FileAccess.Open($"{Constants.UserFolder}/skins/{Settings.Skin}/hit.mp3", FileAccess.ModeFlags.Read);
		HitSound.Stream = LoadAudioStream(hitSoundFile.GetBuffer((long)hitSoundFile.GetLength()));
		hitSoundFile.Close();

		UpdateVolume();
	}

	public override void _Process(double delta)
	{
		delta = (Time.GetTicksUsec() - LastFrame) / 1000000;	// more reliable
		LastFrame = Time.GetTicksUsec();
		
		FPSCounter.Text = $"{Mathf.Floor(1 / delta)} FPS";

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

		int sum = CurrentAttempt.Hits + CurrentAttempt.Misses;
		string accuracy = (Math.Floor((float)CurrentAttempt.Hits / sum * 10000) / 100).ToString().PadDecimals(2);

		Hits.Text = $"{CurrentAttempt.Hits} / {sum}";
		Accuracy.Text = $"{(CurrentAttempt.Hits + CurrentAttempt.Misses == 0 ? "100.00" : accuracy)}%";
		Combo.Text = CurrentAttempt.Combo.ToString();
		Speed.Text = $"{(Math.Round(CurrentAttempt.Speed * 100) / 100).ToString().PadDecimals(2)}x";
		Progress.Text = $"{FormatTime(Math.Max(0, CurrentAttempt.Progress) / 1000)} / {FormatTime(MapLength / 1000)}";
		Progress.Modulate = Color.FromHtml("ffffff" + (CurrentAttempt.Skippable ? "ff" : "40"));
		Health.GetNode<SubViewport>("SubViewport").GetNode<TextureRect>("Main").Size = new Vector2(CurrentAttempt.Health * 10.88f, 80);
		ProgressBar.GetNode<SubViewport>("SubViewport").GetNode<TextureRect>("Main").Size = new Vector2(1088 * (float)(CurrentAttempt.Progress / MapLength), 80);

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
				CurrentAttempt.CursorPosition += new Vector2(1, -1) * eventMouseMotion.Relative / 100 * Settings.Sensitivity;
				CurrentAttempt.CursorPosition = CurrentAttempt.CursorPosition.Clamp(-Constants.Bounds, Constants.Bounds);

				Cursor.GlobalPosition = new Vector3(CurrentAttempt.CursorPosition.X, CurrentAttempt.CursorPosition.Y, 0);
				Camera.GlobalPosition = new Vector3(0, 0, 3.75f) + new Vector3(CurrentAttempt.CursorPosition.X, CurrentAttempt.CursorPosition.Y, 0) * Settings.Parallax;
				Camera.GlobalRotation = Vector3.Zero;
			}
			else
			{
				Camera.GlobalRotation += new Vector3(-eventMouseMotion.Relative.Y / 100 * Settings.Sensitivity / (float)Math.PI / 2, -eventMouseMotion.Relative.X / 100 * Settings.Sensitivity / (float)Math.PI / 2, 0);
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
				{
					Stop();
					break;
				}
				case Key.Space:
				{
					if (Lobby.PlayerCount > 1)
					{
						break;
					}
					
					Skip();
					break;
				}
				case Key.F:
				{
					Settings.FadeOut = !Settings.FadeOut;
					break;
				}
				case Key.P:
				{
					Settings.Pushback = !Settings.Pushback;
					break;
				}
				case Key.C:
				{
					if (Lobby.PlayerCount > 1)
					{
						break;
					}

					Settings.CameraLock = !Settings.CameraLock;
					Settings.ApproachRate *= Settings.CameraLock ? 2.5f : 0.4f;
					Settings.ApproachDistance *= Settings.CameraLock ? 2.5f : 0.4f;
					break;
				}
				case Key.Equal:
				{
					if (Lobby.PlayerCount > 1)
					{
						break;
					}
					
					CurrentAttempt.Speed += 0.05f;
					Audio.PitchScale = CurrentAttempt.Speed;
					break;
				}
				case Key.Minus:
				{
					if (Lobby.PlayerCount > 1)
					{
						break;
					}
					
					CurrentAttempt.Speed = Math.Max(0.05f, CurrentAttempt.Speed - 0.05f);
					Audio.PitchScale = CurrentAttempt.Speed;
					break;
				}
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
					{
						Settings.VolumeMaster = Math.Min(1, Settings.VolumeMaster + 0.025f);
						break;
					}
					case MouseButton.WheelDown:
					{
						Settings.VolumeMaster = Math.Max(0, Settings.VolumeMaster - 0.025f);
						break;
					}
				}

				UpdateVolume();
			}
		}
    }

	public static void Play(Map map, float speed = 1, string[] mods = null, string[] players = null)
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
				CurrentAttempt.Progress = CurrentAttempt.Map.Notes[CurrentAttempt.PassedNotes].Millisecond - Settings.ApproachTime * 1250 * CurrentAttempt.Speed; // turn AT to ms and multiply by 1.25x
		
				if (!Audio.Playing)
				{
					Audio.Play();
				}
				
				Audio.Seek((float)CurrentAttempt.Progress / 1000);
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

		SceneManager.Load(Node3D.GetTree(), "res://scenes/main_menu.tscn");
	}

	private static void UpdateVolume()
	{
		Audio.VolumeDb = -80 + 70 * (float)Math.Pow(Settings.VolumeMusic, 0.1) * (float)Math.Pow(Settings.VolumeMaster, 0.5);
		HitSound.VolumeDb = -80 + 80 * (float)Math.Pow(Settings.VolumeSFX, 0.1) * (float)Math.Pow(Settings.VolumeMaster, 0.5);
	}

	public static void UpdateScore(string player, int score)
	{
		Label scoreLabel = Leaderboard.GetNode("SubViewport").GetNode("Players").GetNode(player).GetNode<Label>("Score");
		scoreLabel.Position = new Vector2(scoreLabel.Position.X, score);
		scoreLabel.Text = score.ToString();
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