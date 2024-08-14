public class Player
{
    public string Name;

    public Player(string name = "Player")
    {
        Name = name;
    }

    public override string ToString() => $"{Name}";
}