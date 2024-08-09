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
            Note note = Game.ProcessNotes[i];

            float depth = (float)(note.Millisecond - Game.CurrentAttempt.Progress) / (1000 * Settings.ApproachTime) * Settings.ApproachDistance / Game.CurrentAttempt.Speed;
            float alpha = 1;
            
            if (Settings.FadeOut)
            {
                alpha -= Math.Clamp((Settings.ApproachDistance - depth) / (Settings.ApproachDistance + Constants.HitWindow * Settings.ApproachRate / 1000), 0, 1);
            }

            if (!Settings.Pushback && note.Millisecond - Game.CurrentAttempt.Progress <= 0)
            {
                alpha = 0;
            }

            int j = Game.ToProcess - i - 1;

            Multimesh.SetInstanceTransform(j, new Transform3D(new Vector3(Settings.NoteSize, 0, 0), new Vector3(0, Settings.NoteSize, 0), new Vector3(0, 0, Settings.NoteSize), new Vector3(note.X, note.Y, -depth)));
            Multimesh.SetInstanceColor(j, Color.FromHtml(Settings.Colors[note.Index % Settings.Colors.Length] + ((int)(alpha * 255)).ToString("X2")));
        }
    }
}