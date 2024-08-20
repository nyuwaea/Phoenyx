using System;
using System.IO;
using System.Reflection;
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
    public static string[] Colors {get; set;} = {"#00ffed", "#ff8ff9"};
    
    public Settings() {}
}

public class Util
{
    private static bool Initialized = false;
    private static string[] UserDirectories = new string[]{"maps", "profiles", "skins", "replays"};
    private static string[] SkinFiles = new string[]{"cursor.png", "grid.png", "health.png", "hits.png", "misses.png", "miss_feedback.png", "health_background.png", "progress.png", "progress_background.png", "panel_left_background.png", "panel_right_background.png", "note.obj", "hit.mp3"};
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
		DiscordRPC.Call("Set", "large_image", "wizardry");

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
                var source = ResourceLoader.Load($"res://skin/{skinFile}");
                byte[] buffer = System.Array.Empty<byte>();
                
                switch (source.GetType().Name)
                {
                    case "CompressedTexture2D":
                        buffer = (source as CompressedTexture2D).GetImage().SavePngToBuffer();
                        break;
                    case "AudioStreamMP3":
                        buffer = (source as AudioStreamMP3).Data;
                        break;
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
            SaveSettings("default");
        }

        try
        {
            LoadSettings();
        }
        catch
        {
            SaveSettings();
        }
    }

    public static string GetProfile()
    {
        return Godot.FileAccess.Open($"{Constants.UserFolder}/current_profile.txt", Godot.FileAccess.ModeFlags.Read).GetLine();
    }

    public static void SaveSettings(string profile = null)
    {
        if (profile == null)
        {
            profile = GetProfile();
        }

        Dictionary data = new(){
            ["_Version"] = 1,
            ["Fullscreen"] = Settings.Fullscreen,
            ["VolumeMaster"] = Settings.VolumeMaster,
            ["VolumeMusic"] = Settings.VolumeMusic,
            ["VolumeSFX"] = Settings.VolumeSFX,
            ["Skin"] = Settings.Skin,
            ["CameraLock"] = Settings.CameraLock,
            ["FoV"] = Settings.FoV,
            ["Sensitivity"] = Settings.Sensitivity,
            ["Parallax"] = Settings.Parallax,
            ["ApproachRate"] = Settings.ApproachRate,
            ["ApproachDistance"] = Settings.ApproachDistance,
            ["FadeIn"] = Settings.FadeIn,
            ["FadeOut"] = Settings.FadeOut,
            ["Pushback"] = Settings.Pushback,
            ["NoteSize"] = Settings.NoteSize,
            ["Colors"] = Settings.Colors
        };

        File.WriteAllText($"{Constants.UserFolder}/profiles/{profile}.json", Json.Stringify(data, "\t"));
    }

    public static void LoadSettings(string profile = null)
    {
        if (profile == null)
        {
            profile = GetProfile();
        }

        try
        {
            Godot.FileAccess file = Godot.FileAccess.Open($"{Constants.UserFolder}/profiles/{profile}.json", Godot.FileAccess.ModeFlags.Read);
            Dictionary data = (Dictionary)Json.ParseString(file.GetAsText());

            file.Close();
            
            Settings.Fullscreen = (bool)data["Fullscreen"];
            Settings.VolumeMaster = (double)data["VolumeMaster"];            
            Settings.VolumeMusic = (double)data["VolumeMusic"];
            Settings.VolumeSFX = (double)data["VolumeSFX"];
            Settings.Skin = (string)data["Skin"];
            Settings.CameraLock = (bool)data["CameraLock"];
            Settings.FoV = (int)data["FoV"];
            Settings.Sensitivity = (double)data["Sensitivity"];
            Settings.Parallax = (double)data["Parallax"];
            Settings.ApproachRate = (double)data["ApproachRate"];
            Settings.ApproachDistance = (double)data["ApproachDistance"];
            Settings.ApproachTime = Settings.ApproachDistance / Settings.ApproachRate;
            Settings.FadeIn = (double)data["FadeIn"];
            Settings.FadeOut = (bool)data["FadeOut"];
            Settings.Pushback = (bool)data["Pushback"];
            Settings.NoteSize = (double)data["NoteSize"];
            Settings.Colors = (string[])data["Colors"];

            ToastNotification.Notify($"Loaded profile [{profile}]");
            ToastNotification.Notify($"Loaded skin [{Settings.Skin}]");
        }
        catch (Exception exception)
        {
            ToastNotification.Notify("Settings file corrupted", 2);
            throw Logger.Error($"Settings file corrupted; {exception.Message}");
        }
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