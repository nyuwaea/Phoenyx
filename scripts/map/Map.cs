public struct Map
{
    public string Artist = "N/A";
    public string Title = "N/A";
    public string Mapper = "N/A";
    public string DifficultyName = "N/A";
    public int Difficulty = 0;
    public double Length = 0;
    public Note[] Notes = new Note[0];

    public Map(Note[] data)
    {
        Notes = data;
    }

    public override string ToString()
    {
        return $"{Artist} - {Title} by {Mapper}";
    }
}