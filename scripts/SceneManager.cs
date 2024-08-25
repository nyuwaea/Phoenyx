using Godot;

public partial class SceneManager : Node
{
    private static Node Node;
    public static Node Scene;

    public override void _Ready()
    {
        Node = this;

        Node.GetTree().Connect("node_added", Callable.From((Node child) => {
            if (child.Name != "SceneMenu" && child.Name != "SceneGame")
            {
                return;
            }

            Scene = child;

            ColorRect inTransition = Scene.GetNode<ColorRect>("Transition");
            inTransition.SelfModulate = Color.FromHtml("ffffffff");
            Tween inTween = inTransition.CreateTween();
            inTween.TweenProperty(inTransition, "self_modulate", Color.FromHtml("ffffff00"), 0.25).SetTrans(Tween.TransitionType.Quad);
            inTween.Play();
        }));
    }

    public static void Load(string path)
    {
        ColorRect outTransition = Scene.GetNode<ColorRect>("Transition");
        Tween outTween = outTransition.CreateTween();
        outTween.TweenProperty(outTransition, "self_modulate", Color.FromHtml("ffffffff"), 0.25).SetTrans(Tween.TransitionType.Quad);
        outTween.TweenCallback(Callable.From(() => {
            Node.GetTree().ChangeSceneToFile(path);
        }));
        outTween.Play();
    }
}