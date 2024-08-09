using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Godot;
using Menu;

public class NoteComparer : IComparer<Note>
{
	public int Compare(Note a, Note b)
	{
		return a.Millisecond.CompareTo(b.Millisecond);
	}
}

public partial class MapParser : Node
{
	public static Map Parse(string path)
	{
		if (!File.Exists(path))
		{
			MainMenu.Notify("Invalid file path", 2);
			throw Logger.Error("Invalid file path");
		}

		Map map;
		Godot.FileAccess file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);

		string ext = path.GetExtension();

		switch (ext)
		{
			case "txt":
				map = SSMapV1(file.GetLine());
				break;
			case "sspm":
				map = SSPMV2(file);
				break;
			default:
				MainMenu.Notify("File extension not supported", 2);
				throw Logger.Error("File extension not supported");
		}

		file.Close();

		return map;
	}

	public static Map SSMapV1(string data)
	{
		Map map;

		try
		{
			string[] split = data.Split(",");
			
			Note[] notes = new Note[split.Length - 1];
			
			for (int i = 1; i < split.Length; i++)
			{
				string[] subsplit = split[i].Split("|");
				
				notes[i - 1] = new Note(i - 1, subsplit[2].ToInt(), -subsplit[0].ToFloat() + 1, subsplit[1].ToFloat() - 1);
			}

			map = new Map(){
				Notes = notes,
				Difficulty = 0,
				Length = notes[notes.Length - 1].Millisecond
			};
		}
		catch (Exception exception)
		{
			MainMenu.Notify($"SSMapV1 file corrupted", 2);
			throw Logger.Error($"SSMapV1 file corrupted; {exception.Message}");
		}

		return map;
	}

	public static Map SSPMV2(Godot.FileAccess file)
	{
		Map map;
		
		try
		{
			byte[] sig = file.GetBuffer(4);

			if (Encoding.ASCII.GetString(sig) != "SS+m")
			{
				throw new Exception("Incorrect file signature");
			}

			if (BitConverter.ToUInt16(file.GetBuffer(2)) != 2)
			{
				throw new Exception("Old SSPM format");
			}
			
			file.GetBuffer(4);	// reserved
			file.GetBuffer(20);	// hash

			uint mapLength = BitConverter.ToUInt32(file.GetBuffer(4));
			uint noteCount = BitConverter.ToUInt32(file.GetBuffer(4));

			file.GetBuffer(4);	// marker count

			int difficulty = file.GetBuffer(1)[0];

			file.GetBuffer(2);	// map rating

			bool hasAudio = BitConverter.ToBoolean(file.GetBuffer(1));
			bool hasCover = BitConverter.ToBoolean(file.GetBuffer(1));

			file.GetBuffer(1);	// 1mod
			file.GetBuffer(16);	// custom data offset & custom data length

			ulong audioByteOffset = BitConverter.ToUInt64(file.GetBuffer(8));
			ulong audioByteLength = BitConverter.ToUInt64(file.GetBuffer(8));

			ulong coverByteOffset = BitConverter.ToUInt64(file.GetBuffer(8));
			ulong coverByteLength = BitConverter.ToUInt64(file.GetBuffer(8));

			file.GetBuffer(16);	// marker definitions offset & marker definitions length

			ulong markerByteOffset = BitConverter.ToUInt64(file.GetBuffer(8));
			
			file.GetBuffer(8);	// marker byte length (can just use notecount)
			
			uint mapIdLength = BitConverter.ToUInt16(file.GetBuffer(2));
			string id = Encoding.UTF8.GetString(file.GetBuffer(mapIdLength));
			
			uint mapNameLength = BitConverter.ToUInt16(file.GetBuffer(2));
			string[] mapName = Encoding.UTF8.GetString(file.GetBuffer(mapNameLength)).Split("-", 2);
			
			string artist = null;
			string song = null;

			if (mapName.Length == 1)
			{
				song = mapName[0];
			}
			else
			{
				artist = mapName[0];
				song = mapName[1];
			}

			uint songNameLength = BitConverter.ToUInt16(file.GetBuffer(2));
			string songName = Encoding.UTF8.GetString(file.GetBuffer(songNameLength));	// why is this different?
			
			uint mapperCount = BitConverter.ToUInt16(file.GetBuffer(2));
			string[] mappers = new string[mapperCount];

			for (int i = 0; i < mapperCount; i++)
			{
				uint mapperNameLength = BitConverter.ToUInt16(file.GetBuffer(2));

				mappers[i] = Encoding.UTF8.GetString(file.GetBuffer(mapperNameLength));
			}
			
			byte[] audioBuffer = null;
			byte[] coverBuffer = null;

			if (hasAudio)
			{
				file.Seek(audioByteOffset);
				audioBuffer = file.GetBuffer((long)audioByteLength);
			}
			
			if (hasCover)
			{
				file.Seek(coverByteOffset);
				coverBuffer = file.GetBuffer((long)coverByteLength);
			}
			
			file.Seek(markerByteOffset);

			Note[] notes = new Note[noteCount];
			
			for (int i = 0; i < noteCount; i++)
			{
				uint millisecond = BitConverter.ToUInt32(file.GetBuffer(4));

				file.GetBuffer(1);	// marker type, always note

				bool isQuantum = BitConverter.ToBoolean(file.GetBuffer(1));
				float x;
				float y;
				
				if (isQuantum)
				{
					x = BitConverter.ToSingle(file.GetBuffer(4));
					y = BitConverter.ToSingle(file.GetBuffer(4));
				}
				else
				{
					x = file.GetBuffer(1)[0];
					y = file.GetBuffer(1)[0];
				}

				notes[i] = new Note(0, (int)millisecond, x - 1, -y + 1);
			}
			
			Array.Sort(notes, new NoteComparer());

			for (int i = 0; i < notes.Length; i++)
			{
				notes[i].Index = i;
			}

			map = new Map(notes, id, artist, song, mappers, difficulty, (int)mapLength, audioBuffer, coverBuffer);
		}
		catch (Exception exception)
		{
			MainMenu.Notify($"SSPMV2 file corrupted", 2);
			throw Logger.Error($"SSPMV2 file corrupted; {exception.Message}");
		}

		return map;
	}
}