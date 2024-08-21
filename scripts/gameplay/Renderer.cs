using System;
using Godot;
using Phoenyx;

public partial class Renderer : MultiMeshInstance3D
{
    public override void _Process(double delta)
    {
        if (!Game.Playing)
        {
            return;
        }

        Multimesh.InstanceCount = Game.ToProcess;

        Transform3D transform = new Transform3D(new Vector3((float)Settings.NoteSize / 2, 0, 0), new Vector3(0, (float)Settings.NoteSize / 2, 0), new Vector3(0, 0, (float)Settings.NoteSize / 2), Vector3.Zero);

        for (int i = 0; i < Game.ToProcess; i++)
        {
            Note note = Game.ProcessNotes[i];

            float depth = (note.Millisecond - (float)Game.CurrentAttempt.Progress) / (1000 * (float)Settings.ApproachTime) * (float)Settings.ApproachDistance / (float)Game.CurrentAttempt.Speed;
            float alpha = Math.Clamp((1 - (float)depth / (float)Settings.ApproachDistance) / (float)Settings.FadeIn, 0, 1);
            
            if (Settings.FadeOut)
            {
                alpha -= ((float)Settings.ApproachDistance - depth) / ((float)Settings.ApproachDistance + (float)Constants.HitWindow * (float)Settings.ApproachRate / 1000);
            }

            if (!Settings.Pushback && note.Millisecond - Game.CurrentAttempt.Progress <= 0)
            {
                alpha = 0;
            }
            
            int j = Game.ToProcess - i - 1;

            transform.Origin = new Vector3(note.X, note.Y, -depth);
            Multimesh.SetInstanceTransform(j, transform);
            Multimesh.SetInstanceColor(j, Color.FromHtml(Phoenyx.Skin.Colors[note.Index % Phoenyx.Skin.Colors.Length] + ((int)(Math.Clamp(alpha, 0, 1) * 255)).ToString("X2")));
        }
    }
}