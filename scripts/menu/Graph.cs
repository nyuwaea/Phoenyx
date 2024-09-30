using Godot;

public partial class Graph : ColorRect
{
	public override void _Draw()
	{
		double start = Time.GetTicksUsec();
		Color hitColor = Color.FromHtml("00ff00ff");
		Color missColor = Color.FromHtml("ff000044");

		for (ulong i = Runner.CurrentAttempt.FirstNote; i < (ulong)Runner.CurrentAttempt.HitsInfo.Length; i++)
		{
			float offset = Runner.CurrentAttempt.HitsInfo[i];

			if (offset < 0)
			{
				int position = (int)(Size.X * Runner.CurrentAttempt.Map.Notes[i].Millisecond / Runner.CurrentAttempt.Map.Length);
				DrawLine(Vector2.Right * position, new(position, Size.Y), missColor, 1);
			}
			else
			{
				DrawRect(new(Size.X * (Runner.CurrentAttempt.Map.Notes[i].Millisecond / (float)Runner.CurrentAttempt.Map.Length), Size.Y * (offset / 55), Vector2.One), hitColor);
			}
		}

		if (Runner.CurrentAttempt.DeathTime >= 0)
		{
			int position = (int)(Size.X * Runner.CurrentAttempt.DeathTime / Runner.CurrentAttempt.Map.Length);
			DrawLine(Vector2.Right * position, new(position, Size.Y), Color.Color8(255, 255, 0), 3);
		}

		Logger.Log($"RESULTS GRAPH: {(Time.GetTicksUsec() - start) / 1000}ms");
	}
}