using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Godot;
using Godot.Collections;

namespace Phoenyx;

public struct Constants
{
    public static string RootFolder {get;} = Directory.GetCurrentDirectory();
    public static string UserFolder {get;} = OS.GetUserDataDir();
    public static double CursorSize {get;} = 0.2625;
    public static double GridSize {get;} = 3.0;
    public static Vector2 Bounds {get;} = new Vector2((float)(GridSize / 2 - CursorSize / 2), (float)(GridSize / 2 - CursorSize / 2));
    public static double HitBoxSize {get;} = 0.07;
    public static double HitWindow {get;} = 55;
    public static int BreakTime {get;} = 4000;  // used for skipping breaks mid-map
    public static string[] Difficulties = new string[6]{"N/A", "Easy", "Medium", "Hard", "Expert", "Insane"};
    public static Dictionary<string, double> ModsMultipliers = new(){
        ["NoFail"] = 0,
        ["Ghost"] = 0.0675
    };
}

public struct Settings
{
    public static bool Fullscreen {get; set;} = false;
    public static double VolumeMaster {get; set;} = 100;
    public static double VolumeMusic {get; set;} = 50;
    public static double VolumeSFX {get; set;} = 50;
    public static string Skin {get; set;} = "default";
    public static bool CameraLock {get; set;} = true;
    public static double FoV {get; set;} = 70;
    public static double Sensitivity {get; set;} = 0.66;
    public static double Parallax {get; set;} = 0.1;
    public static double ApproachRate {get; set;} = 35;
    public static double ApproachDistance {get; set;} = 16;
    public static double ApproachTime {get; set;} = ApproachDistance / ApproachRate;
    public static double FadeIn {get; set;} = 0;
    public static bool FadeOut {get; set;} = true;
    public static bool Pushback {get; set;} = true;
    public static double NoteSize {get; set;} = 0.875;
    public static double CursorScale {get; set;} = 1;
    public static bool CursorTrail {get; set;} = false;
    public static double TrailTime {get; set;} = 0.05;
    public static double TrailDetail {get; set;} = 1;
    public static bool CursorDrift {get; set;} = true;
    public static double VideoDim {get; set;} = 0.8;
    
    public Settings() {}

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
            ["VideoDim"] = VideoDim
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

        try
        {
            Godot.FileAccess file = Godot.FileAccess.Open($"{Constants.UserFolder}/profiles/{profile}.json", Godot.FileAccess.ModeFlags.Read);
            Dictionary data = (Dictionary)Json.ParseString(file.GetAsText());

            file.Close();
            
            Fullscreen = (bool)data["Fullscreen"];
            VolumeMaster = (double)data["VolumeMaster"];            
            VolumeMusic = (double)data["VolumeMusic"];
            VolumeSFX = (double)data["VolumeSFX"];
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

            if (Fullscreen)
		    {
		    	DisplayServer.WindowSetMode(DisplayServer.WindowMode.ExclusiveFullscreen);
		    }

            ToastNotification.Notify($"Loaded profile [{profile}]");
            ToastNotification.Notify($"Loaded skin [{Skin}]");
        }
        catch (Exception exception)
        {
            ToastNotification.Notify("Settings file corrupted", 2);
            throw Logger.Error($"Settings file corrupted; {exception.Message}");
        }

        if (!Directory.Exists($"{Constants.UserFolder}/skins/{Skin}"))
        {
            Skin = "default";
            ToastNotification.Notify($"Could not find skin {Skin}", 1);
        }

        Phoenyx.Skin.Load();
    }
}

public struct Skin
{
    public static string[] Colors {get; set;} = new string[]{"#00ffed", "#ff8ff9"};
    public static ImageTexture CursorImage {get; set;} = new();
    public static ImageTexture GridImage {get; set;} = new();
    public static ImageTexture HealthImage {get; set;} = new();
    public static ImageTexture HealthBackgroundImage {get; set;} = new();
    public static ImageTexture ProgressImage {get; set;} = new();
    public static ImageTexture ProgressBackgroundImage {get; set;} = new();
    public static byte[] HitSoundBuffer {get; set;} = System.Array.Empty<byte>();
    public static ArrayMesh NoteMesh {get; set;} = new();

    public Skin() {}

