using System;
using Godot;

namespace Space;

public partial class Grid : Node3D
{
    private double LastFrame = Time.GetTicksUsec();
    private StandardMaterial3D TileMaterial;
    private Godot.Environment Environment;

    public Color Colour = Color.Color8(255, 255, 255);

    public override void _Ready()
    {
        TileMaterial = (GetNode<MeshInstance3D>("Top").Mesh as PlaneMesh).Material as StandardMaterial3D;
        Environment = GetNode<WorldEnvironment>("WorldEnvironment").Environment;
    }

    public override void _Process(double delta)
    {
        ulong now = Time.GetTicksUsec();
		delta = (now - LastFrame) / 1000000;
		LastFrame = now;
        Colour = Colour.Lerp(Runner.CurrentAttempt.LastHitColour, (float)delta * 12);

        TileMaterial.AlbedoColor = Colour;
        TileMaterial.Uv1Offset += Vector3.Up * (float)delta * 2;
        Environment.FogLightColor = Colour / 10;
    }
}