using System;
using Godot;
using Phoenix;

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

            float depth = (float)(note.Millisecond - Game.CurrentAttempt.Progress) / (1000 * Settings.ApproachTime) * Settings.ApproachDistance;
            float alpha = 1;
            
            if (Settings.FadeOut)
            {
                alpha -= Math.Clamp((Settings.ApproachDistance / 2 - depth) / (Settings.ApproachDistance / 2 + Constants.HitWindow * Settings.ApproachRate / 1000), 0, 0.75f);
            }

            if (!Settings.Pushback && note.Millisecond - Game.CurrentAttempt.Progress <= 0)
            {
                alpha = 0;
            }

            int j = Game.ToProcess - i - 1;

            Multimesh.SetInstanceTransform(j, new Transform3D(Basis.Identity, new Vector3(note.X, note.Y, -depth)));
            Multimesh.SetInstanceColor(j, Color.FromHtml(Settings.Colors[note.Index % Settings.Colors.Length] + ((int)(alpha * 255)).ToString("X2")));
        }
    }
}