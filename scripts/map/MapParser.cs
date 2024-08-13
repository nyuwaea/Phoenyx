using System;
using System.Collections.Generic;
using System.IO;
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
			ToastNotification.Notify("Invalid file path", 2);
			throw Logger.Error("Invalid file path");
		}

		Map map;
		Godot.FileAccess file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
		string ext = path.GetExtension();
		double start = Time.GetTicksUsec();

		switch (ext)
		{
			case "txt":
				map = SSMapV1(file.GetLine());
				break;
			case "sspm":
				map = SSPMV2(new FileParser(path));
				break;
			default:
				ToastNotification.Notify("File extension not supported", 2);
				throw Logger.Error("File extension not supported");
		}

		file.Close();
		Logger.Log($"Parsing: {(Time.GetTicksUsec() - start) / 1000}ms");

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
			ToastNotification.Notify($"SSMapV1 file corrupted", 2);
			throw Logger.Error($"SSMapV1 file corrupted; {exception.Message}");
		}

		return map;
	}

	public static Map SSPMV2(FileParser file)
	{
		Map map;

		try
		{
			if (file.GetString(4) != "SS+m")
			{
				throw new Exception("Incorrect file signature");
			}

			if (file.GetUInt16() != 2)
			{
				throw new Exception("Old SSPM format");
			}

			file.Skip(4);	// reserved
			file.Skip(20);	// hash

			uint mapLength = file.GetUInt32();
			uint noteCount = file.GetUInt32();

			file.Skip(4);	// marker count

			int difficulty = file.Get(1)[0];

			file.Skip(2);	// map rating

			bool hasAudio = file.GetBool();
			bool hasCover = file.GetBool();

			file.Skip(1);	// 1mod
			file.Skip(16);	// custom data offset & custom data length

			ulong audioByteOffset = file.GetUInt64();
			ulong audioByteLength = file.GetUInt64();

			ulong coverByteOffset = file.GetUInt64();
			ulong coverByteLength = file.GetUInt64();

			file.Skip(16);	// marker definitions offset & marker definitions length

			ulong markerByteOffset = file.GetUInt64();
			
			file.Skip(8);	// marker byte length (can just use notecount)
			
			uint mapIdLength = file.GetUInt16();
			string id = file.GetString((int)mapIdLength);
			
			uint mapNameLength = file.GetUInt16();
			string[] mapName = file.GetString((int)mapNameLength).Split("-", 2);
			
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

			uint songNameLength = file.GetUInt16();

			file.Skip((int)songNameLength);	// why is this different?
			
			uint mapperCount = file.GetUInt16();
			string[] mappers = new string[mapperCount];

			for (int i = 0; i < mapperCount; i++)
			{
				uint mapperNameLength = file.GetUInt16();

				mappers[i] = file.GetString((int)mapperNameLength);
			}
			
			byte[] audioBuffer = null;
			byte[] coverBuffer = null;

			if (hasAudio)
			{
				file.Seek((int)audioByteOffset);
				audioBuffer = file.Get((int)audioByteLength);
			}
			
			if (hasCover)
			{
				file.Seek((int)coverByteOffset);
				coverBuffer = file.Get((int)coverByteLength);
			}
			
			file.Seek((int)markerByteOffset);

			Note[] notes = new Note[noteCount];
			
			for (int i = 0; i < noteCount; i++)
			{
				uint millisecond = file.GetUInt32();

				file.Skip(1);	// marker type, always note

				bool isQuantum = file.GetBool();
				float x;
				float y;
				
				if (isQuantum)
				{
					x = file.GetFloat();
					y = file.GetFloat();
				}
				else
				{
					x = file.Get(1)[0];
					y = file.Get(1)[0];
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
			ToastNotification.Notify($"SSPMV2 file corrupted", 2);
			throw Logger.Error($"SSPMV2 file corrupted; {exception.Message}");
		}

		return map;
	}
}