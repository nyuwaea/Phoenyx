using System;
using System.IO;
using System.Text.RegularExpressions;
using Godot;

public partial class SettingsManager : Control
{
	public static Control Control;

	public static ColorRect Settings;
	public static Panel Holder;
	public static bool Shown = false;
	public static LineEdit FocusedLineEdit = null;

	public override void _Ready()
	{
		Control = this;

		Settings = GD.Load<PackedScene>("res://prefabs//settings.tscn").Instantiate<ColorRect>();
		Holder = Settings.GetNode<Panel>("Holder");
		Settings.GetNode<Button>("Deselect").Pressed += HideSettings;

		AddChild(Settings);
		HideSettings();
		GetViewport().SizeChanged += () => {
			Settings.SetSize(DisplayServer.WindowGetSize());
		};

		Settings.SetSize(DisplayServer.WindowGetSize());

		foreach (Node holder in Holder.GetNode("Sidebar").GetNode("Container").GetChildren())
		{
			holder.GetNode<Button>("Button").Pressed += () => {
				foreach (ColorRect otherHolder in Holder.GetNode("Sidebar").GetNode("Container").GetChildren())
				{
					otherHolder.Color = Color.FromHtml($"#ffffff{(holder.Name == otherHolder.Name ? "08" : "00")}");
				}

				foreach (ScrollContainer category in Holder.GetNode("Categories").GetChildren())
				{
					category.Visible = category.Name == holder.Name;
				}
			};
		}

		OptionButton profiles = Holder.GetNode("Header").GetNode<OptionButton>("Profiles");
		LineEdit profileEdit = Holder.GetNode("Header").GetNode<LineEdit>("ProfileEdit");
		OptionButton skins = Holder.GetNode("Categories").GetNode("Visuals").GetNode("Container").GetNode("Skin").GetNode<OptionButton>("OptionsButton");
		OptionButton spaces = Holder.GetNode("Categories").GetNode("Visuals").GetNode("Container").GetNode("Space").GetNode<OptionButton>("OptionsButton");

		Holder.GetNode("Header").GetNode<Button>("CreateProfile").Pressed += () => {
			profileEdit.Visible = !profileEdit.Visible;
		};
		profileEdit.FocusEntered += () => FocusedLineEdit = profileEdit;
		profileEdit.FocusExited += () => FocusedLineEdit = null;
		profileEdit.TextSubmitted += (string text) => {
			text = new Regex("[^a-zA-Z0-9_ -]").Replace(text.Replace(" ", "_"), "");

			profileEdit.ReleaseFocus();
			profileEdit.Visible = false;

			if (File.Exists($"{Phoenyx.Constants.UserFolder}/profiles/{text}.json"))
			{
				ToastNotification.Notify($"Profile {text} already exists!");
				return;
			}

			File.WriteAllText($"{Phoenyx.Constants.UserFolder}/profiles/{text}.json", File.ReadAllText($"{Phoenyx.Constants.UserFolder}/profiles/default.json"));
			UpdateSettings();
		};
		profiles.Pressed += ShowMouse;
		profiles.ItemSelected += (long item) => {
			string profile = profiles.GetItemText((int)item);

			HideMouse();
			Phoenyx.Settings.Save();
			File.WriteAllText($"{Phoenyx.Constants.UserFolder}/current_profile.txt", profile);
			Phoenyx.Settings.Load(profile);
			UpdateSettings();
		};

		skins.Pressed += ShowMouse;
		skins.ItemSelected += (long item) => {
			HideMouse();
			Phoenyx.Settings.Skin = skins.GetItemText((int)item);
			Phoenyx.Skin.Load();

			if (SceneManager.Scene.Name == "SceneMenu")
			{
				Menu.MainMenu.Cursor.Texture = Phoenyx.Skin.CursorImage;
			}

			Holder.GetNode("Categories").GetNode("Visuals").GetNode("Container").GetNode("Colors").GetNode<LineEdit>("LineEdit").Text = Phoenyx.Skin.RawColors;
		};

		spaces.Pressed += ShowMouse;
		spaces.ItemSelected += (long item) => {
			HideMouse();
			Phoenyx.Settings.Space = spaces.GetItemText((int)item);
		};

		Holder.GetNode("Categories").GetNode("Visuals").GetNode("Container").GetNode("Skin").GetNode<Button>("SkinFolder").Pressed += () => {
			OS.ShellOpen($"{Phoenyx.Constants.UserFolder}/skins/{Phoenyx.Settings.Skin}");
		};
		Holder.GetNode("Categories").GetNode("Other").GetNode("Container").GetNode("RhythiaImport").GetNode<Button>("Button").Pressed += () => {
			if (!Directory.Exists($"{OS.GetDataDir()}/SoundSpacePlus") || !File.Exists($"{OS.GetDataDir()}/SoundSpacePlus/settings.json"))
			{
				ToastNotification.Notify("Could not locate Rhythia settings", 1);
				return;
			}

			Godot.FileAccess file = Godot.FileAccess.Open($"{OS.GetDataDir()}/SoundSpacePlus/settings.json", Godot.FileAccess.ModeFlags.Read);
			Godot.Collections.Dictionary data = (Godot.Collections.Dictionary)Json.ParseString(file.GetAsText());

			Phoenyx.Settings.ApproachRate = (float)data["approach_rate"];
			Phoenyx.Settings.ApproachDistance = (float)data["spawn_distance"];
			Phoenyx.Settings.ApproachTime = Phoenyx.Settings.ApproachDistance / Phoenyx.Settings.ApproachRate;
			Phoenyx.Settings.FoV = (float)data["fov"];
			Phoenyx.Settings.Sensitivity = (float)data["sensitivity"] * 2;
			Phoenyx.Settings.Parallax = (float)data["parallax"] / 50;
			Phoenyx.Settings.FadeIn = (float)data["fade_length"] * 100;
			Phoenyx.Settings.FadeOut = (bool)data["half_ghost"];
			Phoenyx.Settings.Pushback = (bool)data["do_note_pushback"];
			Phoenyx.Settings.NoteSize = (float)data["note_size"] * 0.875f;
			Phoenyx.Settings.CursorScale = (float)data["cursor_scale"];
			Phoenyx.Settings.CursorTrail = (bool)data["cursor_trail"];
			Phoenyx.Settings.TrailTime = (float)data["trail_time"];
			Phoenyx.Settings.SimpleHUD = (bool)data["simple_hud"];
			Phoenyx.Settings.AbsoluteInput = (bool)data["absolute_mode"];
			Phoenyx.Settings.FPS = (double)data["fps"];
			Phoenyx.Settings.UnlockFPS = (bool)data["unlock_fps"];

			UpdateSettings();

			ToastNotification.Notify("Successfully imported Rhythia settings");
		};

		UpdateSettings(true);
	}

