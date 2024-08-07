using Godot;

public partial class Renderer : MultiMeshInstance3D
{
    public override void _Process(double delta)
    {
        if (!Game.Playing)
        {
            return;
        }

        Multimesh.InstanceCount = Game.ToProcess;

        for (int i = 0; i < Game.ToProcess; i++)
        {
            Note note = Game.Notes[i];

            Multimesh.SetInstanceTransform(i, new Transform3D(Basis.Identity, new Vector3(note.X, note.Y, (float)(Game.Progress - note.Millisecond) / (1000 * Phoenix.Settings.ApproachTime) * Phoenix.Settings.ApproachDistance)));
            Multimesh.SetInstanceColor(i, Color.FromHtml(Phoenix.Settings.Colors[note.Index % Phoenix.Settings.Colors.Length]));
        }
    }
}