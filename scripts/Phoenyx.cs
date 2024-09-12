using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using Godot;
using Godot.Collections;

public partial class Phoenyx : Node
{
    public struct Constants
    {
        public static ulong Started = Time.GetTicksUsec();
        public static string RootFolder {get;} = Directory.GetCurrentDirectory();
        public static string UserFolder {get;} = OS.GetUserDataDir();
        public static double CursorSize {get;} = 0.2625;
        public static double GridSize {get;} = 3.0;
        public static Vector2 Bounds {get;} = new Vector2((float)(GridSize / 2 - CursorSize / 2), (float)(GridSize / 2 - CursorSize / 2));
        public static double HitBoxSize {get;} = 0.07;
        public static double HitWindow {get;} = 55;
        public static int BreakTime {get;} = 4000;  // used for skipping breaks mid-map
        public static string[] Difficulties = ["N/A", "Easy", "Medium", "Hard", "Expert", "Insane"];
        public static Color[] DifficultyColours = [Color.FromHtml("ffffff"), Color.FromHtml("00ff00"), Color.FromHtml("ffff00"), Color.FromHtml("ff0000"), Color.FromHtml("7f00ff"), Color.FromHtml("007fff")];
        public static Color[] SecondaryDifficultyColours = [Color.FromHtml("808080"), Color.FromHtml("7fff7f"), Color.FromHtml("ffff7f"), Color.FromHtml("ff007f"), Color.FromHtml("ff00ff"), Color.FromHtml("007fff")];
        public static Dictionary<string, double> ModsMultipliers = new(){
            ["NoFail"] = 0,
            ["Ghost"] = 0.0675
        };
    }

    public struct Settings
    {
        public static bool Fullscreen {get; set;} = false;
        public static double VolumeMaster {get; set;} = 50;
        public static double VolumeMusic {get; set;} = 50;
        public static double VolumeSFX {get; set;} = 50;
        public static bool AutoplayJukebox {get; set;} = true;
        public static bool AlwaysPlayHitSound {get; set;} = false;
        public static string Skin {get; set;} = "default";
        public static bool CameraLock {get; set;} = true;
        public static double FoV {get; set;} = 70;
        public static double Sensitivity {get; set;} = 0.5;
        public static double Parallax {get; set;} = 0.1;
        public static double ApproachRate {get; set;} = 32;
        public static double ApproachDistance {get; set;} = 20;
        public static double ApproachTime {get; set;} = ApproachDistance / ApproachRate;
        public static double FadeIn {get; set;} = 15;
        public static bool FadeOut {get; set;} = true;
        public static bool Pushback {get; set;} = true;
        public static double NoteSize {get; set;} = 0.875;
        public static double CursorScale {get; set;} = 1;
        public static bool CursorTrail {get; set;} = false;
        public static double TrailTime {get; set;} = 0.05;
        public static double TrailDetail {get; set;} = 1;
        public static bool CursorDrift {get; set;} = true;
        public static double VideoDim {get; set;} = 80;
        public static double VideoRenderScale {get; set;} = 100;
        public static bool SimpleHUD {get; set;} = false;
        public static string Space {get; set;} = "skin";

