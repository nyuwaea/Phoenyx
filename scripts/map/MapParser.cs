using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Godot;
using Phoenyx;

public class NoteComparer : IComparer<Note>
{
	public int Compare(Note a, Note b)
	{
		return a.Millisecond.CompareTo(b.Millisecond);
	}
}

public partial class MapParser : Node
{
	public static void Import(string[] files)
	{
		double start = Time.GetTicksUsec();
		int good = 0;
		int corrupted = 0;
		
		foreach (string file in files)
		{
			try
			{
				Decode(file, false);
				good++;
			}
			catch
			{
				corrupted++;
				continue;
			}
		}

		Logger.Log($"BULK IMPORT: {(Time.GetTicksUsec() - start) / 1000}ms; TOTAL: {good + corrupted}; CORRUPT: {corrupted}");
	}

	public static void Encode(Map map, bool logBenchmark = true)
	{
		double start = Time.GetTicksUsec();

		if (File.Exists($"{Constants.UserFolder}/maps/{map.ID}.phxm"))
		{
			File.Delete($"{Constants.UserFolder}/maps/{map.ID}.phxm");
		}

		if (!Directory.Exists($"{Constants.UserFolder}/cache/phxmencode"))
		{
			Directory.CreateDirectory($"{Constants.UserFolder}/cache/phxmencode");
		}
		
		File.WriteAllText($"{Constants.UserFolder}/cache/phxmencode/metadata.json", map.EncodeMeta());

		Godot.FileAccess objects = Godot.FileAccess.Open($"{Constants.UserFolder}/cache/phxmencode/objects.phxmo", Godot.FileAccess.ModeFlags.Write);

		/*
			uint32; ms
			1 byte; quantum
			1 byte OR int32; x
			1 byte OR int32; y
		*/

		objects.Store32(12);	// type count
		objects.Store32((uint)map.Notes.Length);	// note count

		foreach (Note note in map.Notes)
		{
			bool quantum = (int)note.X != note.X || (int)note.Y != note.Y || note.X < -1 || note.X > 1 || note.Y < -1 || note.Y > 1;
			
			objects.Store32((uint)note.Millisecond);
			objects.Store8(Convert.ToByte(quantum));
			
			if (quantum)
			{
				objects.Store32(BitConverter.SingleToUInt32Bits(note.X));
				objects.Store32(BitConverter.SingleToUInt32Bits(note.Y));
			}
			else
			{
				objects.Store8((byte)(note.X + 1));	// 0x00 = -1, 0x01 = 0, 0x02 = 1
				objects.Store8((byte)(note.Y + 1));	
			}
		}

		objects.Store32(0);	// timing point count
		objects.Store32(0);	// brightness count
		objects.Store32(0);	// contrast count
		objects.Store32(0);	// saturation count
		objects.Store32(0);	// blur count
		objects.Store32(0);	// fov count
		objects.Store32(0);	// tint count
		objects.Store32(0);	// position count
		objects.Store32(0);	// rotation count
		objects.Store32(0);	// ar factor count
		objects.Store32(0);	// text count

		if (map.AudioBuffer != null)
		{
			Godot.FileAccess audio = Godot.FileAccess.Open($"{Constants.UserFolder}/cache/phxmencode/audio.{map.AudioExt}", Godot.FileAccess.ModeFlags.Write);
			audio.StoreBuffer(map.AudioBuffer);
			audio.Close();
		}

		if (map.CoverBuffer != null)
		{
			Godot.FileAccess cover = Godot.FileAccess.Open($"{Constants.UserFolder}/cache/phxmencode/cover.png", Godot.FileAccess.ModeFlags.Write);
			cover.StoreBuffer(map.CoverBuffer);
			cover.Close();
		}

		if (map.VideoBuffer != null)
		{
			Godot.FileAccess video = Godot.FileAccess.Open($"{Constants.UserFolder}/cache/phxmencode/video.mp4", Godot.FileAccess.ModeFlags.Write);
			video.StoreBuffer(map.VideoBuffer);
			video.Close();
		}

		objects.Close();

		ZipFile.CreateFromDirectory($"{Constants.UserFolder}/cache/phxmencode", $"{Constants.UserFolder}/maps/{map.ID}.phxm", CompressionLevel.NoCompression, false);

		foreach (string filePath in Directory.GetFiles($"{Constants.UserFolder}/cache/phxmencode"))
		{
			File.Delete(filePath);
		}

		Directory.Delete($"{Constants.UserFolder}/cache/phxmencode");

		if (logBenchmark)
		{
			Logger.Log($"Encoding PHXM: {(Time.GetTicksUsec() - start) / 1000}ms");
		}
	}

	public static Map Decode(string path, bool logBenchmark = true)
	{
		if (!File.Exists(path))
		{
			ToastNotification.Notify("Invalid file path", 2);
			throw Logger.Error($"Invalid file path; {path}");
		}

		Map map;
		string ext = path.GetExtension();
		double start = Time.GetTicksUsec();

		switch (ext)
		{
			case "phxm":
				map = PHXM(path);
				break;
			case "sspm":
				map = SSPMV2(path);
				break;
			case "txt":
				map = SSMapV1(path);
				break;
			default:
				ToastNotification.Notify("File extension not supported", 2);
				throw Logger.Error("File extension not supported");
		}

		if (logBenchmark)
		{
			Logger.Log($"Decoding {ext.ToUpper()}: {(Time.GetTicksUsec() - start) / 1000}ms");
		}

		if (!File.Exists($"{Constants.UserFolder}/maps/{map.ID}.phxm"))
		{
			Encode(map, logBenchmark);
		}

		return map;
	}