	public static void ShowSettings(bool show = true)
	{
		Shown = show;
		Settings.GetNode<Button>("Deselect").MouseFilter = Shown ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
		Control.CallDeferred("move_to_front");

		if (Shown)
		{
			Settings.Visible = true;
		}

		Tween tween = Settings.CreateTween();
		tween.TweenProperty(Settings, "modulate", Color.Color8(255, 255, 255, (byte)(Shown ? 255 : 0)), 0.25).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
		tween.Parallel().TweenProperty(Holder, "offset_top", Shown ? 0 : 25, 0.25).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
		tween.Parallel().TweenProperty(Holder, "offset_bottom", Shown ? 0 : 25, 0.25).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
		tween.TweenCallback(Callable.From(() => {
			Settings.Visible = Shown;
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
				Phoenyx.Settings.Sensitivity = (double)value;
				break;
			case "ApproachRate":
				Phoenyx.Settings.ApproachRate = (double)value;
				Phoenyx.Settings.ApproachTime = Phoenyx.Settings.ApproachDistance / Phoenyx.Settings.ApproachRate;
				break;
			case "ApproachDistance":
				Phoenyx.Settings.ApproachDistance = (double)value;
				Phoenyx.Settings.ApproachTime = Phoenyx.Settings.ApproachDistance / Phoenyx.Settings.ApproachRate;
				break;
			case "FadeIn":
				Phoenyx.Settings.FadeIn = (double)value;
				break;
			case "Parallax":
				Phoenyx.Settings.Parallax = (double)value;
				break;
			case "FoV":
				Phoenyx.Settings.FoV = (double)value;
				break;
			case "VolumeMaster":
				Phoenyx.Settings.VolumeMaster = (double)value;
				break;
			case "VolumeMusic":
				Phoenyx.Settings.VolumeMusic = (double)value;
				break;
			case "VolumeSFX":
				Phoenyx.Settings.VolumeSFX = (double)value;
				break;
			case "AlwaysPlayHitSound":
				Phoenyx.Settings.AlwaysPlayHitSound = (bool)value;
				break;
			case "NoteSize":
				Phoenyx.Settings.NoteSize = (double)value;
				break;
			case "CursorScale":
				Phoenyx.Settings.CursorScale = (double)value;
				
				if (SceneManager.Scene.Name == "SceneMenu")
				{
					Menu.MainMenu.Cursor.Size = new Vector2(32 * (float)Phoenyx.Settings.CursorScale, 32 * (float)Phoenyx.Settings.CursorScale);
				}

				break;
			case "FadeOut":
				Phoenyx.Settings.FadeOut = (bool)value;
				break;
			case "Pushback":
				Phoenyx.Settings.Pushback = (bool)value;
				break;
			case "Fullscreen":
				Phoenyx.Settings.Fullscreen = (bool)value;
				DisplayServer.WindowSetMode((bool)value ? DisplayServer.WindowMode.ExclusiveFullscreen : DisplayServer.WindowMode.Windowed);
				break;
			case "CursorTrail":
				Phoenyx.Settings.CursorTrail = (bool)value;
				break;
			case "TrailTime":
				Phoenyx.Settings.TrailTime = (double)value;
				break;
			case "TrailDetail":
				Phoenyx.Settings.TrailDetail = (double)value;
				break;
			case "CursorDrift":
				Phoenyx.Settings.CursorDrift = (bool)value;
				break;
			case "VideoDim":
				Phoenyx.Settings.VideoDim = (double)value;
				break;
			case "VideoRenderScale":
				Phoenyx.Settings.VideoRenderScale = (double)value;
				break;
			case "SimpleHUD":
				Phoenyx.Settings.SimpleHUD = (bool)value;
				break;
			case "AutoplayJukebox":
				Phoenyx.Settings.AutoplayJukebox = (bool)value;
				break;
			case "AbsoluteInput":
				Phoenyx.Settings.AbsoluteInput = (bool)value;
				break;
			case "RecordReplays":
				Phoenyx.Settings.RecordReplays = (bool)value;
				break;
			case "HitPopups":
				Phoenyx.Settings.HitPopups = (bool)value;
				break;
			case "MissPopups":
				Phoenyx.Settings.MissPopups = (bool)value;
				break;
			case "FPS":
				Phoenyx.Settings.FPS = (double)value;
				Engine.MaxFps = Phoenyx.Settings.UnlockFPS ? 0 : Convert.ToInt32(value);
				break;
			case "UnlockFPS":
				Phoenyx.Settings.UnlockFPS = (bool)value;
				Engine.MaxFps = Phoenyx.Settings.UnlockFPS ? 0 : Convert.ToInt32(Phoenyx.Settings.FPS);
				break;
		}

		UpdateSettings();
	}

	public static void UpdateSettings(bool connections = false)
	{
		OptionButton spaces = Holder.GetNode("Categories").GetNode("Visuals").GetNode("Container").GetNode("Space").GetNode<OptionButton>("OptionsButton");
		OptionButton skins = Holder.GetNode("Categories").GetNode("Visuals").GetNode("Container").GetNode("Skin").GetNode<OptionButton>("OptionsButton");
		OptionButton profiles = Holder.GetNode("Header").GetNode<OptionButton>("Profiles");
		string currentProfile = File.ReadAllText($"{Phoenyx.Constants.UserFolder}/current_profile.txt");

		skins.Clear();
		profiles.Clear();

		for (int i = 0; i < spaces.ItemCount; i++)
		{
			if (spaces.GetItemText(i) == Phoenyx.Settings.Space)
			{
				spaces.Selected = i;
				break;
			}
		}

		int j = 0;

		foreach (string path in Directory.GetDirectories($"{Phoenyx.Constants.UserFolder}/skins"))
		{
			string name = path.Split("\\")[^1];
			
			skins.AddItem(name, j);

			if (Phoenyx.Settings.Skin == name)
			{
				skins.Selected = j;
			}

			j++;
		}

		j = 0;

		foreach (string path in Directory.GetFiles($"{Phoenyx.Constants.UserFolder}/profiles"))
		{
			string name = path.Split("\\")[^1].TrimSuffix(".json");
			
			profiles.AddItem(name, j);

			if (currentProfile == name)
			{
				profiles.Selected = j;
			}

			j++;
		}

		foreach (ScrollContainer category in Holder.GetNode("Categories").GetChildren())
		{
			foreach (Panel option in category.GetNode("Container").GetChildren())
			{
				var property = new Phoenyx.Settings().GetType().GetProperty(option.Name);
				
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

	public static void ShowMouse()
	{
		Input.MouseMode = Input.MouseModeEnum.Visible;
	}

	public static void HideMouse()
	{
		if (SceneManager.Scene.Name == "SceneMenu")
		{
			Input.MouseMode = Input.MouseModeEnum.Hidden;
		}
	}
}