        public static void Save(string profile = null)
        {
            if (profile == null)
            {
                profile = Util.GetProfile();
            }

            Dictionary data = new(){
                ["_Version"] = 1,
                ["Fullscreen"] = Fullscreen,
                ["VolumeMaster"] = VolumeMaster,
                ["VolumeMusic"] = VolumeMusic,
                ["VolumeSFX"] = VolumeSFX,
                ["AutoplayJukebox"] = AutoplayJukebox,
                ["AlwaysPlayHitSound"] = AlwaysPlayHitSound,
                ["Skin"] = Skin,
                ["CameraLock"] = CameraLock,
                ["FoV"] = FoV,
                ["Sensitivity"] = Sensitivity,
                ["Parallax"] = Parallax,
                ["ApproachRate"] = ApproachRate,
                ["ApproachDistance"] = ApproachDistance,
                ["FadeIn"] = FadeIn,
                ["FadeOut"] = FadeOut,
                ["Pushback"] = Pushback,
                ["NoteSize"] = NoteSize,
                ["CursorScale"] = CursorScale,
                ["CursorTrail"] = CursorTrail,
                ["TrailTime"] = TrailTime,
                ["TrailDetail"] = TrailDetail,
                ["CursorDrift"] = CursorDrift,
                ["VideoDim"] = VideoDim,
                ["VideoRenderScale"] = VideoRenderScale,
                ["SimpleHUD"] = SimpleHUD,
                ["Space"] = Space
            };

            File.WriteAllText($"{Constants.UserFolder}/profiles/{profile}.json", Json.Stringify(data, "\t"));

            Phoenyx.Skin.Save();
        }

        public static void Load(string profile = null)
        {
            if (profile == null)
            {
                profile = Util.GetProfile();
            }

            Exception err = null;

            try
            {
                Godot.FileAccess file = Godot.FileAccess.Open($"{Constants.UserFolder}/profiles/{profile}.json", Godot.FileAccess.ModeFlags.Read);
                Dictionary data = (Dictionary)Json.ParseString(file.GetAsText());

                file.Close();
                
                Fullscreen = (bool)data["Fullscreen"];
                VolumeMaster = (double)data["VolumeMaster"];            
                VolumeMusic = (double)data["VolumeMusic"];
                VolumeSFX = (double)data["VolumeSFX"];
                AutoplayJukebox = (bool)data["AutoplayJukebox"];
                AlwaysPlayHitSound = (bool)data["AlwaysPlayHitSound"];
                Skin = (string)data["Skin"];
                CameraLock = (bool)data["CameraLock"];
                FoV = (int)data["FoV"];
                Sensitivity = (double)data["Sensitivity"];
                Parallax = (double)data["Parallax"];
                ApproachRate = (double)data["ApproachRate"];
                ApproachDistance = (double)data["ApproachDistance"];
                ApproachTime = ApproachDistance / ApproachRate;
                FadeIn = (double)data["FadeIn"];
                FadeOut = (bool)data["FadeOut"];
                Pushback = (bool)data["Pushback"];
                NoteSize = (double)data["NoteSize"];
                CursorScale = (double)data["CursorScale"];
                CursorTrail = (bool)data["CursorTrail"];
                TrailTime = (double)data["TrailTime"];
                TrailDetail = (double)data["TrailDetail"];
                CursorDrift = (bool)data["CursorDrift"];
                VideoDim = (double)data["VideoDim"];
                VideoRenderScale = (double)data["VideoRenderScale"];
                SimpleHUD = (bool)data["SimpleHUD"];
                Space = (string)data["Space"];

                if (Fullscreen)
                {
                    DisplayServer.WindowSetMode(DisplayServer.WindowMode.ExclusiveFullscreen);
                }

                ToastNotification.Notify($"Loaded profile [{profile}]");
            }
            catch (Exception exception)
            {
                err = exception;
            }

            if (!Directory.Exists($"{Constants.UserFolder}/skins/{Skin}"))
            {
                Skin = "default";
                ToastNotification.Notify($"Could not find skin {Skin}", 1);
            }

            Phoenyx.Skin.Load();

            if (err != null)
            {
                ToastNotification.Notify("Settings file corrupted", 2);
                throw Logger.Error($"Settings file corrupted; {err.Message}");
            }
        }
    }

