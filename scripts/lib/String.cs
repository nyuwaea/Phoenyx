using Godot;

namespace Lib;

public class String
{
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
}