    public static void Save()
    {
        string data = "";

        foreach (string color in Colors)
        {
            data += color + ",";
        }
        
        data = data.TrimSuffix(",");

        File.WriteAllText($"{Constants.UserFolder}/skins/{Settings.Skin}/colors.txt", data);
    }

    public static void Load()
    {
        string[] split = File.ReadAllText($"{Constants.UserFolder}/skins/{Settings.Skin}/colors.txt").Split(",");

        for (int i = 0; i < split.Length; i++)
        {
            split[i] = split[i].TrimPrefix("#").Substr(0, 6);
            split[i] = new Regex("[^a-fA-F0-9$]").Replace(split[i], "f");
        }

        Colors = split;
        CursorImage = ImageTexture.CreateFromImage(Image.LoadFromFile($"{Constants.UserFolder}/skins/{Settings.Skin}/cursor.png"));
        GridImage = ImageTexture.CreateFromImage(Image.LoadFromFile($"{Constants.UserFolder}/skins/{Settings.Skin}/grid.png"));
        HealthImage = ImageTexture.CreateFromImage(Image.LoadFromFile($"{Constants.UserFolder}/skins/{Settings.Skin}/health.png"));
        HealthBackgroundImage = ImageTexture.CreateFromImage(Image.LoadFromFile($"{Constants.UserFolder}/skins/{Settings.Skin}/health_background.png"));
        ProgressImage = ImageTexture.CreateFromImage(Image.LoadFromFile($"{Constants.UserFolder}/skins/{Settings.Skin}/progress.png"));
        ProgressBackgroundImage = ImageTexture.CreateFromImage(Image.LoadFromFile($"{Constants.UserFolder}/skins/{Settings.Skin}/progress_background.png"));

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
    }
}

public class Util
{
    private static bool Initialized = false;
    private static string[] UserDirectories = new string[]{"maps", "profiles", "skins", "replays"};
    private static string[] SkinFiles = new string[]{"cursor.png", "grid.png", "health.png", "hits.png", "misses.png", "miss_feedback.png", "health_background.png", "progress.png", "progress_background.png", "panel_left_background.png", "panel_right_background.png", "note.obj", "hit.mp3", "colors.txt"};
    private static Dictionary<string, bool> IgnoreProperties = new Dictionary<string, bool>(){
        ["_import_path"] = true,
        ["owner"] = true,
        
        ["anchor_left"] = true,
        ["anchor_top"] = true,
        ["anchor_right"] = true,
        ["anchor_bottom"] = true,

        ["layout_mode"] = true,
        ["global_position"] = true
    };

    public static GodotObject DiscordRPC = (GodotObject)GD.Load<GDScript>("res://scripts/DiscordRPC.gd").New();
    public static GodotObject OBJParser = (GodotObject)GD.Load<GDScript>("res://scripts/OBJParser.gd").New();

    public static void Setup()
    {
        if (Initialized)
        {
            return;
        }

        Initialized = true;

        DiscordRPC.Call("Set", "app_id", 1272588732834254878);
		DiscordRPC.Call("Set", "large_image", "short");

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
    }

    public static string GetProfile()
    {
        return Godot.FileAccess.Open($"{Constants.UserFolder}/current_profile.txt", Godot.FileAccess.ModeFlags.Read).GetLine();
    }

    public static string PadMagnitude(string str, string pad = ",")
    {
        string formatted = "";

        for (int i = 0; i < str.Length; i++)
        {
            formatted += str[i];

            if ((str.Length - i - 1) % 3 == 0)
            {
                formatted += pad;
            }
        }

        return formatted.TrimSuffix(pad);
    }

    public static T Clone<T>(T reference, bool recursive = true) where T : Node, new()
    {
        T clone = new T();
        
        foreach (Dictionary entry in reference.GetPropertyList())
        {
            if ((int)entry["type"] == 0 || IgnoreProperties.ContainsKey((string)entry["name"]))
            {
                continue;
            }

            clone.Set((string)entry["name"], reference.Get((string)entry["name"]));
        }

        if (recursive)
        {
            Array<Node> children = reference.GetChildren();
            
            for (int i = 0; i < children.Count; i++)
            {   
                Node childClone = (Node)typeof(Util).GetMethod("Clone", BindingFlags.Public | BindingFlags.Static).MakeGenericMethod(children[i].GetType()).Invoke(null, new object[]{children[i], recursive});

                clone.AddChild(childClone);
            }
        }

        return clone;
    }
}