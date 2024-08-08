using System;
using Godot;

public class Logger
{
    public static void Log(string message, bool error = false)
    {
        message = $"[{Time.GetDatetimeStringFromSystem()}] {message}";

        switch (error)
        {
            case true:
                GD.PrintErr(message);
                break;
            case false:
                GD.Print(message);
                break;
        }
    }

    public static Exception Error(string message)
    {
        Log(message, true);

        return new Exception(message);
    }
}