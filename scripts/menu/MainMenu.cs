using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Godot;
using Phoenyx;

namespace Menu;

public partial class MainMenu : Control
{
	private static Control Control;
	private static readonly PackedScene ChatMessage = GD.Load<PackedScene>("res://prefabs/chat_message.tscn");
	private static readonly PackedScene MapButton = GD.Load<PackedScene>("res://prefabs/map_button.tscn");

	private static TextureRect Cursor;
	private static Panel TopBar;
	private static Panel Jukebox;
	private static Button JukeboxButton;
	private static ColorRect JukeboxProgress;
	private static HBoxContainer JukeboxSpectrum;
	private static AudioStreamPlayer Audio;
	private static AudioEffectSpectrumAnalyzerInstance AudioSpectrum;
	private static Panel ContextMenu;

	private static Button ImportButton;
	private static Button UserFolderButton;
	private static Button SettingsButton;
	private static LineEdit SearchEdit;
	private static FileDialog ImportDialog;
	private static ScrollContainer MapList;
	private static VBoxContainer MapListContainer;

	private static Panel SettingsHolder;

	private static Panel MultiplayerHolder;
	private static LineEdit IPLine;
	private static LineEdit PortLine;
	private static LineEdit ChatLine;
	private static Button Host;
	private static Button Join;
	private static ScrollContainer ChatScrollContainer;
	private static VBoxContainer ChatHolder;

	private static bool Initialized = false;
	private static bool FirstFrame = true;
	private static bool Quitting = false;
	private static Vector2I WindowSize = DisplayServer.WindowGetSize();
	private static double LastFrame = Time.GetTicksUsec();
	private static string[] JukeboxQueue = Array.Empty<string>();
	private static int JukeboxIndex = 0;
	private static bool JukeboxPaused = false;
	private static ulong LastRewind = 0;
	private static float Scroll = 0;
	private static float TargetScroll = 0;
	private static int MaxScroll = 0;
	private static Vector2 MousePosition = Vector2.Zero;
	private static bool RightMouseHeld = false;
	private static bool RightClickingButton = false;
	private static List<string> LoadedMaps = [];
	private static List<string> FavoritedMaps = [];
	private static Dictionary<string, int> OriginalMapOrder = [];
	private static int VisibleMaps = 0;
	private static string SelectedMap = null;
	private static bool SettingsShown = false;
	private static LineEdit FocusedLineEdit = null;
	private static string Search = "";
	private static string ContextMenuTarget;

	public override void _Ready()
	{
		Control = this;
	
		Util.Setup();

		Util.DiscordRPC.Call("Set", "details", "Browsing Maps");
		Util.DiscordRPC.Call("Set", "state", "");
		Util.DiscordRPC.Call("Set", "end_timestamp", 0);

		Input.MouseMode = Input.MouseModeEnum.Visible;
		DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Mailbox);

		GetTree().AutoAcceptQuit = false;
		WindowSize = DisplayServer.WindowGetSize();
		VisibleMaps = 0;
		FirstFrame = true;
		
		if (!Initialized)
		{
			Initialized = true;
			Viewport viewport = GetViewport();

			viewport.SizeChanged += () => {
				WindowSize = DisplayServer.WindowGetSize();
				UpdateMaxScroll();
				TargetScroll = Math.Clamp(TargetScroll, 0, MaxScroll);
				UpdateSpectrumSpacing();
			};
			viewport.Connect("files_dropped", Callable.From((string[] files) => {
				MapParser.Import(files);
				UpdateMapList();
				Panel mapButton = MapListContainer.GetNode<Panel>(files[0].Split("\\")[^1].TrimSuffix(".phxm").TrimSuffix(".sspm").TrimSuffix(".txt"));
				
				if (!mapButton.Name.ToString().Contains(Search))
				{
					mapButton.Visible = false;
					VisibleMaps--;
				}
			}));
		}

		// General

		Cursor = GetNode<TextureRect>("Cursor");
		TopBar = GetNode<Panel>("TopBar");
		Jukebox = GetNode<Panel>("Jukebox");
		JukeboxButton = Jukebox.GetNode<Button>("Button");
		JukeboxProgress = Jukebox.GetNode("Progress").GetNode<ColorRect>("Main");
		JukeboxSpectrum = Jukebox.GetNode<HBoxContainer>("Spectrum");
		Audio = GetNode<AudioStreamPlayer>("AudioStreamPlayer");
		AudioSpectrum = (AudioEffectSpectrumAnalyzerInstance)AudioServer.GetBusEffectInstance(0, 0);
		ContextMenu = GetNode<Panel>("ContextMenu");
		LoadedMaps = [];

		Cursor.Texture = Phoenyx.Skin.CursorImage;
		Cursor.Size = new Vector2(32 * (float)Settings.CursorScale, 32 * (float)Settings.CursorScale);

		JukeboxQueue = Directory.GetFiles($"{Constants.UserFolder}/maps");
		JukeboxIndex = (int)new Random().NextInt64(Math.Max(0, JukeboxQueue.Length - 1));

