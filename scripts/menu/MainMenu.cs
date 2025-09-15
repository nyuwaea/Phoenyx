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
	private static readonly PackedScene LeaderboardScore = GD.Load<PackedScene>("res://prefabs/leaderboard_score.tscn");

	private static Panel TopBar;
	private static ColorRect Background;
	private static Node[] BackgroundTiles;
	private static Panel Menus;
	private static Panel Main;
	private static Panel Jukebox;
	private static Button JukeboxButton;
	private static ColorRect JukeboxProgress;
	private static HBoxContainer JukeboxSpectrum;
	private static ColorRect[] JukeboxSpectrumBars;
	private static AudioEffectSpectrumAnalyzerInstance AudioSpectrum;
	private static Panel ContextMenu;
	private static TextureRect Peruchor;
	private static ShaderMaterial MainBackgroundMaterial;

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
	private static Panel LeaderboardPanel;
	private static VBoxContainer LeaderboardContainer;
	private static Panel ModifiersPanel;
	private static Panel SpeedPanel;
	private static HSlider SpeedSlider;
	private static LineEdit SpeedEdit;
	private static Panel StartFromPanel;
	private static HSlider StartFromSlider;
	private static LineEdit StartFromEdit;
	private static List<TextureButton> ModifierButtons;

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
	private static Dictionary<string, int> MapsOrder = [];
	private static Dictionary<Panel, bool> FavoritedMaps = [];
	private static TextureRect[] FavoriteMapsTextures = [];
	private static int VisibleMaps = 0;
	private static string SelectedMapID = null;
	private static Map SelectedMap = new();
	private static string CurrentMenu = "Main";
	private static string LastMenu = CurrentMenu;
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
			SoundManager.JukeboxPlayed += (Map map) => {
				PassedNotes = 0;
				CurrentMap = map;
			};

			Viewport viewport = GetViewport();
			viewport.SizeChanged += () => {
				if (SceneManager.Scene.Name != "SceneMenu")
				{
					return;
				}

				WindowSize = DisplayServer.WindowGetSize();
				UpdateMaxScroll();
				TargetScroll = Math.Clamp(TargetScroll, 0, MaxScroll);
				UpdateSpectrumSpacing();
			};
			viewport.Connect("files_dropped", Callable.From((string[] files) => {
				Import(files);

				foreach (string file in files)
				{
					switch (file.GetExtension())
					{
						case "phxr":
							List<Replay> replays = [];
							List<Replay> matching = [];

							for (int i = 0; i < files.Length; i++)
							{
								Replay replay = new(files[i]);
								
								if (!replay.Valid)
								{
									continue;
								}

								replays.Add(replay);
							}

							if (replays.Count == 0)
							{
								ToastNotification.Notify("No valid replays", 2);
								return;
							}

							foreach (Replay replay in replays)
							{
								if (replay == replays[0])
								{
									matching.Add(replay);
								}
								else
								{
									ToastNotification.Notify("Replay doesn't match first", 1);
									Logger.Log($"Replay {replay} doesn't match {replays[0]}");
								}
							}

							if (Runner.Playing)
							{
								Runner.QueueStop();
							}
							
							SoundManager.Song.Stop();
							SceneManager.Load("res://scenes/game.tscn");
							Runner.Play(MapParser.Decode(matching[0].MapFilePath), matching[0].Speed, matching[0].StartFrom, matching[0].Modifiers, null, [.. matching]);
							break;
					}
				}
			}));
			
			//string[] args = OS.GetCmdlineArgs();
			string[] args = [
				"--a=\"H:\\Sound Space\\Quantum_Editors\\Sound Space Quantum Editor\\cached\\Camellia feat. Nanahira - べィスドロップ・フリークス (2018 Redrop ver.).asset\"",
				"--t=\"H:\\Sound Space\\Quantum_Editors\\Sound Space Quantum Editor\\assets\\temp\\tempmap.txt\""
			];
			
			if (args.Length > 0)
			{
				string audioString = "";
				string mapString = "";

				foreach (string arg in args)
				{
					switch (arg.Substr(0, 3))
					{
						case "--a":
							audioString = arg.Substr(5, arg.Length - 1).Trim('"');
							break;
						case "--t":
							mapString = arg.Substr(5, arg.Length - 1).Trim('"');
							break;
					}
				}

				//Select("tempmap", true, false);
				
				//Map map = MapParser.Decode(mapString, audioString, false, false);
				//map.Ephemeral = true;
				//SoundManager.Song.Stop();
				//SceneManager.Load("res://scenes/game.tscn");
			}
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
		MainBackgroundMaterial = Background.Material as ShaderMaterial;
		LoadedMaps = [];
		FavoritedMaps = [];

		Cursor.Texture = Phoenyx.Skin.CursorImage;
		Cursor.Size = new Vector2(32 * (float)Phoenyx.Settings.CursorScale, 32 * (float)Phoenyx.Settings.CursorScale);

		Godot.Collections.Array<Node> jukeboxBars = JukeboxSpectrum.GetChildren();

		JukeboxSpectrumBars = new ColorRect[jukeboxBars.Count];

		for (int i = 0; i < jukeboxBars.Count; i++)
		{
			JukeboxSpectrumBars[i] = jukeboxBars[i].GetNode<ColorRect>("Main");
		}

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

		JukeboxButton.MouseEntered += () => {
			Label title = Jukebox.GetNode<Label>("Title");
			Tween tween = title.CreateTween();
			tween.TweenProperty(title, "modulate", Color.Color8(255, 255, 255), 0.25).SetTrans(Tween.TransitionType.Quad);
			tween.Play();
		};
		JukeboxButton.MouseExited += () => {
			Label title = Jukebox.GetNode<Label>("Title");
			Tween tween = title.CreateTween();
			tween.TweenProperty(title, "modulate", Color.Color8(194, 194, 194), 0.25).SetTrans(Tween.TransitionType.Quad);
			tween.Play();
		};
		JukeboxButton.Pressed += () => {
			string fileName = SoundManager.JukeboxQueue[SoundManager.JukeboxIndex].GetFile().GetBaseName();
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
				tween.TweenProperty(button, "self_modulate", Color.Color8(255, 255, 255), 0.25).SetTrans(Tween.TransitionType.Quad);
				tween.Play();
			};
			button.MouseExited += () => {
				Tween tween = button.CreateTween();
				tween.TweenProperty(button, "self_modulate", Color.Color8(194, 194, 194), 0.25).SetTrans(Tween.TransitionType.Quad);
				tween.Play();
			};
			button.Pressed += () => {
				switch (button.Name)
				{
					case "Pause":
						SoundManager.JukeboxPaused = !SoundManager.JukeboxPaused;
						SoundManager.Song.PitchScale = SoundManager.JukeboxPaused ? 0.00000000001f : (float)Lobby.Speed;	// bruh
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
		LeaderboardPanel = PlayMenu.GetNode<Panel>("Leaderboard");
		LeaderboardContainer = LeaderboardPanel.GetNode("ScrollContainer").GetNode<VBoxContainer>("VBoxContainer");
		ModifiersPanel = PlayMenu.GetNode<Panel>("Modifiers");
		SpeedPanel = ModifiersPanel.GetNode<Panel>("Speed");
		SpeedSlider = SpeedPanel.GetNode<HSlider>("HSlider");
		SpeedEdit = SpeedPanel.GetNode<LineEdit>("LineEdit");
		StartFromPanel = ModifiersPanel.GetNode<Panel>("StartFrom");
		StartFromSlider = StartFromPanel.GetNode<HSlider>("HSlider");
		StartFromEdit = StartFromPanel.GetNode<LineEdit>("LineEdit");
		ModifierButtons = [];

		foreach (TextureButton mod in ModifiersPanel.GetNode("Decrease").GetChildren())
		{
			ModifierButtons.Add(mod);
		}

		foreach (TextureButton mod in ModifiersPanel.GetNode("Increase").GetChildren())
		{
			ModifierButtons.Add(mod);
		}
		
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
			Import(files);
		};

		UpdateMapList();

		SearchEdit.Text = SearchTitle;
		SearchAuthorEdit.Text = SearchAuthor;

		SearchEdit.ReleaseFocus();
		SearchAuthorEdit.ReleaseFocus();

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
			Panel mapButton = MapListContainer.GetNode<Panel>(ContextMenuTarget);
			TextureRect favorite = mapButton.GetNode("Holder").GetNode<TextureRect>("Favorited");
			favorite.Visible = !favorited;

			if (favorited)
			{
				File.WriteAllText($"{Phoenyx.Constants.UserFolder}/favorites.txt", favorites.Replace($"{ContextMenuTarget}\n", ""));
				FavoritedMaps[mapButton] = false;
			}
			else
			{
				favorite.Texture = Phoenyx.Skin.FavoriteImage;
				File.WriteAllText($"{Phoenyx.Constants.UserFolder}/favorites.txt", $"{favorites}{ContextMenuTarget}\n");
				FavoritedMaps[mapButton] = true;
			}
			
			SortMapList();
			UpdateFavoriteMapsTextures();

			ToastNotification.Notify($"Successfully {(favorited ? "removed" : "added")} map {(favorited ? "from" : "to")} favorites");
		};
		ContextMenu.GetNode("Container").GetNode<Button>("Delete").Pressed += () => {
			ContextMenu.Visible = false;

			Panel mapButton = MapListContainer.GetNode<Panel>(ContextMenuTarget);

			FavoritedMaps.Remove(mapButton);
			mapButton.QueueFree();
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

		// Modifiers

		SpeedSlider.ValueChanged += (double value) => {
			SpeedEdit.Text = value.ToString();
			Lobby.Speed = value / 100;

			if (!SoundManager.JukeboxPaused)
			{
				SoundManager.Song.PitchScale = (float)Lobby.Speed;
			}
		};
		SpeedEdit.TextSubmitted += (string text) => {
			SpeedSlider.Value = Math.Clamp(text.ToFloat(), 25, 1000);
			SpeedEdit.ReleaseFocus();
		};

		StartFromSlider.ValueChanged += (double value) => {
			value *= SelectedMap.Length;

			StartFromEdit.Text = $"{Lib.String.FormatTime(value / 1000)}";
			Lobby.StartFrom = Math.Floor(value);
		};
		StartFromSlider.DragEnded += (bool _) => {
			if (!SoundManager.JukeboxPaused)
			{
				SoundManager.Song.Seek((float)StartFromSlider.Value * SelectedMap.Length / 1000);
			}
		};
		StartFromEdit.TextSubmitted += (string text) => {
			StartFromSlider.Value = Math.Clamp(text.ToFloat(), 0, 1);
			StartFromEdit.ReleaseFocus();
		};

		foreach (TextureButton modButton in ModifierButtons)
		{
			modButton.SelfModulate = Color.Color8(255, 255, 255, (byte)(Lobby.Mods[modButton.Name] ? 255 : 128));
			modButton.Pressed += () => {
				Lobby.Mods[modButton.Name] = !Lobby.Mods[modButton.Name];

				Tween tween = modButton.CreateTween();
				tween.TweenProperty(modButton, "self_modulate", Color.Color8(255, 255, 255, (byte)(Lobby.Mods[modButton.Name] ? 255 : 128)), 1/4);
				tween.Play();
			};
		}
		
		SpeedSlider.Value = Lobby.Speed * 100;
		StartFromSlider.Value = Lobby.StartFrom / Math.Max(1, CurrentMap.Length);

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

		//MultiplayerHolder = PlayMenu.GetNode<Panel>("Multiplayer");
		//IPLine = MultiplayerHolder.GetNode<LineEdit>("IP");
		//PortLine = MultiplayerHolder.GetNode<LineEdit>("Port");
		//ChatLine = MultiplayerHolder.GetNode<LineEdit>("ChatInput");
		//Host = MultiplayerHolder.GetNode<Button>("Host");
		//Join = MultiplayerHolder.GetNode<Button>("Join");
		//ChatScrollContainer = MultiplayerHolder.GetNode<ScrollContainer>("Chat");
		//ChatHolder = ChatScrollContainer.GetNode<VBoxContainer>("Holder");
		//
		//Host.Pressed += () => {
		//	try
		//	{
		//		
		//	}
		//	catch (Exception exception)
		//	{
		//		ToastNotification.Notify($"{exception.Message}", 2);
		//		return;
		//	}
		//
		//	Host.Disabled = true;
		//	Join.Disabled = true;
		//};
		//Join.Pressed += () => {
		//	try
		//	{
		//		
		//	}
		//	catch (Exception exception)
		//	{
		//		ToastNotification.Notify($"{exception.Message}", 2);
		//		return;
		//	}
		//
		//	Host.Disabled = true;
		//	Join.Disabled = true;
		//};
		
		// Finish

		SoundManager.UpdateJukeboxQueue();
		SoundManager.JukeboxIndex = new Random().Next(0, SoundManager.JukeboxQueue.Length);

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

		SoundManager.Song.PitchScale = SoundManager.JukeboxPaused ? 0.00000000001f : (float)Lobby.Speed;	// bruh
		
		UpdateJukeboxButtons();
		UpdateSpectrumSpacing();
		UpdateLeaderboard();

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

			Search();

			if (SelectedMapID != null)
			{
				Panel selectedMapHolder = MapListContainer.GetNode(SelectedMapID).GetNode<Panel>("Holder");
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
			
			JukeboxSpectrumBars[i].AnchorTop = Math.Clamp(Mathf.Lerp(JukeboxSpectrumBars[i].AnchorTop, 1 - energy * (SoundManager.JukeboxPaused ? 0.0000000001f : 1), (float)delta * 12), 0, 1);	// oh god not again
		}

		for (int i = 0; i < FavoriteMapsTextures.Length; i++)
		{
			Color modulate = Color.Color8(255, 255, 255, (byte)(196 + (59 * Math.Sin(Math.PI * now / 1000000 + i))));
			FavoriteMapsTextures[i].Rotation = (float)now / 1000000;
			FavoriteMapsTextures[i].Modulate = modulate;
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

		if (Phoenyx.Util.Quitting)
		{
			MainBackgroundMaterial.SetShaderParameter("opaqueness", Mathf.Lerp((float)MainBackgroundMaterial.GetShaderParameter("opaqueness"), 0, delta * 8));
		}
		
		MainBackgroundMaterial.SetShaderParameter("window_position", DisplayServer.WindowGetPosition());
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
					if (SelectedMapID != null && !SearchEdit.HasFocus() && !SearchAuthorEdit.HasFocus())
					{
						Map map = MapParser.Decode($"{Phoenyx.Constants.UserFolder}/maps/{SelectedMapID}.phxm");

						SoundManager.Song.Stop();
						SceneManager.Load("res://scenes/game.tscn");
						Runner.Play(map, Lobby.Speed, Lobby.StartFrom, Lobby.Mods);
					}
					break;
				case Key.Mediaplay:
					SoundManager.JukeboxPaused = !SoundManager.JukeboxPaused;
					SoundManager.Song.PitchScale = SoundManager.JukeboxPaused ? 0.00000000001f : (float)Lobby.Speed;	// bruh
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
					if (SettingsManager.FocusedLineEdit == null && !SearchAuthorEdit.HasFocus() && !SpeedEdit.HasFocus() && !StartFromEdit.HasFocus() && !eventKey.CtrlPressed && !eventKey.AltPressed && eventKey.Keycode != Key.Ctrl && eventKey.Keycode != Key.Shift && eventKey.Keycode != Key.Alt && eventKey.Keycode != Key.Escape && eventKey.Keycode != Key.Enter && eventKey.Keycode != Key.F11)
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
					case MouseButton.Xbutton2:
						if (eventMouseButton.Pressed && CurrentMenu != LastMenu)
						{
							Transition(LastMenu);
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

	public static Dictionary<string, bool> Import(string[] files)
	{
		List<string> maps = [];

		foreach (string file in files)
		{
			if (file.GetExtension() == "phxm" || file.GetExtension() == "sspm" || file.GetExtension() == "txt")
			{
				maps.Add(file);
			}
		}

		Dictionary<string, bool> results = MapParser.BulkImport([.. maps]);

		if (maps.Count == 0)
		{
			return results;
		}

		SoundManager.UpdateJukeboxQueue();
		
		if (SceneManager.Scene.Name == "SceneMenu")
		{
			UpdateMapList();
			Search();
			Select(maps[0].GetFile().GetBaseName(), true);
		}

		return results;
	}

	public static void Search()
	{
		VisibleMaps = 0;

		foreach (Panel map in MapListContainer.GetChildren())
		{
			map.Visible = !Phoenyx.Constants.TempMapMode && map.GetNode("Holder").GetNode<Label>("Title").Text.ToLower().Contains(SearchTitle) && map.GetNode("Holder").GetNode<RichTextLabel>("Extra").Text.ToLower().Split(" - ")[^1].Contains(SearchAuthor);

			if (map.Visible)
			{
				MapsOrder[map.Name] = VisibleMaps;
				VisibleMaps++;
			}
		}

		UpdateMaxScroll();
		TargetScroll = Math.Clamp(TargetScroll, 0, MaxScroll);
	}

	public static void Select(string fileName, bool fromImport = false, bool selectInMapList = true)
	{
		if (selectInMapList)
		{
			Panel mapButton = MapListContainer.GetNode<Panel>(fileName);

			if (mapButton == null)
			{
				Logger.Log($"Tried to select map {fileName}, but it wasn't found in the map list");
				return;
			}

			Panel holder = mapButton.GetNode<Panel>("Holder");

			if (SelectedMapID != null)
			{
				Panel selectedHolder = MapListContainer.GetNode(SelectedMapID).GetNode<Panel>("Holder");
				selectedHolder.GetNode<Panel>("Normal").Visible = true;
				selectedHolder.GetNode<Panel>("Selected").Visible = false;

				Tween deselectTween = selectedHolder.CreateTween().SetParallel();
				deselectTween.TweenProperty(selectedHolder, "size", new Vector2(MapListContainer.Size.X - 60, selectedHolder.Size.Y), 0.25).SetTrans(Tween.TransitionType.Quad);
				deselectTween.TweenProperty(selectedHolder, "position", new Vector2(60, selectedHolder.Position.Y), 0.25).SetTrans(Tween.TransitionType.Quad);
				deselectTween.Play();

				if (MapListContainer.GetNode(SelectedMapID) == mapButton && !fromImport)
				{
					Map map = MapParser.Decode($"{Phoenyx.Constants.UserFolder}/maps/{fileName}.phxm");

					SoundManager.Song.Stop();
					SceneManager.Load("res://scenes/game.tscn");
					Runner.Play(map, Lobby.Speed, Lobby.StartFrom, Lobby.Mods);
				}
			}

			holder.GetNode<Panel>("Normal").Visible = false;
			holder.GetNode<Panel>("Selected").Visible = true;
			
			if (fromImport)
			{
				holder.CallDeferred("set_size", new Vector2(MapListContainer.Size.X, holder.Size.Y));
				holder.CallDeferred("set_position", new Vector2(0, holder.Position.Y));
			}
			else
			{
				Tween selectTween = holder.CreateTween().SetParallel();
				selectTween.TweenProperty(holder, "size", new Vector2(MapListContainer.Size.X, holder.Size.Y), 0.25).SetTrans(Tween.TransitionType.Quad);
				selectTween.TweenProperty(holder, "position", new Vector2(0, holder.Position.Y), 0.25).SetTrans(Tween.TransitionType.Quad);
				selectTween.Play();
			}

			TargetScroll = Math.Clamp((MapsOrder[fileName] + 1) * (mapButton.Size.Y + 10) - MapList.Size.Y / 2, 0, MaxScroll);
			
			int index = SoundManager.JukeboxQueueInverse[mapButton.Name];
			
			if (SoundManager.JukeboxIndex != index)
			{
				SoundManager.JukeboxIndex = index;
				SoundManager.JukeboxPaused = false;
				SoundManager.Song.PitchScale = (float)Lobby.Speed;
				SoundManager.PlayJukebox();
				UpdateJukeboxButtons();
			}
		}

		bool firstTimeSelected = fileName != SelectedMapID;

		if (SelectedMapID != fileName)
		{
			StartFromSlider.Value = 0;
			StartFromEdit.Text = "0:00";
			Lobby.StartFrom = 0;
		}

		SelectedMapID = fileName;
		SelectedMap = MapParser.Decode($"{Phoenyx.Constants.UserFolder}/maps/{SelectedMapID}.phxm");

		if (firstTimeSelected)
		{
			UpdateLeaderboard();
		}

		Transition("Play");
	}

	public static void SortMapList()
	{
		List<Node> favorites = [];
		string[] maps = Directory.GetFiles($"{Phoenyx.Constants.UserFolder}/maps");

		for (int i = 0; i < maps.Length; i++)
		{
			Node mapButton = MapListContainer.GetNode(maps[i].GetFile().GetBaseName());

			if (mapButton == null)
			{
				continue;
			}

			MapListContainer.MoveChild(mapButton, i);
		}

		foreach (KeyValuePair<Panel, bool> entry in FavoritedMaps)
		{
			if (!entry.Value)
			{
				continue;
			}

			favorites.Add(entry.Key);
		}

		for (int i = favorites.Count - 1; i >= 0; i--)
		{
			MapListContainer.MoveChild(favorites[i], 0);
		}

		Godot.Collections.Array<Node> mapButtons = MapListContainer.GetChildren();

		for (int i = 0; i < mapButtons.Count; i++)
		{
			MapsOrder[mapButtons[i].Name] = i;
		}
	}
	
	//public static void Chat(string message)
	//{
	//	Label chatMessage = ChatMessage.Instantiate<Label>();
	//	chatMessage.Text = message;
	//
	//	ChatHolder.AddChild(chatMessage);
	//	ChatScrollContainer.ScrollVertical += 100;
	//}
	//
	//private static void SendMessage()
	//{
	//	if (ChatLine.Text.Replace(" ", "") == "")
	//	{
	//		return;
	//	}
	//	
	//	ChatLine.Text = "";
	//}

	private static void Transition(string menuName, bool instant = false)
	{
		LastMenu = CurrentMenu;
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

	public static void UpdateVolume()
	{
		SettingsManager.Holder.GetNode("Categories").GetNode("Audio").GetNode("Container").GetNode("VolumeMaster").GetNode<HSlider>("HSlider").Value = Phoenyx.Settings.VolumeMaster;
	}

	public static void UpdateMapList()
	{
		double start = Time.GetTicksUsec();
		int i = 0;
		Color black = Color.Color8(0, 0, 0, 1);
		List<string> favorites = [.. File.ReadAllText($"{Phoenyx.Constants.UserFolder}/favorites.txt").Split("\n")];

		foreach (string mapFile in Directory.GetFiles($"{Phoenyx.Constants.UserFolder}/maps"))
		{
			try
			{
				string fileName = mapFile.GetFile().GetBaseName();

				if (LoadedMaps.Contains(fileName))
				{
					continue;
				}

				bool favorited = favorites.Contains(fileName);
				string title;
				string difficultyName;
				string mappers = "";
				int difficulty;
				string coverFile = null;

				if (!Directory.Exists($"{Phoenyx.Constants.UserFolder}/cache/maps/{fileName}"))
				{
					Directory.CreateDirectory($"{Phoenyx.Constants.UserFolder}/cache/maps/{fileName}");
					Map map = MapParser.Decode(mapFile, null, false);

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

				FavoritedMaps[mapButton] = favorited;
				
				if (coverFile != null)
				{
					holder.GetNode<TextureRect>("Cover").Texture = ImageTexture.CreateFromImage(Image.LoadFromFile(coverFile));
				}

				holder.GetNode<Label>("Title").Text = title;
				holder.GetNode<RichTextLabel>("Extra").Text = $"[color={Phoenyx.Constants.SecondaryDifficultyColours[difficulty].ToHtml(false)}]{difficultyName}[color=808080] - {mappers}".ReplaceLineEndings("");

				MapListContainer.AddChild(mapButton);
				mapButton.Name = fileName;

				if (favorited)
				{
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
						Select(fileName);
					}
					else
					{
						RightClickingButton = true;
						TargetScroll = Math.Clamp(mapButton.Position.Y + mapButton.Size.Y - WindowSize.Y / 2, 0, MaxScroll);
						ContextMenu.Visible = true;
						ContextMenu.Position = MousePosition;
						ContextMenuTarget = fileName;

						bool favorited = FavoritedMaps[mapButton];

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
		SortMapList();
		UpdateFavoriteMapsTextures();

		Logger.Log($"MAPLIST UPDATE: {(Time.GetTicksUsec() - start) / 1000}ms");
	}

	public static void UpdateLeaderboard()
	{
		foreach (Node child in LeaderboardContainer.GetChildren())
		{
			LeaderboardContainer.RemoveChild(child);
		}

		Leaderboard leaderboard = new();
		
		if (File.Exists($"{Phoenyx.Constants.UserFolder}/pbs/{SelectedMapID}"))
		{
			leaderboard = new(SelectedMapID, $"{Phoenyx.Constants.UserFolder}/pbs/{SelectedMapID}");
		}
		
		LeaderboardPanel.GetNode<Label>("NoScores").Visible = leaderboard.ScoreCount == 0;

		if (!leaderboard.Valid)
		{
			return;
		}

		int count = 0;

		foreach (Leaderboard.Score score in leaderboard.Scores)
		{
			Panel scorePanel = LeaderboardScore.Instantiate<Panel>();
			Label playerLabel = scorePanel.GetNode<Label>("Player");
			Label scoreLabel = scorePanel.GetNode<Label>("Score");

			playerLabel.Text = score.Player;
			scorePanel.GetNode<ColorRect>("Bright").Visible = (count + 1) % 2 == 0;
			scorePanel.GetNode<Label>("Accuracy").Text = $"{score.Accuracy.ToString().PadDecimals(2)}%";
			scorePanel.GetNode<Label>("Speed").Text = $"{score.Speed.ToString().PadDecimals(2)}x";
			scorePanel.GetNode<Label>("Time").Text = Lib.String.FormatUnixTimePretty(Time.GetUnixTimeFromSystem(), score.Time);

			if (score.Qualifies)
			{
				scoreLabel.Text = Lib.String.PadMagnitude(score.Value.ToString());
			}
			else
			{
				playerLabel.LabelSettings = playerLabel.LabelSettings.Duplicate() as LabelSettings;
				playerLabel.LabelSettings.FontColor = Color.Color8(255, 255, 255, 64);
				scoreLabel.LabelSettings = scoreLabel.LabelSettings.Duplicate() as LabelSettings;
				scoreLabel.LabelSettings.FontColor = Color.Color8(255, 255, 255, 64);
				scoreLabel.Text = $"{Lib.String.FormatTime(score.Progress / 1000)} / {Lib.String.FormatTime(score.MapLength / 1000)}";
			}

			scorePanel.GetNode<Button>("Button").Pressed += () => {
				if (File.Exists($"{Phoenyx.Constants.UserFolder}/replays/{score.AttemptID}.phxr"))
				{
					Replay replay = new($"{Phoenyx.Constants.UserFolder}/replays/{score.AttemptID}.phxr");
					SoundManager.Song.Stop();
					SceneManager.Load("res://scenes/game.tscn");
					Runner.Play(MapParser.Decode(replay.MapFilePath), replay.Speed, replay.StartFrom, replay.Modifiers, null, [replay]);
				}
			};

			HBoxContainer modifiersContainer = scorePanel.GetNode<HBoxContainer>("Modifiers");
			TextureRect modifierTemplate = modifiersContainer.GetNode<TextureRect>("ModifierTemplate");

			foreach (KeyValuePair<string, bool> entry in score.Modifiers)
			{
				if (entry.Value)
				{
					TextureRect mod = modifierTemplate.Duplicate() as TextureRect;
					mod.Texture = Phoenyx.Util.GetModIcon(entry.Key);
					mod.Visible = true;
					modifiersContainer.AddChild(mod);
				}
			}

			LeaderboardContainer.AddChild(scorePanel);
			count++;
		}
	}

	public static void UpdateMaxScroll()
	{
		MaxScroll = Math.Max(0, (int)(VisibleMaps * 90 - MapList.Size.Y));
	}

	public static void UpdateSpectrumSpacing()
	{
		JukeboxSpectrum.AddThemeConstantOverride("separation", ((int)JukeboxSpectrum.Size.X - 32 * 6) / 48);
	}

	public static void UpdateFavoriteMapsTextures()
	{
		List<Panel> favorites = [];

		foreach (KeyValuePair<Panel, bool> entry in FavoritedMaps)
		{
			if (entry.Value)
			{
				favorites.Add(entry.Key);
			}
		}

		FavoriteMapsTextures = new TextureRect[favorites.Count];

		for (int i = 0; i < favorites.Count; i++)
		{
			FavoriteMapsTextures[i] = favorites[i].GetNode("Holder").GetNode<TextureRect>("Favorited");
		}
	}

	public static void UpdateJukeboxButtons()
	{
		Jukebox.GetNode<TextureButton>("Pause").TextureNormal = SoundManager.JukeboxPaused ? Phoenyx.Skin.JukeboxPlayImage : Phoenyx.Skin.JukeboxPauseImage;
	}
}