    public struct Skin
    {
        public static Color[] Colors {get; set;} = [Color.FromHtml("#00ffed"), Color.FromHtml("#ff8ff9")];
        public static string RawColors {get; set;} = "00ffed,ff8ff9";
        public static ImageTexture CursorImage {get; set;} = new();
        public static ImageTexture GridImage {get; set;} = new();
        public static ImageTexture PanelLeftImage {get; set;} = new();
        public static ImageTexture PanelRightImage {get; set;} = new();
        public static ImageTexture HealthImage {get; set;} = new();
        public static ImageTexture HealthBackgroundImage {get; set;} = new();
        public static ImageTexture ProgressImage {get; set;} = new();
        public static ImageTexture ProgressBackgroundImage {get; set;} = new();
        public static ImageTexture HitsImage {get; set;} = new();
        public static ImageTexture MissesImage {get; set;} = new();
        public static ImageTexture MissFeedbackImage {get; set;} = new();
        public static ImageTexture JukeboxPlayImage {get; set;} = new();
        public static ImageTexture JukeboxPauseImage {get; set;} = new();
        public static ImageTexture JukeboxSkipImage {get; set;} = new();
        public static ImageTexture FavoriteImage {get; set;} = new();
        public static byte[] HitSoundBuffer {get; set;} = [];
        public static byte[] FailSoundBuffer {get; set;} = [];
        public static ArrayMesh NoteMesh {get; set;} = new();
        public static string Space {get; set;} = "grid";

        public static void Save()
        {
            File.WriteAllText($"{Constants.UserFolder}/skins/{Settings.Skin}/colors.txt", RawColors);
            File.WriteAllText($"{Constants.UserFolder}/skins/{Settings.Skin}/space.txt", Space);
        }

        public static void Load()
        {
            RawColors = File.ReadAllText($"{Constants.UserFolder}/skins/{Settings.Skin}/colors.txt").TrimSuffix(",");

            string[] split = RawColors.Split(",");
            Color[] colors = new Color[split.Length];

            for (int i = 0; i < split.Length; i++)
            {
                split[i] = split[i].TrimPrefix("#").Substr(0, 6);
                split[i] = new Regex("[^a-fA-F0-9$]").Replace(split[i], "f");
                colors[i] = Color.FromHtml(split[i]);
            }
            
            Colors = colors;
            CursorImage = ImageTexture.CreateFromImage(Image.LoadFromFile($"{Constants.UserFolder}/skins/{Settings.Skin}/cursor.png"));
            GridImage = ImageTexture.CreateFromImage(Image.LoadFromFile($"{Constants.UserFolder}/skins/{Settings.Skin}/grid.png"));
            PanelLeftImage = ImageTexture.CreateFromImage(Image.LoadFromFile($"{Constants.UserFolder}/skins/{Settings.Skin}/panel_left_background.png"));
            PanelRightImage = ImageTexture.CreateFromImage(Image.LoadFromFile($"{Constants.UserFolder}/skins/{Settings.Skin}/panel_right_background.png"));
            HealthImage = ImageTexture.CreateFromImage(Image.LoadFromFile($"{Constants.UserFolder}/skins/{Settings.Skin}/health.png"));
            HealthBackgroundImage = ImageTexture.CreateFromImage(Image.LoadFromFile($"{Constants.UserFolder}/skins/{Settings.Skin}/health_background.png"));
            ProgressImage = ImageTexture.CreateFromImage(Image.LoadFromFile($"{Constants.UserFolder}/skins/{Settings.Skin}/progress.png"));
            ProgressBackgroundImage = ImageTexture.CreateFromImage(Image.LoadFromFile($"{Constants.UserFolder}/skins/{Settings.Skin}/progress_background.png"));
            HitsImage = ImageTexture.CreateFromImage(Image.LoadFromFile($"{Constants.UserFolder}/skins/{Settings.Skin}/hits.png"));
            MissesImage = ImageTexture.CreateFromImage(Image.LoadFromFile($"{Constants.UserFolder}/skins/{Settings.Skin}/misses.png"));
            MissFeedbackImage = ImageTexture.CreateFromImage(Image.LoadFromFile($"{Constants.UserFolder}/skins/{Settings.Skin}/miss_feedback.png"));
            JukeboxPlayImage = ImageTexture.CreateFromImage(Image.LoadFromFile($"{Constants.UserFolder}/skins/{Settings.Skin}/jukebox_play.png"));
            JukeboxPauseImage = ImageTexture.CreateFromImage(Image.LoadFromFile($"{Constants.UserFolder}/skins/{Settings.Skin}/jukebox_pause.png"));
            JukeboxSkipImage = ImageTexture.CreateFromImage(Image.LoadFromFile($"{Constants.UserFolder}/skins/{Settings.Skin}/jukebox_skip.png"));
            FavoriteImage = ImageTexture.CreateFromImage(Image.LoadFromFile($"{Constants.UserFolder}/skins/{Settings.Skin}/favorite.png"));
            Space = File.ReadAllText($"{Constants.UserFolder}/skins/{Settings.Skin}/space.txt");

            if (File.Exists($"{Constants.UserFolder}/skins/{Settings.Skin}/note.obj"))
            {
                NoteMesh = (ArrayMesh)Util.OBJParser.Call("load_obj", $"{Constants.UserFolder}/skins/{Settings.Skin}/note.obj");
            }
            else
            {
                NoteMesh = GD.Load<ArrayMesh>($"res://skin/note.obj");
            }

            if (File.Exists($"{Constants.UserFolder}/skins/{Settings.Skin}/hit.mp3"))
            {
                Godot.FileAccess file = Godot.FileAccess.Open($"{Constants.UserFolder}/skins/{Settings.Skin}/hit.mp3", Godot.FileAccess.ModeFlags.Read);
                HitSoundBuffer = file.GetBuffer((long)file.GetLength());
                file.Close();
            }

            if (File.Exists($"{Constants.UserFolder}/skins/{Settings.Skin}/fail.mp3"))
            {
                Godot.FileAccess file = Godot.FileAccess.Open($"{Constants.UserFolder}/skins/{Settings.Skin}/fail.mp3", Godot.FileAccess.ModeFlags.Read);
                FailSoundBuffer = file.GetBuffer((long)file.GetLength());
                file.Close();
            }
            
            ToastNotification.Notify($"Loaded skin [{Settings.Skin}]");
        }
    }

