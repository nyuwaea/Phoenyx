using Godot;

public class SceneManager
{
    public static Node Scene;

    public static void Load(SceneTree tree, string path)
    {
        tree.ChangeSceneToFile(path);
        tree.Connect("node_added", Callable.From((Node child) => {
            Scene = child;
        }), 4);
    }
}