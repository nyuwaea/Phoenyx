using System.IO;
using Godot;
using Godot.Collections;

namespace Phoenix;

public class Constants
{
    public static string UserFolder = OS.GetUserDataDir();
    public static float CursorSize = 0.2625f;
    public static float GridSize = 3.0f;
    public static Vector2 Bounds = new Vector2(GridSize / 2 - CursorSize / 2, GridSize / 2 - CursorSize / 2);
    public static float HitBoxSize = 1.14f;
    public static float HitWindow = 55f;
}

public class Settings
{
    public static string Profile {get; set;} = "default";
    public static float Sensitivity {get; set;} = 0.5f;
    public static float Parallax {get; set;} = 0.1f;
    public static float ApproachRate {get; set;} = 35.0f;
    public static float ApproachDistance {get; set;} = 16.0f;
    public static float ApproachTime {get; set;} = ApproachDistance / ApproachRate;
    public static string[] Colors {get; set;} = {"#00ffed", "#ff8ff9"};
}

public class Util
{
    private static bool Initialized = false;
    private static string[] UserDirectories = new string[]{"maps", "profiles", "skins"};

    public static void SetupUserFolder()
    {
        if (Initialized)
        {
            return;
        }

        Initialized = true;

        for (int i = 0; i < UserDirectories.Length; i++)
        {
            string Folder = UserDirectories[i];

            if (!Directory.Exists($"{Constants.UserFolder}/{Folder}"))
		    {
		    	Directory.CreateDirectory($"{Constants.UserFolder}/{Folder}");
		    }
        }

        if (!File.Exists($"{Constants.UserFolder}/current_profile.txt"))
        {
            File.WriteAllText($"{Constants.UserFolder}/current_profile.txt", "default");
        }

        if (!File.Exists($"{Constants.UserFolder}/profiles/default.json"))
        {
            SaveSettings();
        }

        LoadSettings(Godot.FileAccess.Open($"{Constants.UserFolder}/current_profile.txt", Godot.FileAccess.ModeFlags.Read).GetLine());
    }

    public static void SaveSettings(string profile = "default")
    {
        Dictionary data = new Dictionary();

        data.Add("_Version", 1);
        data.Add("Sensitivity", Settings.Sensitivity);
        data.Add("Parallax", Settings.Parallax);
        data.Add("ApproachRate", Settings.ApproachRate);
        data.Add("ApproachDistance", Settings.ApproachDistance);
        data.Add("ApproachTime", Settings.ApproachTime);
        data.Add("Colors", Settings.Colors);

        File.WriteAllText($"{Constants.UserFolder}/profiles/{profile}.json", Json.Stringify(data, "\t"));
    }

    public static void LoadSettings(string profile = "default")
    {
        Godot.FileAccess file = Godot.FileAccess.Open($"{Constants.UserFolder}/profiles/{profile}.json", Godot.FileAccess.ModeFlags.Read);
        Dictionary data = (Dictionary)Json.ParseString(file.GetAsText());

        file.Close();

        Settings.Sensitivity = (float)data["Sensitivity"];
        Settings.Parallax = (float)data["Parallax"];
        Settings.ApproachRate = (float)data["ApproachRate"];
        Settings.ApproachDistance = (float)data["ApproachDistance"];
        Settings.ApproachTime = (float)data["ApproachTime"];
        Settings.Colors = (string[])data["Colors"];
    }
}