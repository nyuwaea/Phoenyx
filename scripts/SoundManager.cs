using Godot;

public partial class SoundManager : Node
{
    public static AudioStreamPlayer HitSound;
    public static AudioStreamPlayer MissSound;
    public static AudioStreamPlayer FailSound;

    public override void _Ready()
    {
        HitSound = new();
        MissSound = new();
        FailSound = new();

        AddChild(HitSound);
        AddChild(MissSound);
        AddChild(FailSound);
    }

    public static void UpdateSounds()
    {
        HitSound.Stream = Lib.Audio.LoadStream(Phoenyx.Skin.HitSoundBuffer);
        FailSound.Stream = Lib.Audio.LoadStream(Phoenyx.Skin.FailSoundBuffer);
    }
}