		JukeboxButton.MouseEntered += () => {
			Label title = Jukebox.GetNode<Label>("Title");
			Tween tween = title.CreateTween();
			tween.TweenProperty(title, "modulate", Color.FromHtml("ffffffff"), 0.25).SetTrans(Tween.TransitionType.Quad);
			tween.Play();
		};
		JukeboxButton.MouseExited += () => {
			Label title = Jukebox.GetNode<Label>("Title");
			Tween tween = title.CreateTween();
			tween.TweenProperty(title, "modulate", Color.FromHtml("c2c2c2ff"), 0.25).SetTrans(Tween.TransitionType.Quad);
			tween.Play();
		};
		JukeboxButton.Pressed += () => {
			string[] split = JukeboxQueue[JukeboxIndex].Split("\\");
			string fileName = split[^1].TrimSuffix(".phxm");
			Panel mapButton = MapListContainer.GetNode<Panel>(fileName);

			TargetScroll = Math.Clamp(mapButton.Position.Y + mapButton.Size.Y - WindowSize.Y / 2, 0, MaxScroll);
		};

		Audio.Finished += () => {
			JukeboxIndex++;
			PlayJukebox(JukeboxIndex);
		};

		foreach (Node child in Jukebox.GetChildren())
		{
			if (child.GetType().Name != "TextureButton")
			{
				continue;
			}

			TextureButton button = child as TextureButton;

			button.MouseEntered += () => {
				Tween tween = button.CreateTween();
				tween.TweenProperty(button, "self_modulate", Color.FromHtml("ffffffff"), 0.25).SetTrans(Tween.TransitionType.Quad);
				tween.Play();
			};
			button.MouseExited += () => {
				Tween tween = button.CreateTween();
				tween.TweenProperty(button, "self_modulate", Color.FromHtml("c2c2c2ff"), 0.25).SetTrans(Tween.TransitionType.Quad);
				tween.Play();
			};
			button.Pressed += () => {
				switch (button.Name)
				{
					case "Pause":
						JukeboxPaused = !JukeboxPaused;
						button.TextureNormal = JukeboxPaused ? Phoenyx.Skin.JukeboxPlayImage : Phoenyx.Skin.JukeboxPauseImage;
						Audio.PitchScale = JukeboxPaused ? 0.00000000001f : 1;	// bruh
						break;
					case "Skip":
						JukeboxIndex++;
						PlayJukebox(JukeboxIndex);
						break;
					case "Rewind":
						ulong now = Time.GetTicksMsec();

						if (now - LastRewind < 1000)
						{
							JukeboxIndex--;
							PlayJukebox(JukeboxIndex);
						}
						else
						{
							Audio.Seek(0);
						}

						LastRewind = now;
						break;
				}
			};
		}

		// Map selection

		ImportButton = TopBar.GetNode<Button>("Import");
		UserFolderButton = TopBar.GetNode<Button>("UserFolder");
		SettingsButton = TopBar.GetNode<Button>("Settings");
		SearchEdit = TopBar.GetNode<LineEdit>("Search");
		ImportDialog = GetNode<FileDialog>("ImportDialog"); 
		MapList = GetNode<ScrollContainer>("MapList");
		MapListContainer = MapList.GetNode<VBoxContainer>("Container");
		
		ImportButton.Pressed += ImportDialog.Show;
		UserFolderButton.Pressed += () => {
			OS.ShellOpen($"{Constants.UserFolder}");
		};
		SettingsButton.Pressed += () => {
			ShowSettings();
		};
 		SearchEdit.TextChanged += (string text) => {
			Search = text.ToLower();

			if (Search == "")
			{
				SearchEdit.ReleaseFocus();
			}

			VisibleMaps = 0;

			foreach (Panel map in MapListContainer.GetChildren())
			{
				map.Visible = map.GetNode("Holder").GetNode<Label>("Title").Text.ToLower().Contains(Search);

				if (map.Visible)
				{
					VisibleMaps++;
				}
			}

			UpdateMaxScroll();
		};
		ImportDialog.FilesSelected += (string[] files) => {
			MapParser.Import(files);
			UpdateMapList();
		};

		UpdateMapList();

		SearchEdit.Text = Search;

		foreach (Panel map in MapListContainer.GetChildren())
		{
			if (map.FindChild("Holder") == null)
			{
				continue;
			}

			map.Visible = map.GetNode("Holder").GetNode<Label>("Title").Text.ToLower().Contains(Search);
		}

		// Settings

		SettingsHolder = GetNode("Settings").GetNode<Panel>("Holder");
		SettingsHolder.GetParent().GetNode<Button>("Deselect").Pressed += () => {
			HideSettings();
		};

		HideSettings();

		foreach (Node holder in SettingsHolder.GetNode("Sidebar").GetNode("Container").GetChildren())
		{
			holder.GetNode<Button>("Button").Pressed += () => {
				foreach (ColorRect otherHolder in SettingsHolder.GetNode("Sidebar").GetNode("Container").GetChildren())
				{
					otherHolder.Color = Color.FromHtml($"#ffffff{(holder.Name == otherHolder.Name ? "08" : "00")}");
				}

				foreach (ScrollContainer category in SettingsHolder.GetNode("Categories").GetChildren())
				{
					category.Visible = category.Name == holder.Name;
				}
			};
		}

		OptionButton skinOptions = SettingsHolder.GetNode("Categories").GetNode("Visuals").GetNode("Container").GetNode("Skin").GetNode<OptionButton>("OptionsButton");

		skinOptions.ItemSelected += (long item) => {
			Settings.Skin = skinOptions.GetItemText((int)item);
			Phoenyx.Skin.Load();

			Cursor.Texture = Phoenyx.Skin.CursorImage;
			SettingsHolder.GetNode("Categories").GetNode("Visuals").GetNode("Container").GetNode("Colors").GetNode<LineEdit>("LineEdit").Text = Phoenyx.Skin.RawColors;
		};

