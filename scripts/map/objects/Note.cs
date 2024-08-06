public class Note : IMapObject
{
    public int ID {get;}
    public float Millisecond {get;}
    public float X {get;}
    public float Y {get;}

    public Note(float time, float x, float y)
    {
        ID = 0;
        Millisecond = time;
        X = x;
        Y = y;
    }

    public override string ToString()
    {
        return $"({X}, {Y}) {Millisecond}ms";
    }
}