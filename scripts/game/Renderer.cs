using System;
using Godot;

public partial class Renderer : MultiMeshInstance3D
{
    public override void _Process(double delta)
    {
        if (!Runner.Playing)
        {
            return;
        }

        Multimesh.InstanceCount = Runner.ToProcess;

        float ar = (float)(Runner.CurrentAttempt.IsReplay ? Runner.CurrentAttempt.Replays[0].ApproachRate : Phoenyx.Settings.ApproachRate);
        float ad = (float)(Runner.CurrentAttempt.IsReplay ? Runner.CurrentAttempt.Replays[0].ApproachDistance : Phoenyx.Settings.ApproachDistance);
        float at = ad / ar;
        float fadeIn = (float)(Runner.CurrentAttempt.IsReplay ? Runner.CurrentAttempt.Replays[0].FadeIn : Phoenyx.Settings.FadeIn);
        bool fadeOut = Runner.CurrentAttempt.IsReplay ? Runner.CurrentAttempt.Replays[0].FadeOut : Phoenyx.Settings.FadeOut;
        bool pushback = Runner.CurrentAttempt.IsReplay ? Runner.CurrentAttempt.Replays[0].Pushback : Phoenyx.Settings.Pushback;
        float noteSize = (float)(Runner.CurrentAttempt.IsReplay ? Runner.CurrentAttempt.Replays[0].NoteSize : Phoenyx.Settings.NoteSize);
        Transform3D transform = new(new Vector3(noteSize / 2, 0, 0), new Vector3(0, noteSize / 2, 0), new Vector3(0, 0, noteSize / 2), Vector3.Zero);
        
        for (int i = 0; i < Runner.ToProcess; i++)
        {
            Note note = Runner.ProcessNotes[i];
            float depth = (note.Millisecond - (float)Runner.CurrentAttempt.Progress) / (1000 * at) * ad / (float)Runner.CurrentAttempt.Speed;
            float alpha = Math.Clamp((1 - (float)depth / ad) / (fadeIn / 100), 0, 1);
            
            if (Runner.CurrentAttempt.Mods["Ghost"])
            {
                alpha -= Math.Min(1, (ad - depth) / (ad / 2));
            }
            else if (fadeOut)
            {
                alpha -= (ad - depth) / (ad + (float)Phoenyx.Constants.HitWindow * ar / 1000);
            }

            if (!pushback && note.Millisecond - Runner.CurrentAttempt.Progress <= 0)
            {
                alpha = 0;
            }
            
            int j = Runner.ToProcess - i - 1;
            Color color = Phoenyx.Skin.Colors[note.Index % Phoenyx.Skin.Colors.Length];
            
            transform.Origin = new Vector3(note.X, note.Y, -depth);
            color.A = alpha;
            Multimesh.SetInstanceTransform(j, transform);
            Multimesh.SetInstanceColor(j, color);
        }
    }
}