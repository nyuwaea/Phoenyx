using System.Text;
using Godot;

namespace Lib;

public class Audio
{
    public static AudioStream LoadStream(byte[] buffer)
	{
		AudioStream stream;

		if (buffer == null || buffer.Length < 4)
		{
			FileAccess file = FileAccess.Open("res://sounds/quiet.mp3", FileAccess.ModeFlags.Read);
			byte[] quietBuffer = file.GetBuffer((long)file.GetLength());

			file.Close();

			return new AudioStreamMP3(){Data = quietBuffer};
		}

		if (Encoding.UTF8.GetString(buffer[0..4]) == "OggS")
		{
			stream = AudioStreamOggVorbis.LoadFromBuffer(buffer);
		}
		else
		{
			stream = new AudioStreamMP3(){Data = buffer};
		}

		return stream;
	}
}