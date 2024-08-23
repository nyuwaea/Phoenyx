public struct Note : IMapObject
{
    public int ObjectID {get;} = 0;     // map object type id
    public int Index;                  // note index within the map
    public int Millisecond {get;}
    public float X;
    public float Y;
    public bool Hit = false;
    public bool Hittable = false;

    public Note(int index, int millisecond, float x, float y)
    {
        Index = index;
        Millisecond = millisecond;
        X = x;
        Y = y;
    }

    public readonly override string ToString() => $"({X}, {Y}) @{Millisecond}ms";
}