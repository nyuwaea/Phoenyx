using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;
using Menu;

public partial class Game : Node3D
{
	public static Node3D Node3D;

	Camera3D Camera;
	MeshInstance3D Cursor;
	MeshInstance3D Grid;
	MeshInstance3D Health;
	MultiMeshInstance3D NotesMultimesh;
	Label3D SongTitle;
	Label3D Hits;
	Label3D Accuracy;
	Label3D Combo;
	Label3D SpeedLabel;
	Label FPSCounter;
	AudioStreamPlayer Audio;
	double LastFrame = Time.GetTicksUsec(); // delta arg unreliable..
	public static bool StopQueued = false;

	public static bool Playing = false;
	public static int ToProcess = 0;
	public static List<Note> ProcessNotes;
	public static Attempt CurrentAttempt;

	public struct Attempt
	{
		public double Progress = 0;	// ms
		public Map Map = new Map();
		public float Speed = 1;
		public string[] Mods = Array.Empty<string>();
		public int Hits = 0;
		public int Misses = 0;
		public int Combo = 0;
		public int PassedNotes = 0;
		public float Accuracy = 100;
		public float Health = 100;
		public float HealthStep = 15;
		public Vector2 CursorPosition = Vector2.Zero;

		public Attempt(Map map, float speed, string[] mods)
		{
			Map = map;
			Speed = speed;
			Mods = mods;
			Progress = -1000;
		}

		public void Hit(int index)
		{
			Hits++;
			Combo++;
			HealthStep = Math.Max(HealthStep / 1.45f, 15);
			Health = Math.Min(100, Health + HealthStep / 1.75f);
			Map.Notes[index].Hit = true;
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

		Camera = GetNode<Camera3D>("Camera3D");
		Cursor = GetNode<MeshInstance3D>("Cursor");
		Grid = GetNode<MeshInstance3D>("Grid");
		Health = GetNode<MeshInstance3D>("Health");
		NotesMultimesh = GetNode<MultiMeshInstance3D>("Notes");
		FPSCounter = GetNode<Label>("FPSCounter");
		Audio = Node3D.GetNode<AudioStreamPlayer>("AudioStreamPlayer");
		SongTitle = Node3D.GetNode<Label3D>("SongTitle");
		Hits = Node3D.GetNode<Label3D>("Hits");
		Accuracy = Node3D.GetNode<Label3D>("Accuracy");
		Combo = Node3D.GetNode<Label3D>("Combo");
		SpeedLabel = Node3D.GetNode<Label3D>("Speed");

		SongTitle.Text = CurrentAttempt.Map.PrettyTitle;

		Input.MouseMode = Input.MouseModeEnum.Captured;
		Input.UseAccumulatedInput = false;
		//DisplayServer.WindowSetMode(DisplayServer.WindowMode.ExclusiveFullscreen);
		DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);

		try
		{
			(Cursor.GetActiveMaterial(0) as StandardMaterial3D).AlbedoTexture = ImageTexture.CreateFromImage(Image.LoadFromFile($"{Phoenix.Constants.UserFolder}/skins/{Phoenix.Settings.Skin}/cursor.png"));
			(Grid.GetActiveMaterial(0) as StandardMaterial3D).AlbedoTexture = ImageTexture.CreateFromImage(Image.LoadFromFile($"{Phoenix.Constants.UserFolder}/skins/{Phoenix.Settings.Skin}/grid.png"));
			//NotesMultimesh.Multimesh.Mesh = 
			
		} catch (Exception exception)
		{
			MainMenu.Notify("Could not load skin", 2);
			throw Logger.Error($"Could not load skin; {exception.Message}");
		}

		if (CurrentAttempt.Map.AudioBuffer != null)
		{
			AudioStream stream;

			if (Encoding.UTF8.GetString(CurrentAttempt.Map.AudioBuffer[0..4]) == "OggS")
			{
				stream = AudioStreamOggVorbis.LoadFromBuffer(CurrentAttempt.Map.AudioBuffer);
			}
			else
			{
				stream = new AudioStreamMP3(){Data = CurrentAttempt.Map.AudioBuffer};
			}
			
			Audio.Stream = stream;
			Audio.PitchScale = CurrentAttempt.Speed;
		}
	}

