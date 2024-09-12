using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Godot;

namespace Menu;

public partial class MainMenu : Control
{
	public static Control Control;
	public static TextureRect Cursor;

	private static readonly PackedScene ChatMessage = GD.Load<PackedScene>("res://prefabs/chat_message.tscn");
	private static readonly PackedScene MapButton = GD.Load<PackedScene>("res://prefabs/map_button.tscn");

	private static Panel TopBar;
	private static ColorRect Background;
	private static Node[] BackgroundTiles;
	private static Panel Menus;
	private static Panel Main;
	private static Panel Jukebox;
	private static Button JukeboxButton;
	private static ColorRect JukeboxProgress;
	private static HBoxContainer JukeboxSpectrum;
	private static AudioEffectSpectrumAnalyzerInstance AudioSpectrum;
	private static Panel ContextMenu;
	private static TextureRect Peruchor;

	private static Panel PlayMenu;
	private static Panel SubTopBar;
	private static Button ImportButton;
	private static Button UserFolderButton;
	private static Button SettingsButton;
	private static LineEdit SearchEdit;
	private static LineEdit SearchAuthorEdit;
	private static FileDialog ImportDialog;
	private static ScrollContainer MapList;
	private static VBoxContainer MapListContainer;

	private static Panel Extras;

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
	private static Vector2I WindowSize = DisplayServer.WindowGetSize();
	private static double LastFrame = Time.GetTicksUsec();
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
	private static string CurrentMenu = "Main";
	private static string SearchTitle = "";
	private static string SearchAuthor = "";
	private static string ContextMenuTarget;
	private static Map CurrentMap;
	private static int PassedNotes = 0;
	private static int PeruSequenceIndex = 0;
	private static readonly string[] PeruSequence = ["P", "E", "R", "U"];

