using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
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

	private static Button ImportButton;
	private static Button UserFolderButton;
	private static Button SettingsButton;
	private static LineEdit Search;
	private static FileDialog FileDialog;
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
	private static double LastFrame = Time.GetTicksUsec();
	private static float Scroll = 0;
	private static float TargetScroll = 0;
	private static int MaxScroll = 0;
	private static Vector2 MousePosition = Vector2.Zero;
	private static bool RightMouseHeld = false;
	private static List<string> LoadedMaps = new();
	private static Panel SelectedMap = null;
	private static bool SettingsShown = false;

	public override void _Ready()
	{
		Control = this;
		SceneManager.Scene = this;
	
		Util.Setup();

		Util.DiscordRPC.Call("Set", "details", "In the menu");
		Util.DiscordRPC.Call("Set", "state", "");
		Util.DiscordRPC.Call("Set", "end_timestamp", 0);

		Input.MouseMode = Input.MouseModeEnum.Visible;
		DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);

		GetTree().AutoAcceptQuit = false;
		
		if (!Initialized)
		{
			Initialized = true;

			GetViewport().SizeChanged += () => {
				UpdateMaxScroll();
				TargetScroll = Math.Clamp(TargetScroll, 0, MaxScroll);
			};
			GetViewport().Connect("files_dropped", Callable.From((string[] files) => {
				MapParser.Import(files);
				UpdateMapList();
			}));
		}

		// General

		Cursor = GetNode<TextureRect>("Cursor");
		TopBar = GetNode<Panel>("TopBar");
		LoadedMaps = new();
		SelectedMap = null;

		Cursor.Texture = ImageTexture.CreateFromImage(Image.LoadFromFile($"{Constants.UserFolder}/skins/{Settings.Skin}/cursor.png"));

		// Map selection

		ImportButton = TopBar.GetNode<Button>("Import");
		UserFolderButton = TopBar.GetNode<Button>("UserFolder");
		SettingsButton = TopBar.GetNode<Button>("Settings");
		Search = TopBar.GetNode<LineEdit>("Search");
		FileDialog = GetNode<FileDialog>("FileDialog"); 
		MapList = GetNode<ScrollContainer>("MapList");
		MapListContainer = MapList.GetNode<VBoxContainer>("Container");
		
		ImportButton.Pressed += FileDialog.Show;
		UserFolderButton.Pressed += () => {
			OS.ShellOpen($"{Constants.UserFolder}");
		};
		SettingsButton.Pressed += () => {
			ShowSettings();
		};
 		Search.TextChanged += (string text) => {
			text = text.ToLower();

			if (text == "")
			{
				Search.ReleaseFocus();
			}

			foreach (Panel map in MapListContainer.GetChildren())
			{
				map.Visible = map.GetNode<Label>("Title").Text.ToLower().Contains(text);
			}
		};
		FileDialog.FilesSelected += (string[] files) => {
			//if (Lobby.PlayerCount > 0)
			//{
			//	Lobby.Map = MapParser.Decode(path);
			//	string[] split = path.Split("\\");
			//	
			//	ClientManager.Node.Rpc("ReceiveMapName", split[split.Length - 1]);
			//
			//	Lobby.Ready("1");
			//
			//	Lobby.AllReady += () => {
			//		ClientManager.Node.Rpc("ReceiveAllReady", split[split.Length - 1], Lobby.Speed, Lobby.Mods);
			//	};
			//}
			//else
			//{

			MapParser.Import(files);

			UpdateMapList();
			
			//Map map = MapParser.Decode(path);
			//
			//SceneManager.Load("res://scenes/game.tscn");
			//Game.Play(map, Lobby.Speed, Lobby.Mods);

			//}
		};

		UpdateMapList();

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
					lineEdit.Text = slider.Value.ToString();

					slider.ValueChanged += (double value) => {
						lineEdit.Text = value.ToString();

						UpdateSetting(option.Name, value);
					};
					lineEdit.TextSubmitted += (string text) => {
						try
						{
							if (text == "")
							{
								text = lineEdit.PlaceholderText;
								lineEdit.Text = text;
							}

							double value = text.ToFloat();

							slider.Value = value;

							UpdateSetting(option.Name, value);
						}
						catch (Exception exception)
						{
							ToastNotification.Notify($"Incorrect format; {exception.Message}", 2);
						}
					};
				}
				else if (option.FindChild("CheckButton") != null)
				{
					CheckButton checkButton = option.GetNode<CheckButton>("CheckButton");
					
					checkButton.ButtonPressed = (bool)property.GetValue(new());
					checkButton.Toggled += (bool value) => {
						switch (option.Name)
						{
							case "CameraLock":
								Settings.CameraLock = value;
								break;
							case "FadeOut":
								Settings.FadeOut = value;
								break;
							case "Pushback":
								Settings.Pushback = value;
								break;
							case "Fullscreen":
								DisplayServer.WindowSetMode(value ? DisplayServer.WindowMode.ExclusiveFullscreen : DisplayServer.WindowMode.Windowed);
								break;
						}
					};
				}
			}
		}

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
	}

    public override void _Process(double delta)
    {
		double now = Time.GetTicksUsec();
        delta = (now - LastFrame) / 1000000;

		Scroll = Mathf.Lerp(Scroll, TargetScroll, 8 * (float)delta);
		MapList.ScrollVertical = (int)Scroll;
		Cursor.Position = MousePosition - new Vector2(12, 12);

		LastFrame = now;
    }

    public override void _Input(InputEvent @event)
    {
		if (@event is InputEventMouseButton eventMouseButton)
		{
			if (!SettingsShown)
			{
				switch (eventMouseButton.ButtonIndex)
				{
					case MouseButton.Right:
						TargetScroll = Math.Clamp((MousePosition.Y - 50) / (DisplayServer.WindowGetSize().Y - 100), 0, 1) * MaxScroll;
						RightMouseHeld = eventMouseButton.Pressed;
						break;
					case MouseButton.WheelUp:
						TargetScroll = Math.Max(0, TargetScroll - 80);
						break;
					case MouseButton.WheelDown:
						TargetScroll = Math.Min(MaxScroll, TargetScroll + 80);
						break;
				}
			}
		}
		else if (@event is InputEventMouseMotion eventMouseMotion)
		{
			MousePosition = eventMouseMotion.Position;

			if (RightMouseHeld)
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
			else
			{
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
					default:
						if (eventKey.Keycode != Key.Ctrl && eventKey.Keycode != Key.Shift && eventKey.Keycode != Key.Alt)
						{
							Search.GrabFocus();
						}
						break;
				}
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
		tween.TweenCallback(Callable.From(() => {
			parent.Visible = SettingsShown;
		}));
		tween.Play();
	}

	public static void HideSettings()
	{
		ShowSettings(false);
	}

	public static void UpdateSetting(string setting, double value)
	{
		switch (setting)
		{
			case "Sensitivity":
				Settings.Sensitivity = value;
				break;
			case "ApproachRate":
				Settings.ApproachRate = value;
				Settings.ApproachTime = Settings.ApproachDistance / Settings.ApproachRate;
				break;
			case "ApproachDistance":
				Settings.ApproachDistance = value;
				Settings.ApproachTime = Settings.ApproachDistance / Settings.ApproachRate;
				break;
			case "FadeIn":
				Settings.FadeIn = value;
				break;
			case "Parallax":
				Settings.Parallax = value;
				break;
			case "FoV":
				Settings.FoV = value;
				break;
			case "VolumeMaster":
				Settings.VolumeMaster = value;
				break;
			case "VolumeMusic":
				Settings.VolumeMusic = value;
				break;
			case "VolumeSFX":
				Settings.VolumeSFX = value;
				break;
			case "NoteSize":
				Settings.NoteSize = value;
				break;
		}
	}

    public static void UpdateMapList()
	{
		double start = Time.GetTicksUsec();
		
		foreach (string mapFile in Directory.GetFiles($"{Constants.UserFolder}/maps"))
		{
			string[] split = mapFile.Split("\\");
			string fileName = split[split.Length - 1].Replace(".phxm", "");
			
			if (mapFile.GetExtension() != "phxm" || LoadedMaps.Contains(fileName))
			{
				continue;
			}

			string title;
			string extra;
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
				extra = $"{((string)metadata["DifficultyName"] == "N/A" ? Constants.Difficulties[(int)metadata["Difficulty"]] : metadata["DifficultyName"])} - {mappers}";
				title = (string)metadata["Artist"] != "" ? $"{(string)metadata["Artist"]} - {(string)metadata["Title"]}" : (string)metadata["Title"];
			}

			LoadedMaps.Add(fileName);

			Panel mapButton = MapButton.Instantiate<Panel>();

			if (coverFile != null)
			{
				mapButton.GetNode<TextureRect>("Cover").Texture = ImageTexture.CreateFromImage(Image.LoadFromFile(coverFile));
			}

			mapButton.Name = fileName;
			mapButton.GetNode<Label>("Title").Text = title;
			mapButton.GetNode<Label>("Extra").Text = extra;
			MapListContainer.AddChild(mapButton);

			mapButton.GetNode<Button>("Button").MouseEntered += () => {
				mapButton.GetNode<ColorRect>("Select").Color = Color.FromHtml("#ffffff10");
			};

			mapButton.GetNode<Button>("Button").MouseExited += () => {
				mapButton.GetNode<ColorRect>("Select").Color = Color.FromHtml("#ffffff00");
			};

			mapButton.GetNode<Button>("Button").Pressed += () => {
				if (SelectedMap != null)
				{
					SelectedMap.SelfModulate = Color.FromHtml("#ffffff");

					if (SelectedMap == mapButton)
					{
						Map map = MapParser.Decode(mapFile);

						SceneManager.Load("res://scenes/game.tscn");
						Game.Play(map, Lobby.Speed, Lobby.Mods);
					}
				}

				SelectedMap = mapButton;
				SelectedMap.SelfModulate = Color.FromHtml("#ffb29b");
			};
		}

		UpdateMaxScroll();

		Logger.Log($"MAPLIST UPDATE: {(Time.GetTicksUsec() - start) / 1000}ms");
	}

	public static void UpdateMaxScroll()
	{
		MaxScroll = (int)(LoadedMaps.Count * 90 - MapList.Size.Y);	// button.Size.Y + padding
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
		Util.SaveSettings();
		Util.DiscordRPC.Call("Clear");
		Control.GetTree().Quit();
	}
}