using System;
using System.Text;
using System.Text.RegularExpressions;
using Godot;

public struct Map
{
    public string ID;
    public string Artist;
    public string Title;
    public string PrettyTitle;
    public float Rating;
    public string[] Mappers;
    public string PrettyMappers;
    public string DifficultyName;
    public int Difficulty;
    public int Length;
    public byte[] AudioBuffer;
    public string AudioExt;
    public byte[] CoverBuffer;
    public byte[] VideoBuffer;
    public Note[] Notes;

    public Map(Note[] data = null, string id = null, string artist = "", string title = "", float rating = 0, string[] mappers = null, int difficulty = 0, string difficultyName = null, int? length = null, byte[] audioBuffer = null, byte[] coverBuffer = null, byte[] videoBuffer = null)
    {
        Artist = artist;
        Title = title;
        PrettyTitle = artist != "" ? $"{artist} - {title}" : title;
        Rating = rating;
        Mappers = mappers ?? new string[]{"N/A"};
        PrettyMappers = "";
        Difficulty = difficulty;
        DifficultyName = difficultyName ?? "N/A";
        AudioBuffer = audioBuffer;
        CoverBuffer = coverBuffer;
        VideoBuffer = videoBuffer;

        Notes = data ?? Array.Empty<Note>();
        Length = length ?? Notes[Notes.Length - 1].Millisecond;
        ID = id ?? new Regex("[^a-zA-Z0-9_ -]").Replace($"{Mappers.Stringify()}_{PrettyTitle}".Replace(" ", "_"), "");
        AudioExt = (AudioBuffer != null && Encoding.UTF8.GetString(AudioBuffer[0..4]) == "OggS") ? "ogg" : "mp3";
        
        foreach (string mapper in Mappers)
        {
            PrettyMappers += $"{mapper}, ";
        }

        PrettyMappers = PrettyMappers.Substr(0, PrettyMappers.Length - 2);
    }

    public string EncodeMeta()
    {
        return Json.Stringify(new Godot.Collections.Dictionary(){
			["ID"] = ID,
			["Artist"] = Artist,
			["Title"] = Title,
			["Rating"] = Rating,
			["Mappers"] = Mappers,
			["Difficulty"] = Difficulty,
			["DifficultyName"] = DifficultyName,
			["Length"] = Length,
			["HasAudio"] = AudioBuffer != null,
			["HasCover"] = CoverBuffer != null,
            ["HasVideo"] = VideoBuffer != null,
			["AudioExt"] = AudioExt
		}, "\t");
    }

    public readonly override string ToString() => $"{PrettyTitle} by {PrettyMappers}";
}