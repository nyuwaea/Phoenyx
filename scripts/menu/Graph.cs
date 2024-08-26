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
			DrawLine(Vector2.Right * position, new Vector2(position, Size.Y), missColor, 1);
		}

		foreach (Dictionary<string, int> hit in Runner.CurrentAttempt.HitsInfo)
		{
			DrawRect(new(Size.X * (hit["Time"] / (float)Runner.CurrentAttempt.Map.Length), Size.Y * (hit["Offset"] / 55f), Vector2.One), hitColor);
		}

		Logger.Log($"RESULTS GRAPH: {(Time.GetTicksUsec() - start) / 1000}ms");
	}
}
