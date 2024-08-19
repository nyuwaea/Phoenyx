using System;
using System.Text.RegularExpressions;
using Godot;

public struct Map
{
    public string ID;
    public string Artist;
    public string Title;
    public string PrettyTitle;
    public string[] Mappers;
    public string PrettyMappers;
    public string DifficultyName;
    public int Difficulty;
    public int Length;
    public byte[] AudioBuffer;
    public byte[] CoverBuffer;
    public Note[] Notes;

    public Map(Note[] data = null, string id = null, string artist = "N/A", string title = "N/A", string[] mappers = null, int difficulty = 0, string difficultyName = null, int? length = null, byte[] audioBuffer = null, byte[] coverBuffer = null)
    {
        Artist = artist;
        Title = title;
        PrettyTitle = artist != null ? $"{artist} - {title}" : title;
        Mappers = mappers ?? new string[]{"N/A"};
        PrettyMappers = "";
        Difficulty = difficulty;
        DifficultyName = difficultyName ?? "N/A";
        AudioBuffer = audioBuffer;
        CoverBuffer = coverBuffer;
        Notes = data ?? Array.Empty<Note>();
        Length = length ?? Notes[Notes.Length - 1].Millisecond;
        ID = id ?? new Regex("[^a-zA-Z0-9_ -]").Replace($"{Mappers.Stringify()}_{PrettyTitle}".Replace(" ", "_"), "");
        
        foreach (string mapper in Mappers)
        {
            PrettyMappers += $"{mapper}, ";
        }

        PrettyMappers = PrettyMappers.Substr(0, PrettyMappers.Length - 2);
    }

    public readonly override string ToString() => $"{PrettyTitle} by {PrettyMappers}";
}