    public class Stats
    {
        public static ulong GamePlaytime = 0;
        public static ulong TotalPlaytime = 0;
        public static ulong GamesOpened = 0;
        public static ulong TotalDistance = 0;
        public static ulong NotesHit = 0;
        public static ulong NotesMissed = 0;
        public static ulong HighestCombo = 0;
        public static ulong Attempts = 0;
        public static ulong Passes = 0;
        public static ulong FullCombos = 0;
        public static ulong HighestScore = 0;
        public static ulong TotalScore = 0;
        public static ulong RageQuits = 0;
        public static Array<double> PassAccuracies = [];
        public static Dictionary<string, ulong> FavouriteMaps = [];

        public static void Save()
        {
            File.SetAttributes($"{Constants.UserFolder}/stats", FileAttributes.None);
            Godot.FileAccess file = Godot.FileAccess.Open($"{Constants.UserFolder}/stats", Godot.FileAccess.ModeFlags.Write);
            string accuraciesJson = Json.Stringify(PassAccuracies);
            string mapsJson = Json.Stringify(FavouriteMaps);
            
            file.Store8(1);
            file.Store64(GamePlaytime);
            file.Store64(TotalPlaytime);
            file.Store64(GamesOpened);
            file.Store64(TotalDistance);
            file.Store64(NotesHit);
            file.Store64(NotesMissed);
            file.Store64(HighestCombo);
            file.Store64(Attempts);
            file.Store64(Passes);
            file.Store64(FullCombos);
            file.Store64(HighestScore);
            file.Store64(TotalScore);
            file.Store64(RageQuits);
            file.Store32((uint)accuraciesJson.Length);
            file.StoreString(accuraciesJson);
            file.Store32((uint)mapsJson.Length);
            file.StoreString(mapsJson);
            file.Close();

            byte[] bytes = File.ReadAllBytes($"{Constants.UserFolder}/stats");
            byte[] hash = new byte[32];
            
            SHA256.HashData(bytes, hash);

            file = Godot.FileAccess.Open($"{Constants.UserFolder}/stats", Godot.FileAccess.ModeFlags.Write);
            file.StoreBuffer(bytes);
            file.StoreBuffer(hash);
            file.Close();

            File.SetAttributes($"{Constants.UserFolder}/stats", FileAttributes.Hidden);
        }

