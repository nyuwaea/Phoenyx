using System;
using System.IO;
using System.Security.Cryptography;
using Godot;

public struct Replay
{
    public string ID;
    public string MapID;
    public string MapFilePath;
    public string Player;
    public FileParser FileBuffer;
    public bool Valid;
    public float Length;

    public double Speed;
    public string[] Modifiers;
    public double ApproachRate;
    public double ApproachDistance;
    public double ApproachTime;
    public double FadeIn;
    public bool FadeOut;
    public bool Pushback;
    public double Parallax;
    public double FoV;
    public double NoteSize;

    public bool Complete;
    public ulong LastNote;
	public ulong LastFrame;
    public Vector2 CurrentPosition;
    public Frame[] Frames;
    public int FrameIndex;
    public float[] Notes;
    public float[] Skips;
    public int SkipIndex;

    public struct Frame(float progress, float x, float y)
    {
        public float Progress = progress;
        public Vector2 CursorPosition = new(x, y);
    
        public override string ToString() => $"({CursorPosition}) @{Progress}ms";
    }

    public Replay(string path)
    {
        FileBuffer = new(path);
        Valid = true;
        Complete = false;
        LastNote = 0;
        LastFrame = 0;

        string sig = FileBuffer.GetString(4);

		if (sig != "phxr")
		{
            Valid = false;
            
			ToastNotification.Notify("Replay file corrupted", 2);
			Logger.Error($"Replay file corrupted; invalid file sig {sig}");
			return;
		}

        if (FileBuffer.GetUInt8() == 1)
        {
            ID = path.GetFile().GetBaseName();
            Speed = FileBuffer.GetDouble();
            ApproachRate = FileBuffer.GetDouble();
            ApproachDistance = FileBuffer.GetDouble();
            ApproachTime = ApproachDistance / ApproachRate;
            FadeIn = FileBuffer.GetDouble();
            FadeOut = FileBuffer.GetBool();
            Pushback = FileBuffer.GetBool();
            Parallax = FileBuffer.GetDouble();
            FoV = FileBuffer.GetDouble();
            NoteSize = FileBuffer.GetDouble();
            Modifiers = FileBuffer.GetString((int)FileBuffer.GetUInt32()).Split("_");
            MapID = FileBuffer.GetString((int)FileBuffer.GetUInt32());
            MapFilePath = $"{Phoenyx.Constants.UserFolder}/maps/{MapID}.phxm";

            if (!File.Exists(MapFilePath))
            {
                Valid = false;
                ToastNotification.Notify("Replay map not found", 2);
                Logger.Log($"Replay map not found; map ID {MapID}");
                return;
            }
            
            Player = FileBuffer.GetString((int)FileBuffer.GetUInt32());
            Frames = new Frame[FileBuffer.GetUInt64()];
            CurrentPosition = new();
            FrameIndex = 0;
            SkipIndex = 0;

            for (int i = 0; i < Frames.Length; i++)
            {
                Frames[i] = new(FileBuffer.GetFloat(), FileBuffer.GetFloat(), FileBuffer.GetFloat());
            }

            Length = Frames.Length > 0 ? Frames[^1].Progress : 0;
            Notes = new float[FileBuffer.GetUInt64()];

            for (int i = 0; i < Notes.Length; i++)
            {
                Notes[i] = FileBuffer.GetFloat();
            }

            Skips = new float[FileBuffer.GetUInt64()];

            for (int i = 0; i < Skips.Length; i++)
            {
                Skips[i] = FileBuffer.GetFloat();
            }
        }
    }

    public override readonly string ToString() => ID;

    public override readonly bool Equals(object obj)
    {
        return GetHashCode() == obj.GetHashCode();
    }

    public override readonly int GetHashCode()
    {
        int hashCode = 0;
        byte[] bytes = $"{MapID}{Valid}{Speed}{Modifiers}".ToUtf16Buffer();
        byte[] hash = SHA256.HashData(bytes);

        for (int i = 0; i < hash.Length; i += 4)
        {
            hashCode += BitConverter.ToInt32(hash, i);  // this is so fucking ass
        }
        
        return hashCode;
    }

    public static bool operator ==(Replay left, Replay right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Replay left, Replay right)
    {
        return !(left == right);
    }
}