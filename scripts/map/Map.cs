public struct Map
{
    public bool Loaded = false;
    public string Artist = "N/A";
    public string Title = "N/A";
    public string Mapper = "N/A";
    public string DifficultyName = "N/A";
    public int Difficulty = 0;
    public Note[] Notes = new Note[0];

    public Map(string artist = "N/A", string title = "N/A", int? difficulty = 0, Note[] data = null)
    {
        Artist = artist;
        Title = title;
        Difficulty = difficulty == null ? 0 : (int)difficulty;
        Notes = data == null ? new Note[0] : data;
        Loaded = true;
    }

    public override string ToString()
    {
        return $"{Artist} - {Title} by {Mapper}";
    }
}