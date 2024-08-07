using Godot;

public partial class MapParser : Node
{
	public static Map SSMapV1(string data)
	{
		string[] split = data.Split(",");

		Note[] notes = new Note[split.Length - 1];

		for (int i = 1; i < split.Length; i++)
		{
			string[] subsplit = split[i].Split("|");
			
			notes[i - 1] = new Note(i - 1, subsplit[2].ToFloat(), -subsplit[0].ToFloat() + 1, subsplit[1].ToFloat() - 1);
		}

		return new Map(notes);
	}
}