	public override void _Process(double delta)
	{
		double Delta = (Time.GetTicksUsec() - LastFrame) / 1000000;	// more reliable
		LastFrame = Time.GetTicksUsec();
		
		FPSCounter.Text = $"{Mathf.Floor(1 / Delta)} FPS";

		if (!Playing)
		{
			return;
		}
		
		CurrentAttempt.Progress += Delta * 1000 * CurrentAttempt.Speed;

		if (CurrentAttempt.Progress >= Audio.GetPlaybackPosition() * 1000 + 200)
		{
			Audio.Stop();
		}
		else if (!Audio.Playing && CurrentAttempt.Progress >= 0)
		{
			Audio.Play();
		}
		
		if (CurrentAttempt.Progress >= CurrentAttempt.Map.Length + 2000)
		{
			Stop();
			return;
		}

		ToProcess = 0;
		ProcessNotes = new List<Note>();

		for (int i = CurrentAttempt.PassedNotes; i < CurrentAttempt.Map.Notes.Length; i++)	// note process check
		{
			Note note = CurrentAttempt.Map.Notes[i];

			if (note.Millisecond + Phoenix.Constants.HitWindow * CurrentAttempt.Speed < CurrentAttempt.Progress)	// past hit window
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
			else if (note.Millisecond > CurrentAttempt.Progress + Phoenix.Settings.ApproachTime * 1000 * CurrentAttempt.Speed)	// past approach distance
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

		for (int i = 0; i < ToProcess; i++)	// hitreg check
		{
			Note note = ProcessNotes[i];

			if (note.Hit || note.Millisecond - CurrentAttempt.Progress > 0)
			{
				continue;
			}

			if (CurrentAttempt.CursorPosition.X + Phoenix.Constants.HitBoxSize >= note.X - 0.5f && CurrentAttempt.CursorPosition.X - Phoenix.Constants.HitBoxSize <= note.X + 0.5f && CurrentAttempt.CursorPosition.Y + Phoenix.Constants.HitBoxSize >= note.Y - 0.5f && CurrentAttempt.CursorPosition.Y - Phoenix.Constants.HitBoxSize <= note.Y + 0.5f)
			{
				CurrentAttempt.Hit(note.Index);
			}
		}

		int sum = CurrentAttempt.Hits + CurrentAttempt.Misses;
		string accuracy = (Math.Floor((float)CurrentAttempt.Hits / sum * 10000) / 100).ToString().PadDecimals(2);

		Hits.Text = $"{CurrentAttempt.Hits} / {sum}";
		Accuracy.Text = $"{(CurrentAttempt.Hits + CurrentAttempt.Misses == 0 ? "100.00" : accuracy)}%";
		Health.Mesh.Set("size", new Vector2(CurrentAttempt.Health / 10, 1));
		Health.Mesh.Set("center_offset", new Vector3(-(100 - CurrentAttempt.Health) / 20, 0, 0));
		Combo.Text = CurrentAttempt.Combo.ToString();
		SpeedLabel.Text = $"{CurrentAttempt.Speed.ToString().PadDecimals(2)}x";

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
			CurrentAttempt.CursorPosition += new Vector2(1, -1) * eventMouseMotion.Relative / 100 * Phoenix.Settings.Sensitivity;
			CurrentAttempt.CursorPosition = CurrentAttempt.CursorPosition.Clamp(-Phoenix.Constants.Bounds, Phoenix.Constants.Bounds);

			Cursor.Translate(new Vector3(CurrentAttempt.CursorPosition.X, CurrentAttempt.CursorPosition.Y, 0) - Cursor.Transform.Origin);
			Camera.Translate(new Vector3(0, 0, 3.75f) + new Vector3(CurrentAttempt.CursorPosition.X, CurrentAttempt.CursorPosition.Y, 0) * Phoenix.Settings.Parallax - Camera.Transform.Origin);
		}
		else if (@event is InputEventKey eventKey && eventKey.Pressed)
		{
			if (eventKey.Keycode == Key.Escape)
			{
				Stop();
			}
			else if (eventKey.Keycode == Key.F)
			{
				Phoenix.Settings.FadeOut = !Phoenix.Settings.FadeOut;
			}
			else if (eventKey.Keycode == Key.P)
			{
				Phoenix.Settings.Pushback = !Phoenix.Settings.Pushback;
			}
			else if (eventKey.Keycode == Key.Equal)
			{
				CurrentAttempt.Speed += 0.05f;
				Audio.PitchScale = CurrentAttempt.Speed;
			}
			else if (eventKey.Keycode == Key.Minus)
			{
				CurrentAttempt.Speed = Math.Max(0.05f, CurrentAttempt.Speed - 0.05f);
				Audio.PitchScale = CurrentAttempt.Speed;
			}
		}
	}

	public static void Play(Map map, float speed = 1, string[] mods = null)
	{
		CurrentAttempt = new Attempt(map, speed, mods);
		Playing = true;
		ProcessNotes = null;
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

		Node3D.GetTree().ChangeSceneToFile("res://scenes/main_menu.tscn");
	}
}