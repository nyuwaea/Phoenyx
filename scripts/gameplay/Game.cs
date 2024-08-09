using System;
using System.Collections.Generic;
using Godot;

public partial class Game : Node3D
{
	public static Node3D Node3D;

	Camera3D Camera;
	MeshInstance3D Cursor;
	Label3D SongTitle;
	Label3D Hits;
	Label3D Accuracy;
	Label FPSCounter;
	AudioStreamPlayer Audio;
	double LastFrame = Time.GetTicksUsec(); // delta arg unreliable..

	public static bool Playing = false;
	public static int ToProcess = 0;
	public static List<Note> Notes;		// notes to process
	public static Attempt CurrentAttempt;

	public struct Attempt
	{
		public double Progress = 0;	// ms
		public Map Map = new Map();
		public float Speed = 1;
		public string[] Mods = Array.Empty<string>();
		public int Hits = 0;
		public int Misses = 0;
		public int PassedNotes = 0;
		public float Accuracy = 100;
		public float Health = 100;
		public Vector2 CursorPosition = Vector2.Zero;

		public Attempt(Map map, float speed, string[] mods) {
			Map = map;
			Speed = speed;
			Mods = mods;
			Progress = -1000;
			Hits = 0;
			Misses = 0;
			PassedNotes = 0;
			Accuracy = 100;
			Health = 100;
			CursorPosition = Vector2.Zero;
		}
	}
	
	public override void _Ready()
	{
		Node3D = this;

		Camera = GetNode<Camera3D>("Camera3D");
		Cursor = GetNode<MeshInstance3D>("Cursor");
		FPSCounter = GetNode<Label>("FPSCounter");
		Audio = Node3D.GetNode<AudioStreamPlayer>("AudioStreamPlayer");
		SongTitle = Node3D.GetNode<Label3D>("SongTitle");
		Hits = Node3D.GetNode<Label3D>("Hits");
		Accuracy = Node3D.GetNode<Label3D>("Accuracy");

		SongTitle.Text = CurrentAttempt.Map.PrettyTitle;

		Input.MouseMode = Input.MouseModeEnum.Captured;
		Input.UseAccumulatedInput = false;
		DisplayServer.WindowSetMode(DisplayServer.WindowMode.ExclusiveFullscreen);
		DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);

		if (CurrentAttempt.Map.AudioBuffer != null)
		{
			Audio.Stream = new AudioStreamMP3(){Data = CurrentAttempt.Map.AudioBuffer};
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

		CurrentAttempt.Progress += Delta * 1000;

		if (!Audio.Playing && CurrentAttempt.Progress >= 0)
		{
			Audio.Play();
		}

		if (CurrentAttempt.Progress >= CurrentAttempt.Map.Length + 1000)
		{
			Stop();
			return;
		}

		ToProcess = 0;
		Notes = new List<Note>();

		for (int i = CurrentAttempt.PassedNotes; i < CurrentAttempt.Map.Notes.Length; i++)	// note process check
		{
			Note note = CurrentAttempt.Map.Notes[i];

			if (note.Millisecond + Phoenix.Constants.HitWindow < CurrentAttempt.Progress)	// past hit window
			{
				if (i + 1 > CurrentAttempt.PassedNotes)
				{
					if (!note.Hit)
					{
						CurrentAttempt.Misses++;
					}

					CurrentAttempt.PassedNotes = i + 1;
				}

				continue;
			} else if (note.Millisecond > CurrentAttempt.Progress + Phoenix.Settings.ApproachTime * 1000)	// past approach distance
			{
				break;
			} else if (note.Hit)	// no point
			{
				continue;
			}
			
			ToProcess++;
			Notes.Add(note);
		}

		for (int i = 0; i < ToProcess; i++)	// hitreg check
		{
			Note note = Notes[i];

			if (note.Hit || note.Millisecond - CurrentAttempt.Progress > 0)
			{
				continue;
			}

			if (CurrentAttempt.CursorPosition.X + Phoenix.Constants.CursorSize / 2 >= note.X - 0.5f && CurrentAttempt.CursorPosition.X - Phoenix.Constants.CursorSize / 2 <= note.X + 0.5f && CurrentAttempt.CursorPosition.Y + Phoenix.Constants.CursorSize / 2 >= note.Y - 0.5f && CurrentAttempt.CursorPosition.Y - Phoenix.Constants.CursorSize / 2 <= note.Y + 0.5f)
			{
				CurrentAttempt.Hits++;
				CurrentAttempt.Map.Notes[note.Index].Hit = true;
			}
		}

		int sum = CurrentAttempt.Hits + CurrentAttempt.Misses;
		string accuracy = (Math.Floor((float)CurrentAttempt.Hits / (float)sum * 10000) / 100).ToString().PadDecimals(2);

		Hits.Text = $"Notes\n{CurrentAttempt.Hits} / {sum}";
		Accuracy.Text = $"Accuracy\n{(CurrentAttempt.Hits + CurrentAttempt.Misses == 0 ? "100.00" : accuracy)}%";
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
		}
	}

	public static void Play(Map map, float speed = 1, string[] mods = null)
	{
		CurrentAttempt = new Attempt(map, speed, mods);

		Playing = true;
		Notes = null;
	}

	public static void Stop()
	{
		Playing = false;
		Notes = null;

		Node3D.GetTree().ChangeSceneToFile("res://scenes/main_menu.tscn");
	}
}