        public static void Load()
        {
            try
            {
                FileParser file = new($"{Constants.UserFolder}/stats");

                byte[] bytes = file.Get((int)file.Length - 32);

                file.Seek(0);

                byte version = file.Get(1)[0];

                switch (version)
                {
                    case 1:
                    {
                        GamePlaytime = file.GetUInt64();
                        TotalPlaytime = file.GetUInt64();
                        GamesOpened = file.GetUInt64();
                        TotalDistance = file.GetUInt64();
                        NotesHit = file.GetUInt64();
                        NotesMissed = file.GetUInt64();
                        HighestCombo = file.GetUInt64();
                        Attempts = file.GetUInt64();
                        Passes = file.GetUInt64();
                        FullCombos = file.GetUInt64();
                        HighestScore = file.GetUInt64();
                        TotalScore = file.GetUInt64();
                        RageQuits = file.GetUInt64();
                        PassAccuracies = (Array<double>)Json.ParseString(file.GetString((int)file.GetUInt32()));
                        FavouriteMaps = (Dictionary<string, ulong>)Json.ParseString(file.GetString((int)file.GetUInt32()));

                        byte[] hash = file.Get(32);
                        byte[] newHash = new byte[32];

                        SHA256.HashData(bytes, newHash);

                        for (int i = 0; i < 32; i++)
                        {
                            if (hash[i] != newHash[i])
                            {
                                throw new("Wrong hash");
                            }
                        }

                        break;
                    }
                }
            }
            catch (Exception exception)
            {
                ToastNotification.Notify("Stats file corrupt or modified", 2);
                throw Logger.Error($"Stats file corrupt or modified; {exception.Message}");
            }
        }
    }

    public class Util
    {
        private static bool Initialized = false;
        private static string[] UserDirectories = ["maps", "profiles", "skins", "replays"];
        private static string[] SkinFiles = ["cursor.png", "grid.png", "health.png", "hits.png", "misses.png", "miss_feedback.png", "health_background.png", "progress.png", "progress_background.png", "panel_left_background.png", "panel_right_background.png", "jukebox_play.png", "jukebox_pause.png", "jukebox_skip.png", "favorite.png", "hit.mp3", "fail.mp3", "colors.txt"];

        public static GodotObject DiscordRPC = (GodotObject)GD.Load<GDScript>("res://scripts/DiscordRPC.gd").New();
        public static GodotObject OBJParser = (GodotObject)GD.Load<GDScript>("res://scripts/OBJParser.gd").New();

        public static bool Quitting = false;

