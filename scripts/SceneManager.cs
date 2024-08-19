using Godot;

public partial class SceneManager : Node
{
    private static Node Node;
    public static Node Scene;

    public override void _Ready()
    {
        Node = this;
    }

    public static void Load(string path)
    {
        Node.GetTree().ChangeSceneToFile(path);
        Node.GetTree().Connect("node_added", Callable.From((Node child) => {
            Scene = child;
        }), 4);
    }
}