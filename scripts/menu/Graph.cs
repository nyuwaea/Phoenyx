using Godot;
using System;
using System.Collections.Generic;

public partial class Graph : ColorRect
{
	public override void _Draw()
	{
		double start = Time.GetTicksUsec();
		Color hitColor = Color.FromHtml("00ff00ff");
		Color missColor = Color.FromHtml("ff000044");
		
		foreach (int miss in Runner.CurrentAttempt.MissesInfo)
		{
			int position = (int)(Size.X * miss / Runner.CurrentAttempt.Map.Length);
			DrawLine(Vector2.Right * position, new(position, Size.Y), missColor, 1);
		}

		foreach (Dictionary<string, float> hit in Runner.CurrentAttempt.HitsInfo)
		{
			DrawRect(new(Size.X * (hit["Time"] / Runner.CurrentAttempt.Map.Length), Size.Y * (hit["Offset"] / 55), Vector2.One), hitColor);
		}

		if (Runner.CurrentAttempt.DeathTime >= 0)
		{
			int position = (int)(Size.X * Runner.CurrentAttempt.DeathTime / Runner.CurrentAttempt.Map.Length);
			DrawLine(Vector2.Right * position, new(position, Size.Y), Color.Color8(255, 255, 0), 3);
		}

		Logger.Log($"RESULTS GRAPH: {(Time.GetTicksUsec() - start) / 1000}ms");
	}
}