        public static void Setup()
        {
            if (Initialized)
            {
                return;
            }

            Initialized = true;

            DiscordRPC.Call("Set", "app_id", 1272588732834254878);
            DiscordRPC.Call("Set", "large_image", "short");
            
            if (!File.Exists($"{Constants.UserFolder}/favorites.txt"))
            {
                File.WriteAllText($"{Constants.UserFolder}/favorites.txt", "");
            }

            if (!Directory.Exists($"{Constants.UserFolder}/cache"))
            {
                Directory.CreateDirectory($"{Constants.UserFolder}/cache");
            }

            if (!Directory.Exists($"{Constants.UserFolder}/cache/maps"))
            {
                Directory.CreateDirectory($"{Constants.UserFolder}/cache/maps");
            }

            foreach (string cacheFile in Directory.GetFiles($"{Constants.UserFolder}/cache"))
            {
                File.Delete(cacheFile);
            }

            for (int i = 0; i < UserDirectories.Length; i++)
            {
                string Folder = UserDirectories[i];

                if (!Directory.Exists($"{Constants.UserFolder}/{Folder}"))
                {
                    Directory.CreateDirectory($"{Constants.UserFolder}/{Folder}");
                }
            }

            if (!Directory.Exists($"{Constants.UserFolder}/skins/default"))
            {
                Directory.CreateDirectory($"{Constants.UserFolder}/skins/default");
            }
            
            foreach (string skinFile in SkinFiles)
            {
                try
                {
                    if (!File.Exists($"{Constants.UserFolder}/skins/default/{skinFile}"))
                    {
                        byte[] buffer = System.Array.Empty<byte>();

                        if (skinFile.GetExtension() == "txt")
                        {
                            Godot.FileAccess file = Godot.FileAccess.Open($"res://skin/{skinFile}", Godot.FileAccess.ModeFlags.Read);
                            buffer = file.GetBuffer((long)file.GetLength());
                        }
                        else
                        {
                            var source = ResourceLoader.Load($"res://skin/{skinFile}");

                            switch (source.GetType().Name)
                            {
                                case "CompressedTexture2D":
                                    buffer = (source as CompressedTexture2D).GetImage().SavePngToBuffer();
                                    break;
                                case "AudioStreamMP3":
                                    buffer = (source as AudioStreamMP3).Data;
                                    break;
                            }
                        }

                        if (buffer.Length == 0)
                        {
                            continue;
                        }

                        Godot.FileAccess target = Godot.FileAccess.Open($"{Constants.UserFolder}/skins/default/{skinFile}", Godot.FileAccess.ModeFlags.Write);
                        target.StoreBuffer(buffer);
                        target.Close();
                    }
                }
                catch (Exception exception)
                {
                    Logger.Log($"Couldn't copy default skin file {skinFile}; {exception}");
                }
            }

            if (!File.Exists($"{Constants.UserFolder}/current_profile.txt"))
            {
                File.WriteAllText($"{Constants.UserFolder}/current_profile.txt", "default");
            }

            if (!File.Exists($"{Constants.UserFolder}/profiles/default.json"))
            {
                Settings.Save("default");
            }
            
            try
            {
                Settings.Load();
            }
            catch
            {
                Settings.Save();
            }

            if (!File.Exists($"{Constants.UserFolder}/stats"))
            {
                File.WriteAllText($"{Constants.UserFolder}/stats", "");
                Stats.Save();
            }
            
            try
            {
                Stats.Load();
            }
            catch
            {
                Stats.Save();
            }

            Stats.GamesOpened++;
        }

        public static void Quit()
        {
            if (Quitting)
            {
                return;
            }

            Quitting = true;

            Stats.TotalPlaytime += (Time.GetTicksUsec() - Constants.Started) / 1000000;

            Settings.Save();
            Stats.Save();
            
            DiscordRPC.Call("Set", "end_timestamp", 0);
            DiscordRPC.Call("Clear");

            if (SceneManager.Scene.Name == "SceneMenu")
            {
                Tween tween = SceneManager.Scene.CreateTween();
                tween.TweenProperty(SceneManager.Scene, "modulate", Color.Color8(1, 1, 1, 0), 0.5).SetTrans(Tween.TransitionType.Quad);
                tween.TweenCallback(Callable.From(() => {
                    SceneManager.Scene.GetTree().Quit();
                }));
                tween.Play();
            }
            else
            {
                SceneManager.Scene.GetTree().Quit();
            }
        }

        public static string GetProfile()
        {
            return File.ReadAllText($"{Constants.UserFolder}/current_profile.txt");
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest)
		{
            if (SceneManager.Scene.Name == "SceneGame")
            {
                Stats.RageQuits++;
            }

			Util.Quit();
		}
    }
}