public struct Note : IMapObject
{
    public int ObjectID {get;}  // map object type id
    public int Index; // note index within the map
    public int Millisecond {get;}
    public float X;
    public float Y;
    public bool Hit;

    public Note(int index, int millisecond, float x, float y)
    {
        ObjectID = 0;
        Index = index;
        Millisecond = millisecond;
        X = x;
        Y = y;
        Hit = false;
    }

    public readonly override string ToString() => $"({X}, {Y}) @{Millisecond}ms";
}