	public override void _Ready()
	{
		Control = this;
	
		Phoenyx.Util.Setup();

		Phoenyx.Util.DiscordRPC.Call("Set", "details", "Main Menu");
		Phoenyx.Util.DiscordRPC.Call("Set", "state", "");
		Phoenyx.Util.DiscordRPC.Call("Set", "end_timestamp", 0);

		Input.MouseMode = Input.MouseModeEnum.Hidden;
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
				
				if (!mapButton.Name.ToString().Contains(SearchTitle))
				{
					mapButton.Visible = false;
					VisibleMaps--;
				}
			}));
		}

		// General

		Cursor = GetNode<TextureRect>("Cursor");
		TopBar = GetNode<Panel>("TopBar");
		Background = GetNode<ColorRect>("Background");
		BackgroundTiles = Background.GetNode("TileHolder").GetChildren().ToArray();
		Menus = GetNode<Panel>("Menus");
		Main = Menus.GetNode<Panel>("Main");
		Extras = Menus.GetNode<Panel>("Extras");
		Jukebox = GetNode<Panel>("Jukebox");
		JukeboxButton = Jukebox.GetNode<Button>("Button");
		JukeboxProgress = Jukebox.GetNode("Progress").GetNode<ColorRect>("Main");
		JukeboxSpectrum = Jukebox.GetNode<HBoxContainer>("Spectrum");
		AudioSpectrum = (AudioEffectSpectrumAnalyzerInstance)AudioServer.GetBusEffectInstance(0, 0);
		ContextMenu = GetNode<Panel>("ContextMenu");
		Peruchor = Main.GetNode<TextureRect>("Peruchor");
		LoadedMaps = [];

		Cursor.Texture = Phoenyx.Skin.CursorImage;
		Cursor.Size = new Vector2(32 * (float)Phoenyx.Settings.CursorScale, 32 * (float)Phoenyx.Settings.CursorScale);

		VBoxContainer buttons = Main.GetNode<VBoxContainer>("Buttons");

		buttons.GetNode<Button>("Play").Pressed += () => {
			Transition("Play");
		};
		buttons.GetNode<Button>("Settings").Pressed += () => {
			SettingsManager.ShowSettings();
		};
		buttons.GetNode<Button>("Extras").Pressed += () => {
			foreach (Panel holder in Extras.GetNode("Stats").GetNode("ScrollContainer").GetNode("VBoxContainer").GetChildren())
			{
				string value = "";

				switch (holder.Name)
				{
					case "GamePlaytime":
						value = $"{Math.Floor((double)Phoenyx.Stats.GamePlaytime / 36) / 100} h";
						break;
					case "TotalPlaytime":
						value = $"{Math.Floor((double)Phoenyx.Stats.TotalPlaytime / 36) / 100} h";
						break;
					case "GamesOpened":
						value = Lib.String.PadMagnitude(Phoenyx.Stats.GamesOpened.ToString());
						break;
					case "TotalDistance":
						value = $"{Lib.String.PadMagnitude(((double)Phoenyx.Stats.TotalDistance / 1000).ToString())} m";
						break;
					case "NotesHit":
						value = Lib.String.PadMagnitude(Phoenyx.Stats.NotesHit.ToString());
						break;
					case "NotesMissed":
						value = Lib.String.PadMagnitude(Phoenyx.Stats.NotesMissed.ToString());
						break;
					case "HighestCombo":
						value = Lib.String.PadMagnitude(Phoenyx.Stats.HighestCombo.ToString());
						break;
					case "Attempts":
						value = Lib.String.PadMagnitude(Phoenyx.Stats.Attempts.ToString());
						break;
					case "Passes":
						value = Lib.String.PadMagnitude(Phoenyx.Stats.Passes.ToString());
						break;
					case "FullCombos":
						value = Lib.String.PadMagnitude(Phoenyx.Stats.FullCombos.ToString());
						break;
					case "HighestScore":
						value = Lib.String.PadMagnitude(Phoenyx.Stats.HighestScore.ToString());
						break;
					case "TotalScore":
						value = Lib.String.PadMagnitude(Phoenyx.Stats.TotalScore.ToString());
						break;
					case "AverageAccuracy":
						double sum = 0;

						foreach (double accuracy in Phoenyx.Stats.PassAccuracies)
						{
							sum += accuracy;
						}

						value = $"{(Phoenyx.Stats.PassAccuracies.Count == 0 ? 0 : Math.Floor(sum / Phoenyx.Stats.PassAccuracies.Count * 100) / 100).ToString().PadDecimals(2)}%";
						break;
					case "RageQuits":
						value = Lib.String.PadMagnitude(Phoenyx.Stats.RageQuits.ToString());
						break;
					case "FavouriteMap":
						string mostPlayedID = null;
						ulong mostPlayedCount = 0;

						foreach (KeyValuePair<string, ulong> entry in Phoenyx.Stats.FavouriteMaps)
						{
							if (entry.Value > mostPlayedCount)
							{
								mostPlayedID = entry.Key;
								mostPlayedCount = entry.Value;
							}
						}

						value = mostPlayedID != null ? MapParser.Decode($"{Phoenyx.Constants.UserFolder}/maps/{mostPlayedID}.phxm").PrettyTitle : "None";
						break;
				}

				holder.GetNode<Label>("Value").Text = value;
			}

			Transition("Extras");
		};
		buttons.GetNode<Button>("Quit").Pressed += () => {
			Control.GetTree().Root.PropagateNotification((int)NotificationWMCloseRequest);
		};

		TopBar.GetNode<TextureButton>("Discord").Pressed += () => {
			OS.ShellOpen("https://discord.gg/aSyC7btWDX");
		};

		SoundManager.JukeboxQueue = Directory.GetFiles($"{Phoenyx.Constants.UserFolder}/maps");
		SoundManager.JukeboxIndex = (int)new Random().NextInt64(Math.Max(0, SoundManager.JukeboxQueue.Length - 1));
		SoundManager.JukeboxPlayed += (Map map) => {
			PassedNotes = 0;
			CurrentMap = map;
		};

		for (int i = 0; i < SoundManager.JukeboxQueue.Length; i++)
		{
			SoundManager.JukeboxQueueInverse[SoundManager.JukeboxQueue[i].GetFile().GetBaseName().Replace(".", "_")] = i;
		}

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
			string fileName = SoundManager.JukeboxQueue[SoundManager.JukeboxIndex].Split("\\")[^1].TrimSuffix(".phxm").Replace(".", "_");
			Panel mapButton = MapListContainer.GetNode<Panel>(fileName);

			TargetScroll = Math.Clamp(mapButton.Position.Y + mapButton.Size.Y - WindowSize.Y / 2, 0, MaxScroll);
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
						SoundManager.JukeboxPaused = !SoundManager.JukeboxPaused;
						SoundManager.Song.PitchScale = SoundManager.JukeboxPaused ? 0.00000000001f : 1;	// bruh
						UpdateJukeboxButtons();
						break;
					case "Skip":
						SoundManager.JukeboxIndex++;
						SoundManager.PlayJukebox();
						break;
					case "Rewind":
						ulong now = Time.GetTicksMsec();

						if (now - SoundManager.LastRewind < 1000)
						{
							SoundManager.JukeboxIndex--;
							SoundManager.PlayJukebox();
						}
						else
						{
							SoundManager.Song.Seek(0);
						}

						SoundManager.LastRewind = now;
						break;
				}
			};
		}

		// Map selection

		PlayMenu = Menus.GetNode<Panel>("Play");
		SubTopBar = PlayMenu.GetNode<Panel>("SubTopBar");
		ImportButton = SubTopBar.GetNode<Button>("Import");
		UserFolderButton = SubTopBar.GetNode<Button>("UserFolder");
		SettingsButton = SubTopBar.GetNode<Button>("Settings");
		SearchEdit = SubTopBar.GetNode<LineEdit>("Search");
		SearchAuthorEdit = SubTopBar.GetNode<LineEdit>("SearchAuthor");
		ImportDialog = GetNode<FileDialog>("ImportDialog"); 
		MapList = PlayMenu.GetNode<ScrollContainer>("MapList");
		MapListContainer = MapList.GetNode<VBoxContainer>("Container");
		
		ImportButton.Pressed += ImportDialog.Show;
		UserFolderButton.Pressed += () => {
			OS.ShellOpen($"{Phoenyx.Constants.UserFolder}");
		};
		SettingsButton.Pressed += () => {
			SettingsManager.ShowSettings();
		};
 		SearchEdit.TextChanged += (string text) => {
			SearchTitle = text.ToLower();

			if (SearchTitle == "")
			{
				SearchEdit.ReleaseFocus();
			}

			Search();
		};
		SearchAuthorEdit.TextChanged += (string text) => {
			SearchAuthor = text.ToLower();

			if (SearchAuthor == "")
			{
				SearchAuthorEdit.ReleaseFocus();
			}

			Search();
		};
		ImportDialog.FilesSelected += (string[] files) => {
			MapParser.Import(files);
			UpdateMapList();
		};

		UpdateMapList();

		SearchEdit.Text = SearchTitle;

		foreach (Panel map in MapListContainer.GetChildren())
		{
			if (map.FindChild("Holder") == null)
			{
				continue;
			}

			map.Visible = map.GetNode("Holder").GetNode<Label>("Title").Text.ToLower().Contains(SearchTitle);
		}

		// Context Menu

		ContextMenu.GetNode("Container").GetNode<Button>("Favorite").Pressed += () => {
			ContextMenu.Visible = false;

			string favorites = File.ReadAllText($"{Phoenyx.Constants.UserFolder}/favorites.txt");
			bool favorited = favorites.Split("\n").ToList().Contains(ContextMenuTarget);
			TextureRect favorite = MapListContainer.GetNode(ContextMenuTarget).GetNode("Holder").GetNode<TextureRect>("Favorited");
			favorite.Visible = !favorited;

			if (favorited)
			{
				File.WriteAllText($"{Phoenyx.Constants.UserFolder}/favorites.txt", favorites.Replace($"{ContextMenuTarget}\n", ""));
				FavoritedMaps.Remove(ContextMenuTarget);
			}
			else
			{
				favorite.Texture = Phoenyx.Skin.FavoriteImage;
				File.WriteAllText($"{Phoenyx.Constants.UserFolder}/favorites.txt", $"{favorites}{ContextMenuTarget}\n");
				FavoritedMaps.Add(ContextMenuTarget);
			}
			
			MapListContainer.MoveChild(MapListContainer.GetNode(ContextMenuTarget), favorited ? OriginalMapOrder[ContextMenuTarget] : 0);

			ToastNotification.Notify($"Successfully {(favorited ? "removed" : "added")} map {(favorited ? "from" : "to")} favorites");
		};
		ContextMenu.GetNode("Container").GetNode<Button>("Delete").Pressed += () => {
			ContextMenu.Visible = false;
			MapListContainer.GetNode(ContextMenuTarget).QueueFree();
			LoadedMaps.Remove(ContextMenuTarget);
			
			if (ContextMenuTarget.Contains(SearchTitle))
			{
				VisibleMaps--;
			}

			File.Delete($"{Phoenyx.Constants.UserFolder}/maps/{ContextMenuTarget}.phxm");
			
			if (Directory.Exists($"{Phoenyx.Constants.UserFolder}/cache/maps/{ContextMenuTarget}"))
			{
				foreach (string file in Directory.GetFiles($"{Phoenyx.Constants.UserFolder}/cache/maps/{ContextMenuTarget}"))
				{
					File.Delete(file);
				}

				Directory.Delete($"{Phoenyx.Constants.UserFolder}/cache/maps/{ContextMenuTarget}");
			}

			ToastNotification.Notify("Successfuly deleted map");
		};
		ContextMenu.GetNode("Container").GetNode<Button>("VideoAdd").Pressed += () => {
			ContextMenu.Visible = false;
			GetNode<FileDialog>("VideoDialog").Visible = true;
		};
		ContextMenu.GetNode("Container").GetNode<Button>("VideoRemove").Pressed += () => {
			ContextMenu.Visible = false;
			Map map = MapParser.Decode($"{Phoenyx.Constants.UserFolder}/maps/{ContextMenuTarget}.phxm");

			File.Delete($"{Phoenyx.Constants.UserFolder}/maps/{ContextMenuTarget}.phxm");

			map.VideoBuffer = null;

			MapParser.Encode(map);

			if (Directory.Exists($"{Phoenyx.Constants.UserFolder}/cache/maps/{ContextMenuTarget}"))
			{
				foreach (string filePath in Directory.GetFiles($"{Phoenyx.Constants.UserFolder}/cache/maps/{ContextMenuTarget}"))
				{
					File.Delete(filePath);
				}

				Directory.Delete($"{Phoenyx.Constants.UserFolder}/cache/maps/{ContextMenuTarget}");
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
			Map map = MapParser.Decode($"{Phoenyx.Constants.UserFolder}/maps/{ContextMenuTarget}.phxm");

			File.Delete($"{Phoenyx.Constants.UserFolder}/maps/{ContextMenuTarget}.phxm");

			map.VideoBuffer = videoBuffer;

			MapParser.Encode(map);

			if (Directory.Exists($"{Phoenyx.Constants.UserFolder}/cache/maps/{ContextMenuTarget}"))
			{
				foreach (string filePath in Directory.GetFiles($"{Phoenyx.Constants.UserFolder}/cache/maps/{ContextMenuTarget}"))
				{
					File.Delete(filePath);
				}

				Directory.Delete($"{Phoenyx.Constants.UserFolder}/cache/maps/{ContextMenuTarget}");
			}

			ToastNotification.Notify("Successfully added video to map");
		};

		// Extras

		Button soundSpace = Extras.GetNode<Button>("SoundSpace");
		
		soundSpace.MouseEntered += () => {
			soundSpace.GetNode<RichTextLabel>("RichTextLabel").Text = "[center][color=ffffff40]Inspired by [color=ffffff80]Sound Space";
		};
		soundSpace.MouseExited += () => {
			soundSpace.GetNode<RichTextLabel>("RichTextLabel").Text = "[center][color=ffffff40]Inspired by Sound Space";
		};
		soundSpace.Pressed += () => {
			OS.ShellOpen("https://www.roblox.com/games/2677609345");
		};

		// Multiplayer

		MultiplayerHolder = PlayMenu.GetNode<Panel>("Multiplayer");
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

		if (!SoundManager.Song.Playing)
		{
			SoundManager.PlayJukebox();
			SoundManager.JukeboxPaused = !Phoenyx.Settings.AutoplayJukebox;
		}
		else
		{
			Jukebox.GetNode<Label>("Title").Text = Runner.CurrentAttempt.Map.PrettyTitle;
			SoundManager.JukeboxPaused = false;
			CurrentMap = Runner.CurrentAttempt.Map;

			Transition("Play", true);
		}

		SoundManager.Song.PitchScale = SoundManager.JukeboxPaused ? 0.00000000001f : 1;	// bruh
		
		UpdateJukeboxButtons();
		UpdateSpectrumSpacing();

		SoundManager.Song.VolumeDb = -180;
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
		
		if (SoundManager.Song.Stream != null)
		{
			JukeboxProgress.AnchorRight = (float)Math.Clamp(SoundManager.Song.GetPlaybackPosition() / SoundManager.Song.Stream.GetLength(), 0, 1);
			SoundManager.Song.VolumeDb = Mathf.Lerp(SoundManager.Song.VolumeDb, Phoenyx.Util.Quitting ? -80 : -80 + 70 * (float)Math.Pow(Phoenyx.Settings.VolumeMusic / 100, 0.1) * (float)Math.Pow(Phoenyx.Settings.VolumeMaster / 100, 0.1), (float)Math.Clamp(delta * 2, 0, 1));
		}

		float prevHz = 0;
		
		for (int i = 0; i < 32; i++)
		{
			float hz = (i + 1) * 4000 / 32;
			float magnitude = AudioSpectrum.GetMagnitudeForFrequencyRange(prevHz, hz).Length();
			float energy = (60 + Mathf.LinearToDb(magnitude)) / 30;
			prevHz = hz;
			
			ColorRect colorRect = JukeboxSpectrum.GetNode((i + 1).ToString()).GetNode<ColorRect>("Main");
			colorRect.AnchorTop = Math.Clamp(Mathf.Lerp(colorRect.AnchorTop, 1 - energy * (SoundManager.JukeboxPaused ? 0.0000000001f : 1), (float)delta * 12), 0, 1);	// oh god not again
		}

		for (int i = 0; i < FavoritedMaps.Count; i++)
		{
			TextureRect favoriteRect = MapListContainer.GetNode(FavoritedMaps[i]).GetNode("Holder").GetNode<TextureRect>("Favorited");
			Color modulate = Color.FromHtml("ffffff" + (196 + (int)(59 * Math.Sin(Math.PI * now / 1000000 + i))).ToString("X2"));
			favoriteRect.Rotation = (float)now / 1000000;
			favoriteRect.Modulate = modulate;
		}

		foreach (ColorRect tile in BackgroundTiles)
		{
			tile.Color = tile.Color.Lerp(Color.Color8(255, 255, 255, 0), (float)delta * 8);
		}

		for (int i = PassedNotes; i < CurrentMap.Notes.Length; i++)
		{
			if (CurrentMap.Notes[i].Millisecond > SoundManager.Song.GetPlaybackPosition() * 1000)
			{
				break;
			}

			Vector2I pos = new(Math.Clamp((int)Math.Floor(CurrentMap.Notes[i].X + 1.5), 0, 2), Math.Clamp((int)Math.Floor(CurrentMap.Notes[i].Y + 1.5), 0, 2));
			int tile = 0;
			
			tile += pos.X;
			tile += 3 * pos.Y;

			(BackgroundTiles[tile] as ColorRect).Color = Color.Color8(255, 255, 255, 12);

			PassedNotes = i + 1;
		}

		Main.Position = Main.Position.Lerp((Size / 2 - MousePosition) * (4 / Size.Y), Math.Min(1, (float)delta * 16));
		Extras.Position = Main.Position;
    }

    public override void _Input(InputEvent @event)
    {
		if (@event is InputEventKey eventKey && eventKey.Pressed)
		{
			if (eventKey.AsText() == PeruSequence[PeruSequenceIndex])
			{
				PeruSequenceIndex++;
				
				if (PeruSequenceIndex >= 4)
				{
					PeruSequenceIndex = 0;
					Peruchor.Visible = true;

					Tween tween = Peruchor.CreateTween();
					tween.TweenProperty(Peruchor, "modulate", Color.Color8(255, 255, 255, 255), 3);
					tween.Play();
				}
			}
			else
			{
				PeruSequenceIndex = 0;
			}

			switch (eventKey.Keycode)
			{
				case Key.Space:
					if (SelectedMap != null && !SearchEdit.HasFocus() && !SearchAuthorEdit.HasFocus())
					{
						Map map = MapParser.Decode($"{Phoenyx.Constants.UserFolder}/maps/{SelectedMap}.phxm");

						SoundManager.Song.Stop();
						SceneManager.Load("res://scenes/game.tscn");
						Runner.Play(map, Lobby.Speed, Lobby.Mods);
					}
					break;
				case Key.Mediaplay:
					SoundManager.JukeboxPaused = !SoundManager.JukeboxPaused;
					SoundManager.Song.PitchScale = SoundManager.JukeboxPaused ? 0.00000000001f : 1;	// bruh
					UpdateJukeboxButtons();
					break;
				case Key.Medianext:
					SoundManager.JukeboxIndex++;
					SoundManager.PlayJukebox();
					break;
				case Key.Mediaprevious:
					ulong now = Time.GetTicksMsec();

					if (now - SoundManager.LastRewind < 1000)
					{
						SoundManager.JukeboxIndex--;
						SoundManager.PlayJukebox();
					}
					else
					{
						SoundManager.Song.Seek(0);
					}

					SoundManager.LastRewind = now;
					break;
				default:
					if (SettingsManager.FocusedLineEdit == null && !SearchAuthorEdit.HasFocus() && !eventKey.CtrlPressed && !eventKey.AltPressed && eventKey.Keycode != Key.Ctrl && eventKey.Keycode != Key.Shift && eventKey.Keycode != Key.Alt && eventKey.Keycode != Key.Escape && eventKey.Keycode != Key.Enter && eventKey.Keycode != Key.F11)
					{
						SearchEdit.GrabFocus();
					}
					break;
			}
		}
		else if (@event is InputEventMouseButton eventMouseButton)
		{
			if (!SettingsManager.Shown && !eventMouseButton.CtrlPressed)
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
					case MouseButton.Xbutton1:
						if (eventMouseButton.Pressed && CurrentMenu != "Main")
						{
							Transition("Main");
						}
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
						SettingsManager.ShowSettings(!SettingsManager.Shown);
						break;
				}
			}

			switch (eventKey.Keycode)
			{
				case Key.Escape:
					if (SettingsManager.Shown)
					{
						SettingsManager.HideSettings();
					}
					else
					{
						if (CurrentMenu != "Main")
						{
							Transition("Main");
						}
						else
						{
							Control.GetTree().Root.PropagateNotification((int)NotificationWMCloseRequest);
						}
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

	public static void Search()
	{
		VisibleMaps = 0;

		foreach (Panel map in MapListContainer.GetChildren())
		{
			map.Visible = map.GetNode("Holder").GetNode<Label>("Title").Text.ToLower().Contains(SearchTitle) && map.GetNode("Holder").GetNode<RichTextLabel>("Extra").Text.ToLower().Contains(SearchAuthor);

			if (map.Visible)
			{
				VisibleMaps++;
			}
		}

		UpdateMaxScroll();
	}

	public static void UpdateVolume()
	{
		SettingsManager.Holder.GetNode("Categories").GetNode("Audio").GetNode("Container").GetNode("VolumeMaster").GetNode<HSlider>("HSlider").Value = Phoenyx.Settings.VolumeMaster;
	}

    public static void UpdateMapList()
	{
		double start = Time.GetTicksUsec();
		int i = 0;
		Color black = Color.Color8(0, 0, 0, 1);
		List<string> favorites = File.ReadAllText($"{Phoenyx.Constants.UserFolder}/favorites.txt").Split("\n").ToList();
		FavoritedMaps = [];

		foreach (string mapFile in Directory.GetFiles($"{Phoenyx.Constants.UserFolder}/maps"))
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
				string difficultyName;
				string mappers = "";
				int difficulty;
				string coverFile = null;

				if (!Directory.Exists($"{Phoenyx.Constants.UserFolder}/cache/maps/{fileName}"))
				{
					Directory.CreateDirectory($"{Phoenyx.Constants.UserFolder}/cache/maps/{fileName}");
					Map map = MapParser.Decode(mapFile, false);

					File.WriteAllText($"{Phoenyx.Constants.UserFolder}/cache/maps/{fileName}/metadata.json", map.EncodeMeta());

					//if (map.CoverBuffer != null)
					//{
					//	Godot.FileAccess cover = Godot.FileAccess.Open($"{Phoenyx.Constants.UserFolder}/cache/maps/{fileName}/cover.png", Godot.FileAccess.ModeFlags.Write);
					//	cover.StoreBuffer(map.CoverBuffer);
					//	cover.Close();
					//	
					//	Image coverImage = Image.LoadFromFile($"{Phoenyx.Constants.UserFolder}/cache/maps/{fileName}/cover.png");
					//	coverImage.Resize(128, 128);
					//	coverImage.SavePng($"{Phoenyx.Constants.UserFolder}/cache/maps/{fileName}/cover.png");
					//	coverFile = $"{Phoenyx.Constants.UserFolder}/cache/maps/{fileName}/cover.png";
					//}

					title = map.PrettyTitle;
					difficultyName = map.DifficultyName;
					mappers = map.PrettyMappers;
					difficulty = map.Difficulty;
				}
				else
				{
					Godot.FileAccess metaFile = Godot.FileAccess.Open($"{Phoenyx.Constants.UserFolder}/cache/maps/{fileName}/metadata.json", Godot.FileAccess.ModeFlags.Read);
					Godot.Collections.Dictionary metadata = (Godot.Collections.Dictionary)Json.ParseString(Encoding.UTF8.GetString(metaFile.GetBuffer((long)metaFile.GetLength())));
					metaFile.Close();

					//if (File.Exists($"{Phoenyx.Constants.UserFolder}/cache/maps/{fileName}/cover.png"))
					//{
					//	coverFile = $"{Phoenyx.Constants.UserFolder}/cache/maps/{fileName}/cover.png";
					//}

					foreach (string mapper in (string[])metadata["Mappers"])
					{
						mappers += $"{mapper}, ";
					}

					mappers = mappers.Substr(0, mappers.Length - 2);
					difficultyName = (string)metadata["DifficultyName"];
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
				holder.GetNode<RichTextLabel>("Extra").Text = $"[color={Phoenyx.Constants.SecondaryDifficultyColours[difficulty].ToHtml(false)}]{difficultyName}[color=808080] - {mappers}".ReplaceLineEndings("");

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

							Tween deselectTween = selectedHolder.CreateTween().SetParallel();
							deselectTween.TweenProperty(selectedHolder, "size", new Vector2(MapListContainer.Size.X - 60, selectedHolder.Size.Y), 0.25).SetTrans(Tween.TransitionType.Quad);
							deselectTween.TweenProperty(selectedHolder, "position", new Vector2(60, selectedHolder.Position.Y), 0.25).SetTrans(Tween.TransitionType.Quad);
							deselectTween.Play();

							if (MapListContainer.GetNode(SelectedMap) == mapButton)
							{
								Map map = MapParser.Decode(mapFile);

								SoundManager.Song.Stop();
								SceneManager.Load("res://scenes/game.tscn");
								Runner.Play(map, Lobby.Speed, Lobby.Mods);
							}
						}

						holder.GetNode<Panel>("Normal").Visible = false;
						holder.GetNode<Panel>("Selected").Visible = true;
						
						Tween selectTween = holder.CreateTween().SetParallel();
						selectTween.TweenProperty(holder, "size", new Vector2(MapListContainer.Size.X, holder.Size.Y), 0.25).SetTrans(Tween.TransitionType.Quad);
						selectTween.TweenProperty(holder, "position", new Vector2(0, holder.Position.Y), 0.25).SetTrans(Tween.TransitionType.Quad);
						selectTween.Play();

						TargetScroll = Math.Clamp(mapButton.Position.Y + mapButton.Size.Y - WindowSize.Y / 2, 0, MaxScroll);

						int index = SoundManager.JukeboxQueueInverse[mapButton.Name];

						if (SoundManager.JukeboxIndex != index)
						{
							SoundManager.JukeboxIndex = index;
							SoundManager.JukeboxPaused = false;
							SoundManager.Song.PitchScale = 1;
							SoundManager.PlayJukebox();
							UpdateJukeboxButtons();
						}

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

	public static void UpdateJukeboxButtons()
	{
		Jukebox.GetNode<TextureButton>("Pause").TextureNormal = SoundManager.JukeboxPaused ? Phoenyx.Skin.JukeboxPlayImage : Phoenyx.Skin.JukeboxPauseImage;
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

	private static void Transition(string menuName, bool instant = false)
	{
		CurrentMenu = menuName;

		switch (CurrentMenu)
		{
			case "Main":
				Phoenyx.Util.DiscordRPC.Call("Set", "details", "Main Menu");
				break;
			case "Play":
				Phoenyx.Util.DiscordRPC.Call("Set", "details", "Browsing Maps");
				break;
			case "Extras":
				Phoenyx.Util.DiscordRPC.Call("Set", "details", "Extras");
				break;
		}

		if (SettingsManager.FocusedLineEdit != null)
		{
			SettingsManager.FocusedLineEdit.ReleaseFocus();
		}

		Tween outTween = Control.CreateTween();
		
		foreach (Panel menu in Menus.GetChildren())
		{
			if (menu.Name == CurrentMenu)
			{
				continue;
			}
			outTween.Parallel().TweenProperty(menu, "modulate", Color.Color8(255, 255, 255, 0), instant ? 0 : 0.15).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
		}

		outTween.TweenCallback(Callable.From(() => {
			foreach (Panel menu in Menus.GetChildren())
			{
				if (menu.Name == CurrentMenu)
				{
					continue;
				}
				menu.Visible = false;
			}
		}));
		outTween.Play();

		Panel inMenu = Menus.GetNode<Panel>(menuName);
		inMenu.Visible = true;

		Tween inTween = Control.CreateTween();
		inTween.TweenProperty(inMenu, "modulate", Color.Color8(255, 255, 255, 255), instant ? 0 : 0.15).SetTrans(Tween.TransitionType.Quad);
		inTween.Play();
	}
}