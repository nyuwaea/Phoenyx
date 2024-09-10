using System.Collections.Generic;
using Godot;
using Menu;

public partial class SoundManager : Node
{
    public static AudioStreamPlayer HitSound;
    public static AudioStreamPlayer MissSound;
    public static AudioStreamPlayer FailSound;
    public static AudioStreamPlayer Song;

    public delegate void JukeboxPlayedHandler(Map map);
    public static event JukeboxPlayedHandler JukeboxPlayed;

	public static string[] JukeboxQueue = [];
    public static Dictionary<string, int> JukeboxQueueInverse = [];
	public static int JukeboxIndex = 0;
	public static bool JukeboxPaused = false;
	public static ulong LastRewind = 0;
    public static Map Map;

    public override void _Ready()
    {
        HitSound = new();
        MissSound = new();
        FailSound = new();
        Song = new();

        AddChild(HitSound);
        AddChild(MissSound);
        AddChild(FailSound);
        AddChild(Song);

        Song.Finished += () => {
            switch (SceneManager.Scene.Name)
            {
                case "SceneMenu":
                    JukeboxIndex++;
                    PlayJukebox(JukeboxIndex);
                    break;
                case "SceneResults":
                    PlayJukebox(JukeboxIndex);
                    break;
                default:
                    break;
            }
		};
    }

    public static void PlayJukebox(int index = -1, bool setRichPresence = true)
	{
        index = index == -1 ? JukeboxIndex : index;

		if (index >= JukeboxQueue.Length)
		{
			index = 0;
		}
		else if (index < 0)
		{
			index = JukeboxQueue.Length - 1;
		}

		if (JukeboxQueue.Length == 0)
		{
			return;
		}

		Map = MapParser.Decode(JukeboxQueue[index], false);

		if (Map.AudioBuffer == null)
		{
			JukeboxIndex++;
			PlayJukebox(JukeboxIndex);
		}

        JukeboxPlayed.Invoke(Map);

		if (SceneManager.Scene.Name == "SceneMenu")
        {
            MainMenu.Control.GetNode("Jukebox").GetNode<Label>("Title").Text = Map.PrettyTitle;
        }

		Song.Stream = Lib.Audio.LoadStream(Map.AudioBuffer);
		Song.Play();

		if (setRichPresence)
        {
            Phoenyx.Util.DiscordRPC.Call("Set", "state", $"Listening to {Map.PrettyTitle}");
        }
	}

    public static void UpdateSounds()
    {
        HitSound.Stream = Lib.Audio.LoadStream(Phoenyx.Skin.HitSoundBuffer);
        FailSound.Stream = Lib.Audio.LoadStream(Phoenyx.Skin.FailSoundBuffer);
    }
}