		ContextMenu.GetNode("Container").GetNode<Button>("Favorite").Pressed += () => {
			ContextMenu.Visible = false;

			string favorites = File.ReadAllText($"{Constants.UserFolder}/favorites.txt");
			bool favorited = favorites.Split("\n").ToList().Contains(ContextMenuTarget);
			TextureRect favorite = MapListContainer.GetNode(ContextMenuTarget).GetNode("Holder").GetNode<TextureRect>("Favorited");
			favorite.Visible = !favorited;

			if (favorited)
			{
				File.WriteAllText($"{Constants.UserFolder}/favorites.txt", favorites.Replace($"{ContextMenuTarget}\n", ""));
				FavoritedMaps.Remove(ContextMenuTarget);
			}
			else
			{
				favorite.Texture = Phoenyx.Skin.FavoriteImage;
				File.WriteAllText($"{Constants.UserFolder}/favorites.txt", $"{favorites}{ContextMenuTarget}\n");
				FavoritedMaps.Add(ContextMenuTarget);
			}
			
			MapListContainer.MoveChild(MapListContainer.GetNode(ContextMenuTarget), favorited ? OriginalMapOrder[ContextMenuTarget] : 0);

			ToastNotification.Notify($"Successfully {(favorited ? "removed" : "added")} map {(favorited ? "from" : "to")} favorites");
		};
		ContextMenu.GetNode("Container").GetNode<Button>("Delete").Pressed += () => {
			ContextMenu.Visible = false;
			MapListContainer.GetNode(ContextMenuTarget).QueueFree();
			LoadedMaps.Remove(ContextMenuTarget);
			
			if (ContextMenuTarget.Contains(Search))
			{
				VisibleMaps--;
			}

			File.Delete($"{Constants.UserFolder}/maps/{ContextMenuTarget}.phxm");
			
			if (Directory.Exists($"{Constants.UserFolder}/cache/maps/{ContextMenuTarget}"))
			{
				foreach (string file in Directory.GetFiles($"{Constants.UserFolder}/cache/maps/{ContextMenuTarget}"))
				{
					File.Delete(file);
				}

				Directory.Delete($"{Constants.UserFolder}/cache/maps/{ContextMenuTarget}");
			}

			ToastNotification.Notify("Successfuly deleted map");
		};
		ContextMenu.GetNode("Container").GetNode<Button>("VideoAdd").Pressed += () => {
			ContextMenu.Visible = false;
			GetNode<FileDialog>("VideoDialog").Visible = true;
		};
		ContextMenu.GetNode("Container").GetNode<Button>("VideoRemove").Pressed += () => {
			ContextMenu.Visible = false;
			Map map = MapParser.Decode($"{Constants.UserFolder}/maps/{ContextMenuTarget}.phxm");

			File.Delete($"{Constants.UserFolder}/maps/{ContextMenuTarget}.phxm");

			map.VideoBuffer = null;

			MapParser.Encode(map);

			if (Directory.Exists($"{Constants.UserFolder}/cache/maps/{ContextMenuTarget}"))
			{
				foreach (string filePath in Directory.GetFiles($"{Constants.UserFolder}/cache/maps/{ContextMenuTarget}"))
				{
					File.Delete(filePath);
				}

				Directory.Delete($"{Constants.UserFolder}/cache/maps/{ContextMenuTarget}");
			}

			ToastNotification.Notify("Successfully removed video from map");
		};
		GetNode<FileDialog>("VideoDialog").FileSelected += (string path) => {
			if (path.GetExtension() != "mp4")
			{
				ToastNotification.Notify("Only .mp4 files are allowed", 1);
				return;
			}

			Godot.FileAccess file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
			byte[] videoBuffer = file.GetBuffer((long)file.GetLength());
			file.Close();
			Map map = MapParser.Decode($"{Constants.UserFolder}/maps/{ContextMenuTarget}.phxm");

			File.Delete($"{Constants.UserFolder}/maps/{ContextMenuTarget}.phxm");

			map.VideoBuffer = videoBuffer;

			MapParser.Encode(map);

			if (Directory.Exists($"{Constants.UserFolder}/cache/maps/{ContextMenuTarget}"))
			{
				foreach (string filePath in Directory.GetFiles($"{Constants.UserFolder}/cache/maps/{ContextMenuTarget}"))
				{
					File.Delete(filePath);
				}

				Directory.Delete($"{Constants.UserFolder}/cache/maps/{ContextMenuTarget}");
			}

			ToastNotification.Notify("Successfully added video to map");
		};

