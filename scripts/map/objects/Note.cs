public struct Note : IMapObject
{
    public int ObjectID {get;}  // map object type id
    public int Index {get; set;} // note index within the map
    public float Millisecond {get;}
    public float X {get;}
    public float Y {get;}
    public bool Hit {set; get;}

    public Note(int index, float time, float x, float y)
    {
        ObjectID = 0;
        Index = index;
        Millisecond = time;
        X = x;
        Y = y;
        Hit = false;
    }

    public override string ToString()
    {
        return $"({X}, {Y}) @{Millisecond}ms";
    }
}