using System;
using Godot;

public struct Map
{
    public string ID = "0";
    public string Artist = "N/A";
    public string Title = "N/A";
    public string PrettyTitle = "N/A";
    public string[] Mappers = Array.Empty<string>();
    public string PrettyMappers = "N/A";
    public string DifficultyName = "N/A";
    public int Difficulty = 0;
    public int Length = 0;
    public byte[] AudioBuffer = new byte[0];
    public byte[] CoverBuffer = new byte[0];
    public Note[] Notes = new Note[0];

    public Map(Note[] data = null, string id = "0", string artist = "N/A", string title = "N/A", string[] mappers = null, int difficulty = 0, int length = 0, byte[] audioBuffer = null, byte[] coverBuffer = null)
    {
        ID = id;
        Artist = artist;
        Title = title;
        PrettyTitle = artist != null ? $"{artist} - {title}" : title;
        Mappers = mappers;
        PrettyMappers = "";
        Difficulty = difficulty;
        Length = length;
        AudioBuffer = audioBuffer;
        CoverBuffer = coverBuffer;
        Notes = data == null ? new Note[0] : data;

        foreach (string mapper in Mappers)
        {
            PrettyMappers += $"{mapper}, ";
        }

        PrettyMappers = PrettyMappers.Substr(0, PrettyMappers.Length - 2);
    }

    public override string ToString()
    {
        return $"{PrettyTitle} by {PrettyMappers}";
    }
}