		SettingsHolder.GetNode("Categories").GetNode("Visuals").GetNode("Container").GetNode("Skin").GetNode<Button>("SkinFolder").Pressed += () => {
			OS.ShellOpen($"{Constants.UserFolder}/skins/{Settings.Skin}");
		};
		SettingsHolder.GetNode("Categories").GetNode("Other").GetNode("Container").GetNode("RhythiaImport").GetNode<Button>("Button").Pressed += () => {
			if (!Directory.Exists($"{OS.GetDataDir()}/SoundSpacePlus") || !File.Exists($"{OS.GetDataDir()}/SoundSpacePlus/settings.json"))
			{
				ToastNotification.Notify("Could not locate Rhythia settings", 1);
				return;
			}

			Godot.FileAccess file = Godot.FileAccess.Open($"{OS.GetDataDir()}/SoundSpacePlus/settings.json", Godot.FileAccess.ModeFlags.Read);
            Godot.Collections.Dictionary data = (Godot.Collections.Dictionary)Json.ParseString(file.GetAsText());

			Settings.ApproachRate = (float)data["approach_rate"];
			Settings.ApproachDistance = (float)data["spawn_distance"];
			Settings.ApproachTime = Settings.ApproachDistance / Settings.ApproachRate;
			Settings.FoV = (float)data["fov"];
			Settings.Sensitivity = (float)data["sensitivity"] * 2;
			Settings.Parallax = (float)data["parallax"] / 50;
			Settings.FadeIn = (float)data["fade_length"] * 100;
			Settings.FadeOut = (bool)data["half_ghost"];
			Settings.Pushback = (bool)data["do_note_pushback"];
			Settings.NoteSize = (float)data["note_size"] * 0.875f;
			Settings.CursorScale = (float)data["cursor_scale"];
			Settings.CursorTrail = (bool)data["cursor_trail"];
			Settings.TrailTime = (float)data["trail_time"];
			Settings.SimpleHUD = (bool)data["simple_hud"];

			UpdateSettings();

			ToastNotification.Notify("Successfully imported Rhythia settings");
		};

		UpdateSettings(true);

		// Multiplayer

		MultiplayerHolder = GetNode<Panel>("Multiplayer");
		IPLine = MultiplayerHolder.GetNode<LineEdit>("IP");
		PortLine = MultiplayerHolder.GetNode<LineEdit>("Port");
		ChatLine = MultiplayerHolder.GetNode<LineEdit>("ChatInput");
		Host = MultiplayerHolder.GetNode<Button>("Host");
		Join = MultiplayerHolder.GetNode<Button>("Join");
		ChatScrollContainer = MultiplayerHolder.GetNode<ScrollContainer>("Chat");
		ChatHolder = ChatScrollContainer.GetNode<VBoxContainer>("Holder");

		Host.Pressed += () => {
			try
			{
				ServerManager.CreateServer(IPLine.Text, PortLine.Text);
			}
			catch (Exception exception)
			{
				ToastNotification.Notify($"{exception.Message}", 2);
				return;
			}

			Host.Disabled = true;
			Join.Disabled = true;
		};
		Join.Pressed += () => {
			try
			{
				ClientManager.CreateClient(IPLine.Text, PortLine.Text);
			}
			catch (Exception exception)
			{
				ToastNotification.Notify($"{exception.Message}", 2);
				return;
			}

			Host.Disabled = true;
			Join.Disabled = true;
		};

		// Finish

		PlayJukebox(JukeboxIndex);

		JukeboxPaused = !Settings.AutoplayJukebox;
		Audio.PitchScale = JukeboxPaused ? 0.00000000001f : 1;	// bruh
		Jukebox.GetNode<TextureButton>("Pause").TextureNormal = JukeboxPaused ? Phoenyx.Skin.JukeboxPlayImage : Phoenyx.Skin.JukeboxPauseImage;

		UpdateSpectrumSpacing();

