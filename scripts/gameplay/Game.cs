using System.Collections.Generic;
using Godot;

public partial class Game : Node3D
{
	public static Node3D Node3D;

	Camera3D Camera;
	MeshInstance3D Cursor;
	Label FPSCounter;
	double LastFrame = Time.GetTicksUsec(); // delta arg unreliable..

	public static bool Playing = false;
	public static double Progress = 0;	// ms
	public static Map Map;
	public static List<Note> Notes;
	public static int PassedNotes = 0;
	public static int ToProcess = 0;
    public static Vector2 CursorPosition = new Vector2();
	
	public override void _Ready()
	{
		Node3D = this;

		Camera = GetNode<Camera3D>("Camera3D");
		Cursor = GetNode<MeshInstance3D>("Cursor");
		FPSCounter = GetNode<Label>("FPSCounter");

		Input.MouseMode = Input.MouseModeEnum.Captured;
		Input.UseAccumulatedInput = false;
		//DisplayServer.WindowSetMode(DisplayServer.WindowMode.ExclusiveFullscreen);
		DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);

		Node3D.GetNode<Label3D>("SongTitle").Text = $"{Map.Artist} - {Map.Title}";
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

		Progress += Delta * 1000;
		ToProcess = 0;

		Notes = new List<Note>();

		for (int i = PassedNotes; i < Map.Notes.Length; i++)	// note process check
		{
			Note note = Map.Notes[i];

			if (note.Millisecond + Phoenix.Constants.HitWindow < Progress)	// past hit window
			{
				if (i + 1 > PassedNotes)
				{
					PassedNotes = i + 1;
				}

				continue;
			} else if (note.Millisecond + Phoenix.Constants.HitWindow > Progress + Phoenix.Settings.ApproachTime * 1000)	// past approach distance
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

			if (note.Hit || note.Millisecond - Progress > 0)
			{
				continue;
			}

			if (CursorPosition.X + Phoenix.Constants.CursorSize / 2 >= note.X - 0.5f && CursorPosition.X - Phoenix.Constants.CursorSize / 2 <= note.X + 0.5f && CursorPosition.Y + Phoenix.Constants.CursorSize / 2 >= note.Y - 0.5f && CursorPosition.Y - Phoenix.Constants.CursorSize / 2 <= note.Y + 0.5f)
			{
				Map.Notes[note.Index].Hit = true;
			}
		}
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseMotion eventMouseMotion)
		{
			CursorPosition += new Vector2(1, -1) * eventMouseMotion.Relative / 50 * Phoenix.Settings.Sensitivity;
			CursorPosition = CursorPosition.Clamp(-Phoenix.Constants.Bounds, Phoenix.Constants.Bounds);

			Cursor.Translate(new Vector3(CursorPosition.X, CursorPosition.Y, 0) - Cursor.Transform.Origin);
			Camera.Translate(new Vector3(0, 0, 3.75f) + new Vector3(CursorPosition.X, CursorPosition.Y, 0) * Phoenix.Settings.Parallax - Camera.Transform.Origin);
		}
		else if (@event is InputEventKey eventKey && eventKey.Pressed)
		{
			if (eventKey.Keycode == Key.Escape)
			{
				Stop();
				GetTree().ChangeSceneToFile("res://scenes/main_menu.tscn");
			}
		}
	}

	public static void Play(Map map)
	{
		Map = map;
		Progress = -1000;	// 1 second break before start
		Notes = null;
		PassedNotes = 0;
		CursorPosition = new Vector2();
		Playing = true;
	}

	public static void Stop()
	{
		Playing = false;
		Notes = null;
	}
}