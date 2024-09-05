using System;
using Godot;

namespace Lib;

public class String
{
    public static string FormatTime(double seconds, bool padMinutes = false)
	{
		int minutes = (int)Math.Floor(seconds / 60);

		seconds -= minutes * 60;
		seconds = Math.Floor(seconds);

		return $"{(seconds < 0 ? "-" : "")}{(padMinutes ? minutes.ToString().PadZeros(2) : minutes)}:{seconds.ToString().PadZeros(2)}";
	}

    public static string PadMagnitude(string str, string pad = ",")
    {
        string formatted = "";
        string[] split = str.Split(".");
        string whole = split[0];
        string decimals = split.Length > 1 ? "." + split[1] : "";

        for (int i = 0; i < whole.Length; i++)
        {
            formatted += whole[i];

            if ((whole.Length - i - 1) % 3 == 0)
            {
                formatted += pad;
            }
        }

        return formatted.TrimSuffix(pad) + decimals;
    }
}