	public static Map SSMapV1(string path)
	{
		string[] pathSplit = path.Split("\\");
		string name = pathSplit[pathSplit.Length - 1].Replace(".txt", "");
		Godot.FileAccess file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
		Map map;

		try
		{
			string[] split = file.GetLine().Split(",");
			
			Note[] notes = new Note[split.Length - 1];
			
			for (int i = 1; i < split.Length; i++)
			{
				string[] subsplit = split[i].Split("|");
				
				notes[i - 1] = new Note(i - 1, subsplit[2].ToInt(), -subsplit[0].ToFloat() + 1, subsplit[1].ToFloat() - 1);
			}

			map = new(notes, name);
		}
		catch (Exception exception)
		{
			ToastNotification.Notify($"SSMapV1 file corrupted", 2);
			throw Logger.Error($"SSMapV1 file corrupted; {exception.Message}");
		}

		file.Close();

		return map;
	}

	public static Map SSPMV2(string path)
	{
		FileParser file = new(path);
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

			ulong customDataOffset = file.GetUInt64();
			ulong customDataLength = file.GetUInt64();

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
			string[] mapName = file.GetString((int)mapNameLength).Split(" - ", 2);
			
			string artist = null;
			string song = null;

			if (mapName.Length == 1)
			{
				song = mapName[0].StripEdges();
			}
			else
			{
				artist = mapName[0].StripEdges();
				song = mapName[1].StripEdges();
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
			string difficultyName = null;

			file.Seek((int)customDataOffset);
			file.Skip(2);	// skip number of fields, only care about diff name
			
			if (file.GetString(file.GetUInt16()) == "difficulty_name")
			{
				int length = 0;

				switch (file.Get(1)[0])
				{
					case 9:
						length = file.GetUInt16();
						break;
					case 11:
						length = (int)file.GetUInt32();
						break;
				}
				
				difficultyName = file.GetString(length);
			}
			
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
				int millisecond = (int)file.GetUInt32();

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

				notes[i] = new Note(0, millisecond, x - 1, -y + 1);
			}
			
			Array.Sort(notes, new NoteComparer());

			for (int i = 0; i < notes.Length; i++)
			{
				notes[i].Index = i;
			}
			
			map = new Map(notes, id, artist, song, 0, mappers, difficulty, difficultyName, (int)mapLength, audioBuffer, coverBuffer);
		}
		catch (Exception exception)
		{
			ToastNotification.Notify($"SSPMV2 file corrupted", 2);
			throw Logger.Error($"SSPMV2 file corrupted; {exception.Message}");
		}

		return map;
	}

	public static Map PHXM(string path)
	{
		if (Directory.Exists($"{Constants.UserFolder}/cache/phxmdecode"))
		{
			foreach (string filePath in Directory.GetFiles($"{Constants.UserFolder}/cache/phxmdecode"))
			{
				File.Delete(filePath);
			}

			Directory.Delete($"{Constants.UserFolder}/cache/phxmdecode");
		}
		
		Map map;

		try
		{
			ZipArchive file = ZipFile.OpenRead(path);
			Stream metaStream = file.GetEntry("metadata.json").Open();
			Stream objectsStream = file.GetEntry("objects.phxmo").Open();
			
			byte[] metaBuffer = new byte[metaStream.Length];
			byte[] objectsBuffer = new byte[objectsStream.Length];
			byte[] audioBuffer = null;
			byte[] coverBuffer = null;
			byte[] videoBuffer = null;
			metaStream.Read(metaBuffer, 0, (int)metaStream.Length);
			objectsStream.Read(objectsBuffer, 0, (int)objectsStream.Length);
			metaStream.Close();
			objectsStream.Close();

			Godot.Collections.Dictionary metadata = (Godot.Collections.Dictionary)Json.ParseString(Encoding.UTF8.GetString(metaBuffer));
			FileParser objects = new(objectsBuffer);

			if ((bool)metadata["HasAudio"])
			{
				Stream audioStream = file.GetEntry($"audio.{metadata["AudioExt"]}").Open();
				audioBuffer = new byte[audioStream.Length];
				audioStream.Read(audioBuffer, 0, (int)audioStream.Length);
				audioStream.Close();
			}

			if ((bool)metadata["HasCover"])
			{
				Stream coverStream = file.GetEntry("cover.png").Open();
				coverBuffer = new byte[coverStream.Length];
				coverStream.Read(coverBuffer, 0, (int)coverStream.Length);
				coverStream.Close();
			}

			if ((bool)metadata["HasVideo"])
			{
				Stream videoStream = file.GetEntry("video.mp4").Open();
				videoBuffer = new byte[videoStream.Length];
				videoStream.Read(videoBuffer, 0, (int)videoStream.Length);
				videoStream.Close();
			}

			uint typeCount = objects.GetUInt32();
			uint noteCount = objects.GetUInt32();
			
			Note[] notes = new Note[noteCount];
			
			for (int i = 0; i < noteCount; i++)
			{
				int ms = (int)objects.GetUInt32();
				bool quantum = objects.GetBool();
				float x;
				float y;
				
				if (quantum)
				{
					x = objects.GetFloat();
					y = objects.GetFloat();
				}
				else
				{
					x = objects.Get(1)[0] - 1;
					y = objects.Get(1)[0] - 1;
				}
				
				notes[i] = new(i, ms, x, y);
			}
			
			map = new(notes, (string)metadata["ID"], (string)metadata["Artist"], (string)metadata["Title"], 0, (string[])metadata["Mappers"], (int)metadata["Difficulty"], (string)metadata["DifficultyName"], (int)metadata["Length"], audioBuffer, coverBuffer, videoBuffer);
		}
		catch (Exception exception)
		{
			ToastNotification.Notify($"PHXM file corrupted", 2);
			throw Logger.Error($"PHXM file corrupted; {exception.Message}");
		}

		return map;
	}
}