		Audio.VolumeDb = -180;
	}

    public override void _Process(double delta)
    {
		ulong now = Time.GetTicksUsec();
        delta = (now - LastFrame) / 1000000;
		LastFrame = now;
		Scroll = Mathf.Lerp(Scroll, TargetScroll, 8 * (float)delta);
		MapList.ScrollVertical = (int)Scroll;
		Cursor.Position = MousePosition - new Vector2(Cursor.Size.X / 2, Cursor.Size.Y / 2);

		if (FirstFrame)
		{
			FirstFrame = false;

			if (SelectedMap != null)
			{
				Panel selectedMapHolder = MapListContainer.GetNode(SelectedMap).GetNode<Panel>("Holder");
				selectedMapHolder.GetNode<Panel>("Normal").Visible = false;
				selectedMapHolder.GetNode<Panel>("Selected").Visible = true;
				selectedMapHolder.Size = new Vector2(MapListContainer.Size.X, selectedMapHolder.Size.Y);
				selectedMapHolder.Position = new Vector2(0, selectedMapHolder.Position.Y);
			}
		}
		
		if (Audio.Stream != null)
		{
			JukeboxProgress.AnchorRight = (float)Math.Clamp(Audio.GetPlaybackPosition() / Audio.Stream.GetLength(), 0, 1);
			Audio.VolumeDb = Mathf.Lerp(Audio.VolumeDb, Quitting ? -80 : -80 + 70 * (float)Math.Pow(Settings.VolumeMusic / 100, 0.1) * (float)Math.Pow(Settings.VolumeMaster / 100, 0.1), (float)Math.Clamp(delta * 2, 0, 1));
		}

		float prevHz = 0;

		for (int i = 0; i < 32; i++)
		{
			float hz = (i + 1) * 4000 / 32;
			float magnitude = AudioSpectrum.GetMagnitudeForFrequencyRange(prevHz, hz).Length();
			float energy = (60 + Mathf.LinearToDb(magnitude)) / 30;
			prevHz = hz;
			
			ColorRect colorRect = JukeboxSpectrum.GetNode((i + 1).ToString()).GetNode<ColorRect>("Main");
			colorRect.AnchorTop = Math.Clamp(Mathf.Lerp(colorRect.AnchorTop, 1 - energy * (JukeboxPaused ? 0 : 1), (float)delta * 12), 0, 1);
		}

		for (int i = 0; i < FavoritedMaps.Count; i++)
		{
			TextureRect favoriteRect = MapListContainer.GetNode(FavoritedMaps[i]).GetNode("Holder").GetNode<TextureRect>("Favorited");
			Color modulate = Color.FromHtml("ffffff" + (196 + (int)(59 * Math.Sin(Math.PI * now / 1000000 + i))).ToString("X2"));
			favoriteRect.Rotation = (float)now / 1000000;
			favoriteRect.Modulate = modulate;
		}
    }

    public override void _Input(InputEvent @event)
    {
		if (@event is InputEventKey eventKey && eventKey.Pressed)
		{
			switch (eventKey.Keycode)
			{
				case Key.Space:
					if (SelectedMap != null && !SearchEdit.HasFocus())
					{
						Map map = MapParser.Decode($"{Constants.UserFolder}/maps/{SelectedMap}.phxm");

						Audio.Stop();
						SceneManager.Load("res://scenes/game.tscn");
						Runner.Play(map, Lobby.Speed, Lobby.Mods);
					}
					break;
				case Key.Mediaplay:
					JukeboxPaused = !JukeboxPaused;
					Audio.PitchScale = JukeboxPaused ? 0.00000000001f : 1;	// bruh
					Jukebox.GetNode<TextureButton>("Pause").TextureNormal = JukeboxPaused ? Phoenyx.Skin.JukeboxPlayImage : Phoenyx.Skin.JukeboxPauseImage;
					break;
				case Key.Medianext:
					JukeboxIndex++;
					PlayJukebox(JukeboxIndex);
					break;
				case Key.Mediaprevious:
					ulong now = Time.GetTicksMsec();

					if (now - LastRewind < 1000)
					{
						JukeboxIndex--;
						PlayJukebox(JukeboxIndex);
					}
					else
					{
						Audio.Seek(0);
					}

					LastRewind = now;
					break;
				default:
					if (FocusedLineEdit == null && !eventKey.CtrlPressed && !eventKey.AltPressed && eventKey.Keycode != Key.Ctrl && eventKey.Keycode != Key.Shift && eventKey.Keycode != Key.Alt && eventKey.Keycode != Key.Escape && eventKey.Keycode != Key.Enter && eventKey.Keycode != Key.F11)
					{
						SearchEdit.GrabFocus();
					}
					break;
			}
		}
		else if (@event is InputEventMouseButton eventMouseButton)
		{
			if (!SettingsShown && !eventMouseButton.CtrlPressed)
			{
				switch (eventMouseButton.ButtonIndex)
				{
					case MouseButton.Right:
						RightMouseHeld = eventMouseButton.Pressed;
						
						if (!RightClickingButton)
						{
							TargetScroll = Math.Clamp((MousePosition.Y - 50) / (DisplayServer.WindowGetSize().Y - 100), 0, 1) * MaxScroll;
						}

						if (!RightMouseHeld && RightClickingButton)
						{
							RightClickingButton = false;
						} 

						break;
					case MouseButton.WheelUp:
						ContextMenu.Visible = false;
						TargetScroll = Math.Max(0, TargetScroll - 80);
						break;
					case MouseButton.WheelDown:
						ContextMenu.Visible = false;
						TargetScroll = Math.Min(MaxScroll, TargetScroll + 80);
						break;
				}
			}
		}
		else if (@event is InputEventMouseMotion eventMouseMotion)
		{
			MousePosition = eventMouseMotion.Position;

			if (RightMouseHeld && !RightClickingButton)
			{
				TargetScroll = Math.Clamp((MousePosition.Y - 50) / (DisplayServer.WindowGetSize().Y - 100), 0, 1) * MaxScroll;
			}
		}
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey eventKey && eventKey.Pressed)
		{
			if (eventKey.CtrlPressed)
			{
				switch (eventKey.Keycode)
				{
					case Key.O:
						ShowSettings(!SettingsShown);
						break;
				}
			}

			switch (eventKey.Keycode)
			{
				case Key.Escape:
					if (SettingsShown)
					{
						HideSettings();
					}
					else
					{
						Control.GetTree().Root.PropagateNotification((int)NotificationWMCloseRequest);
					}
					break;
			}
		}
		else if (@event is InputEventMouseButton eventMouseButton && eventMouseButton.Pressed)
		{
			switch (eventMouseButton.ButtonIndex)
			{
				case MouseButton.Left:
					ContextMenu.Visible = false;
					break;
			}
		}
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest)
		{
			Quit();
		}
    }

	public static void PlayJukebox(int index)
	{
		if (index >= JukeboxQueue.Length)
		{
			index = 0;
		}
		else if (index < 0)
		{
			index = JukeboxQueue.Length - 1;
		}

		if (JukeboxQueue.Length == 0)
		{
			return;
		}

		Map map = MapParser.Decode(JukeboxQueue[index], false);

		if (map.AudioBuffer == null)
		{
			JukeboxIndex++;
			PlayJukebox(JukeboxIndex);
		}

		Jukebox.GetNode<Label>("Title").Text = map.PrettyTitle;

		Audio.Stream = Lib.Audio.LoadStream(map.AudioBuffer);
		Audio.Play();

		Util.DiscordRPC.Call("Set", "state", $"Listening to {map.PrettyTitle}");
	}

	public static void ShowSettings(bool show = true)
	{
		SettingsShown = show;

		ColorRect parent = SettingsHolder.GetParent<ColorRect>();
		parent.GetNode<Button>("Deselect").MouseFilter = SettingsShown ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;

		if (SettingsShown)
		{
			parent.Visible = true;
		}

		Tween tween = parent.CreateTween();
		tween.TweenProperty(parent, "modulate", Color.FromHtml($"#ffffff{(SettingsShown ? "ff" : "00")}"), 0.25).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
		tween.Parallel().TweenProperty(SettingsHolder, "offset_top", SettingsShown ? 0 : 25, 0.25).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
		tween.Parallel().TweenProperty(SettingsHolder, "offset_bottom", SettingsShown ? 0 : 25, 0.25).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
		tween.TweenCallback(Callable.From(() => {
			parent.Visible = SettingsShown;
		}));
		tween.Play();
	}

	public static void HideSettings()
	{
		ShowSettings(false);
	}

	public static void ApplySetting(string setting, object value)
	{
		switch (setting)
		{
			case "Sensitivity":
				Settings.Sensitivity = (double)value;
				break;
			case "ApproachRate":
				Settings.ApproachRate = (double)value;
				Settings.ApproachTime = Settings.ApproachDistance / Settings.ApproachRate;
				break;
			case "ApproachDistance":
				Settings.ApproachDistance = (double)value;
				Settings.ApproachTime = Settings.ApproachDistance / Settings.ApproachRate;
				break;
			case "FadeIn":
				Settings.FadeIn = (double)value;
				break;
			case "Parallax":
				Settings.Parallax = (double)value;
				break;
			case "FoV":
				Settings.FoV = (double)value;
				break;
			case "VolumeMaster":
				Settings.VolumeMaster = (double)value;
				break;
			case "VolumeMusic":
				Settings.VolumeMusic = (double)value;
				break;
			case "VolumeSFX":
				Settings.VolumeSFX = (double)value;
				break;
			case "AlwaysPlayHitSound":
				Settings.AlwaysPlayHitSound = (bool)value;
				break;
			case "NoteSize":
				Settings.NoteSize = (double)value;
				break;
			case "CursorScale":
				Settings.CursorScale = (double)value;
				Cursor.Size = new Vector2(32 * (float)Settings.CursorScale, 32 * (float)Settings.CursorScale);
				break;
			case "CameraLock":
				Settings.CameraLock = (bool)value;
				break;
			case "FadeOut":
				Settings.FadeOut = (bool)value;
				break;
			case "Pushback":
				Settings.Pushback = (bool)value;
				break;
			case "Fullscreen":
				Settings.Fullscreen = (bool)value;
				DisplayServer.WindowSetMode((bool)value ? DisplayServer.WindowMode.ExclusiveFullscreen : DisplayServer.WindowMode.Windowed);
				break;
			case "CursorTrail":
				Settings.CursorTrail = (bool)value;
				break;
			case "TrailTime":
				Settings.TrailTime = (double)value;
				break;
			case "TrailDetail":
				Settings.TrailDetail = (double)value;
				break;
			case "CursorDrift":
				Settings.CursorDrift = (bool)value;
				break;
			case "VideoDim":
				Settings.VideoDim = (double)value;
				break;
			case "VideoRenderScale":
				Settings.VideoRenderScale = (double)value;
				break;
			case "SimpleHUD":
				Settings.SimpleHUD = (bool)value;
				break;
			case "AutoplayJukebox":
				Settings.AutoplayJukebox = (bool)value;
				break;
		}

		UpdateSettings();
	}

	public static void UpdateSettings(bool connections = false)
	{
		OptionButton skinOptions = SettingsHolder.GetNode("Categories").GetNode("Visuals").GetNode("Container").GetNode("Skin").GetNode<OptionButton>("OptionsButton");

		skinOptions.Clear();

		int i = 0;

		foreach (string path in Directory.GetDirectories($"{Constants.UserFolder}/skins"))
		{
			string[] split = path.Split("\\");
			string name = split[^1];
			
			skinOptions.AddItem(name, i);

			if (Settings.Skin == name)
			{
				skinOptions.Selected = i;
				SettingsHolder.GetNode("Categories").GetNode("Visuals").GetNode("Container").GetNode("Colors").GetNode<LineEdit>("LineEdit").Text = Phoenyx.Skin.RawColors;
			}

			i++;
		}

		foreach (ScrollContainer category in SettingsHolder.GetNode("Categories").GetChildren())
		{
			foreach (Panel option in category.GetNode("Container").GetChildren())
			{
				var property = new Settings().GetType().GetProperty(option.Name);
				
				if (option.FindChild("HSlider") != null)
				{
					HSlider slider = option.GetNode<HSlider>("HSlider");
					LineEdit lineEdit = option.GetNode<LineEdit>("LineEdit");

					slider.Value = (double)property.GetValue(new());
					lineEdit.Text = (Math.Floor(slider.Value * 1000) / 1000).ToString();

					if (connections)
					{
						void set(string text)
						{
							try
							{
								if (text == "")
								{
									text = lineEdit.PlaceholderText;
								}

								slider.Value = text.ToFloat();
								lineEdit.Text = slider.Value.ToString();
								
								ApplySetting(option.Name, slider.Value);
							}
							catch (Exception exception)
							{
								ToastNotification.Notify($"Incorrect format; {exception.Message}", 2);
							}

							lineEdit.ReleaseFocus();
						}

						slider.ValueChanged += (double value) => {
							lineEdit.Text = value.ToString();

							ApplySetting(option.Name, value);
						};
						lineEdit.FocusEntered += () => {
							FocusedLineEdit = lineEdit;
						};
						lineEdit.FocusExited += () => {
							set(lineEdit.Text);
							FocusedLineEdit = null;
						};
						lineEdit.TextSubmitted += (string text) => {
							set(text);
						};
					}
				}
				else if (option.FindChild("CheckButton") != null)
				{
					CheckButton checkButton = option.GetNode<CheckButton>("CheckButton");
					
					checkButton.ButtonPressed = (bool)property.GetValue(new());
					
					if (connections)
					{
						checkButton.Toggled += (bool value) => {
							ApplySetting(option.Name, value);
						};
					}
				}
				else if (option.FindChild("LineEdit") != null)
				{
					LineEdit lineEdit = option.GetNode<LineEdit>("LineEdit");

                    void set(string text)
                    {
						if (text == "")
						{
							text = lineEdit.PlaceholderText;
							lineEdit.Text = text;
						}

						switch (option.Name)
						{
							case "Colors":
								string[] split = text.Replace(" ", "").Replace("\n", ",").Split(",");
								string raw = "";
								Color[] colors = new Color[split.Length];

								if (split.Length == 0)
								{
									split = lineEdit.PlaceholderText.Split(",");
								}

								for (int i = 0; i < split.Length; i++)
								{
									split[i] = split[i].TrimPrefix("#").Substr(0, 6).PadRight(6, Convert.ToChar("f"));
									split[i] = new Regex("[^a-fA-F0-9$]").Replace(split[i], "f");
									colors[i] = Color.FromHtml(split[i]);

									raw += $"{split[i]},";
								}

								raw = raw.TrimSuffix(",");
								lineEdit.Text = raw;

								Phoenyx.Skin.Colors = colors;
								Phoenyx.Skin.RawColors = raw;

								break;
						}

						lineEdit.ReleaseFocus();
                    }

                    if (connections)
					{
						lineEdit.FocusEntered += () => {
							FocusedLineEdit = lineEdit;
						};
						lineEdit.FocusExited += () => {
							set(lineEdit.Text);
							FocusedLineEdit = null;
						};
						lineEdit.TextSubmitted += (string text) => {
							set(text);
						};
					}
				}
			}
		}
	}

	public static void UpdateVolume()
	{
		SettingsHolder.GetNode("Categories").GetNode("Audio").GetNode("Container").GetNode("VolumeMaster").GetNode<HSlider>("HSlider").Value = Settings.VolumeMaster;
	}

    public static void UpdateMapList()
	{
		double start = Time.GetTicksUsec();
		int i = 0;
		List<string> favorites = File.ReadAllText($"{Constants.UserFolder}/favorites.txt").Split("\n").ToList();
		FavoritedMaps = [];

		foreach (string mapFile in Directory.GetFiles($"{Constants.UserFolder}/maps"))
		{
			try
			{
				string[] split = mapFile.Split("\\");
				string fileName = split[^1].Replace(".phxm", "");
				bool favorited = favorites.Contains(fileName);
				
				if (mapFile.GetExtension() != "phxm" || LoadedMaps.Contains(fileName))
				{
					if (favorited)
					{
						FavoritedMaps.Add(fileName);
					}

					continue;
				}

				string title;
				string extra;
				int difficulty;
				string coverFile = null;

				if (!Directory.Exists($"{Constants.UserFolder}/cache/maps/{fileName}"))
				{
					Directory.CreateDirectory($"{Constants.UserFolder}/cache/maps/{fileName}");
					Map map = MapParser.Decode(mapFile, false);

					File.WriteAllText($"{Constants.UserFolder}/cache/maps/{fileName}/metadata.json", map.EncodeMeta());

					//if (map.CoverBuffer != null)
					//{
					//	Godot.FileAccess cover = Godot.FileAccess.Open($"{Constants.UserFolder}/cache/maps/{fileName}/cover.png", Godot.FileAccess.ModeFlags.Write);
					//	cover.StoreBuffer(map.CoverBuffer);
					//	cover.Close();
					//	
					//	Image coverImage = Image.LoadFromFile($"{Constants.UserFolder}/cache/maps/{fileName}/cover.png");
					//	coverImage.Resize(128, 128);
					//	coverImage.SavePng($"{Constants.UserFolder}/cache/maps/{fileName}/cover.png");
					//	coverFile = $"{Constants.UserFolder}/cache/maps/{fileName}/cover.png";
					//}

					title = map.PrettyTitle;
					extra = $"{map.DifficultyName} - {map.PrettyMappers}";
					difficulty = map.Difficulty;
				}
				else
				{
					Godot.FileAccess metaFile = Godot.FileAccess.Open($"{Constants.UserFolder}/cache/maps/{fileName}/metadata.json", Godot.FileAccess.ModeFlags.Read);
					Godot.Collections.Dictionary metadata = (Godot.Collections.Dictionary)Json.ParseString(Encoding.UTF8.GetString(metaFile.GetBuffer((long)metaFile.GetLength())));
					metaFile.Close();

					//if (File.Exists($"{Constants.UserFolder}/cache/maps/{fileName}/cover.png"))
					//{
					//	coverFile = $"{Constants.UserFolder}/cache/maps/{fileName}/cover.png";
					//}

					string mappers = "";

					foreach (string mapper in (string[])metadata["Mappers"])
					{
						mappers += $"{mapper}, ";
					}

					mappers = mappers.Substr(0, mappers.Length - 2);
					extra = $"{metadata["DifficultyName"]} - {mappers}";
					title = (string)metadata["Artist"] != "" ? $"{(string)metadata["Artist"]} - {(string)metadata["Title"]}" : (string)metadata["Title"];
					difficulty = (int)metadata["Difficulty"];
				}

				LoadedMaps.Add(fileName);
				VisibleMaps++;

				Panel mapButton = MapButton.Instantiate<Panel>();
				Panel holder = mapButton.GetNode<Panel>("Holder");
				
				if (coverFile != null)
				{
					holder.GetNode<TextureRect>("Cover").Texture = ImageTexture.CreateFromImage(Image.LoadFromFile(coverFile));
				}

				mapButton.Name = fileName;
				holder.GetNode<Label>("Title").Text = title;
				holder.GetNode<Label>("Extra").Text = extra.ReplaceLineEndings("");

				MapListContainer.AddChild(mapButton);

				OriginalMapOrder[fileName] = i + 1;

				if (favorited)
				{
					MapListContainer.MoveChild(mapButton, 0);
					FavoritedMaps.Add(fileName);

					TextureRect favorite = holder.GetNode<TextureRect>("Favorited");
					favorite.Texture = Phoenyx.Skin.FavoriteImage;
					favorite.Visible = true;
				}

				holder.GetNode<Button>("Button").MouseEntered += () => {
					holder.GetNode<ColorRect>("Hover").Color = Color.FromHtml("#ffffff10");
				};
				
				holder.GetNode<Button>("Button").MouseExited += () => {
					holder.GetNode<ColorRect>("Hover").Color = Color.FromHtml("#ffffff00");
				};

				holder.GetNode<Button>("Button").Pressed += () => {
					ContextMenu.Visible = false;
					
					if (!RightMouseHeld)
					{
						if (SelectedMap != null)
						{
							Panel selectedHolder = MapListContainer.GetNode(SelectedMap).GetNode<Panel>("Holder");
							selectedHolder.GetNode<Panel>("Normal").Visible = true;
							selectedHolder.GetNode<Panel>("Selected").Visible = false;

							Tween deselectTween = selectedHolder.CreateTween();
							deselectTween.TweenProperty(selectedHolder, "size", new Vector2(MapListContainer.Size.X - 60, selectedHolder.Size.Y), 0.25).SetTrans(Tween.TransitionType.Quad);
							deselectTween.Parallel().TweenProperty(selectedHolder, "position", new Vector2(60, selectedHolder.Position.Y), 0.25).SetTrans(Tween.TransitionType.Quad);
							deselectTween.Play();

							if (MapListContainer.GetNode(SelectedMap) == mapButton)
							{
								Map map = MapParser.Decode(mapFile);

								Audio.Stop();
								SceneManager.Load("res://scenes/game.tscn");
								Runner.Play(map, Lobby.Speed, Lobby.Mods);
							}
						}

						holder.GetNode<Panel>("Normal").Visible = false;
						holder.GetNode<Panel>("Selected").Visible = true;
						
						Tween selectTween = holder.CreateTween();
						selectTween.TweenProperty(holder, "size", new Vector2(MapListContainer.Size.X, holder.Size.Y), 0.25).SetTrans(Tween.TransitionType.Quad);
						selectTween.Parallel().TweenProperty(holder, "position", new Vector2(0, holder.Position.Y), 0.25).SetTrans(Tween.TransitionType.Quad);
						selectTween.Play();

						TargetScroll = Math.Clamp(mapButton.Position.Y + mapButton.Size.Y - WindowSize.Y / 2, 0, MaxScroll);
						SelectedMap = mapButton.Name;
					}
					else
					{
						RightClickingButton = true;
						TargetScroll = Math.Clamp(mapButton.Position.Y + mapButton.Size.Y - WindowSize.Y / 2, 0, MaxScroll);
						ContextMenu.Visible = true;
						ContextMenu.Position = MousePosition;
						ContextMenuTarget = fileName;

						bool favorited = FavoritedMaps.Contains(fileName);

						ContextMenu.GetNode("Container").GetNode<Button>("Favorite").Text = favorited ? "Unfavorite" : "Favorite";
					}
				};

				i++;
			}
			catch
			{
				continue;
			}
		}

		UpdateMaxScroll();

		Logger.Log($"MAPLIST UPDATE: {(Time.GetTicksUsec() - start) / 1000}ms");
	}

	public static void UpdateMaxScroll()
	{
		MaxScroll = Math.Max(0, (int)(VisibleMaps * 90 - MapList.Size.Y));
	}

	public static void UpdateSpectrumSpacing()
	{
		JukeboxSpectrum.AddThemeConstantOverride("separation", ((int)JukeboxSpectrum.Size.X - 32 * 6) / 48);
	}

	public static void Chat(string message)
	{
		Label chatMessage = ChatMessage.Instantiate<Label>();
		chatMessage.Text = message;

		ChatHolder.AddChild(chatMessage);
		ChatScrollContainer.ScrollVertical += 100;
	}

	private static void SendMessage()
	{
		if (ChatLine.Text.Replace(" ", "") == "")
		{
			return;
		}
		
		ServerManager.Node.Rpc("ValidateChat", ChatLine.Text);
		ChatLine.Text = "";
	}

    private static void Quit()
	{
		Quitting = true;

		Settings.Save();
		Util.DiscordRPC.Call("Set", "end_timestamp", 0);
		Util.DiscordRPC.Call("Clear");

		Tween tween = Control.CreateTween();
		tween.TweenProperty(Control, "modulate", Color.Color8(1, 1, 1, 0), 0.5).SetTrans(Tween.TransitionType.Quad);
		tween.TweenCallback(Callable.From(() => {
			Control.GetTree().Quit();
		}));
		tween.Play();
	}
}