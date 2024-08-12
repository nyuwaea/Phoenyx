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

        Transform3D transform = new Transform3D(new Vector3(Settings.NoteSize, 0, 0), new Vector3(0, Settings.NoteSize, 0), new Vector3(0, 0, Settings.NoteSize), Vector3.Zero);

        for (int i = 0; i < Game.ToProcess; i++)
        {
            Note note = Game.ProcessNotes[i];

            float depth = (note.Millisecond - (float)Game.CurrentAttempt.Progress) / (1000 * Settings.ApproachTime) * Settings.ApproachDistance / Game.CurrentAttempt.Speed;
            float alpha = Math.Clamp((1 - depth / Settings.ApproachDistance) / Settings.FadeIn, 0, 1);
            
            if (Settings.FadeOut)
            {
                alpha -= (Settings.ApproachDistance - depth) / (Settings.ApproachDistance + Constants.HitWindow * Settings.ApproachRate / 1000);
            }

            if (!Settings.Pushback && note.Millisecond - Game.CurrentAttempt.Progress <= 0)
            {
                alpha = 0;
            }

            int j = Game.ToProcess - i - 1;

            transform.Origin = new Vector3(note.X, note.Y, -depth);
            Multimesh.SetInstanceTransform(j, transform);
            Multimesh.SetInstanceColor(j, Color.FromHtml(Settings.Colors[note.Index % Settings.Colors.Length] + ((int)(Math.Clamp(alpha, 0, 1) * 255)).ToString("X2")));
        }
    }
}