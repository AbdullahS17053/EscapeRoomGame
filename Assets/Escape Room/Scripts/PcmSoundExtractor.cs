using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

/// <summary>
/// Reads uncompressed PCM sound out of MP4 and QuickTime video files. Unity cannot decode
/// PCM inside MP4, and Android lists neither PCM in MP4 nor QuickTime files, so such clips
/// play without sound. This copies their sound into a 16-bit WAV that can play beside the
/// muted video. Plain C# with no Unity calls, so it can run on a worker thread.
/// </summary>
public static class PcmSoundExtractor
{
    public const string SoundFolderName = "extracted-sound";

    public sealed class PcmTrack
    {
        public string Format;
        public int Channels;
        public int SampleRate;
        public int BitsPerSample;
        public bool IsFloat;
        public bool IsUnsigned;
        public bool LittleEndian;
        public long LeadingSilenceFrames;
        public long SkipFrames;
        internal readonly List<long> ChunkOffsets = new List<long>();
        internal readonly List<long> ChunkSizes = new List<long>();

        public int BytesPerSample => BitsPerSample / 8;
        public int BytesPerFrame => Channels * BytesPerSample;

        public long FrameCount
        {
            get
            {
                long bytes = 0;
                foreach (long size in ChunkSizes) bytes += size;
                return bytes / BytesPerFrame;
            }
        }

        public double DurationSeconds =>
            (double)(Math.Max(0, FrameCount - SkipFrames) + LeadingSilenceFrames) / SampleRate;
    }

    private struct Box
    {
        public string Type;
        public int Body;
        public int End;
    }

    /// <summary>
    /// Returns the clip's first PCM sound track, or null when it has none or the file
    /// is not an MP4 or QuickTime file.
    /// </summary>
    public static PcmTrack FindPcmTrack(string videoPath)
    {
        byte[] moov;
        using (FileStream stream = File.OpenRead(videoPath))
            moov = ReadTopLevelBox(stream, "moov");
        if (moov == null) return null;

        uint movieTimescale = 0;
        foreach (Box box in Children(moov, 0, moov.Length))
        {
            if (box.Type == "mvhd")
                movieTimescale = U32(moov, box.Body + (moov[box.Body] == 1 ? 20 : 12));
        }

        foreach (Box trak in Children(moov, 0, moov.Length))
        {
            if (trak.Type != "trak") continue;
            PcmTrack track = ParseTrack(moov, trak, movieTimescale);
            if (track != null) return track;
        }
        return null;
    }

    /// <summary>
    /// Where the WAV for this video lives. The name carries the video's size and time,
    /// so a replaced video never reuses an old WAV.
    /// </summary>
    public static string WavPathFor(string videoPath)
    {
        var info = new FileInfo(videoPath);
        string folder = Path.Combine(info.DirectoryName ?? "", SoundFolderName);
        return Path.Combine(folder, info.Name + "." + info.Length + "-" + info.LastWriteTimeUtc.Ticks + ".wav");
    }

    /// <summary>
    /// Returns the path of a WAV holding the clip's PCM sound, writing it first if needed.
    /// Returns null when the clip has no PCM sound. Not safe to call twice at once for
    /// the same video.
    /// </summary>
    public static string EnsureWav(string videoPath)
    {
        string wavPath = WavPathFor(videoPath);
        if (File.Exists(wavPath)) return wavPath;

        PcmTrack track = FindPcmTrack(videoPath);
        if (track == null) return null;

        string folder = Path.GetDirectoryName(wavPath);
        Directory.CreateDirectory(folder);
        DeleteOldCopies(folder, Path.GetFileName(videoPath), wavPath);
        WriteWav(videoPath, track, wavPath);
        return wavPath;
    }

    /// <summary>Writes the track as 16-bit PCM WAV, through a temporary file.</summary>
    public static void WriteWav(string videoPath, PcmTrack track, string wavPath)
    {
        string tempPath = wavPath + ".tmp";
        int channels = track.Channels;
        int inBytesPerSample = track.BytesPerSample;
        int inBytesPerFrame = track.BytesPerFrame;
        long dataBytes = 0;

        using (FileStream input = File.OpenRead(videoPath))
        using (var output = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16))
        {
            WriteWavHeader(output, channels, track.SampleRate, 0);

            var silence = new byte[channels * 2 * 1024];
            for (long left = track.LeadingSilenceFrames; left > 0; left -= 1024)
            {
                int frames = (int)Math.Min(left, 1024);
                output.Write(silence, 0, frames * channels * 2);
                dataBytes += frames * channels * 2;
            }

            int blockFrames = Math.Max(1, (1 << 18) / inBytesPerFrame);
            var inBuffer = new byte[blockFrames * inBytesPerFrame];
            var outBuffer = new byte[blockFrames * channels * 2];
            long skipBytes = track.SkipFrames * inBytesPerFrame;

            for (int c = 0; c < track.ChunkOffsets.Count; c++)
            {
                long chunkBytes = track.ChunkSizes[c];
                if (skipBytes >= chunkBytes)
                {
                    skipBytes -= chunkBytes;
                    continue;
                }

                input.Position = track.ChunkOffsets[c] + skipBytes;
                long remaining = chunkBytes - skipBytes;
                skipBytes = 0;

                while (remaining >= inBytesPerFrame)
                {
                    int want = (int)Math.Min(remaining - remaining % inBytesPerFrame, inBuffer.Length);
                    int got = ReadFully(input, inBuffer, want);
                    got -= got % inBytesPerFrame;
                    if (got == 0) break;

                    int samples = got / inBytesPerSample;
                    for (int s = 0; s < samples; s++)
                    {
                        short value = ToInt16(inBuffer, s * inBytesPerSample, track);
                        outBuffer[s * 2] = (byte)value;
                        outBuffer[s * 2 + 1] = (byte)(value >> 8);
                    }
                    output.Write(outBuffer, 0, samples * 2);
                    dataBytes += samples * 2;
                    remaining -= got;
                    if (got < want) break;
                }
            }

            if (dataBytes > uint.MaxValue - 36)
                throw new IOException("Sound is too long for a WAV file: " + videoPath);

            output.Position = 0;
            WriteWavHeader(output, channels, track.SampleRate, dataBytes);
        }

        if (File.Exists(wavPath)) File.Delete(wavPath);
        File.Move(tempPath, wavPath);
    }

    private static PcmTrack ParseTrack(byte[] data, Box trak, uint movieTimescale)
    {
        Box mdia = FindChild(data, trak, "mdia");
        if (mdia.Type == null) return null;

        Box hdlr = FindChild(data, mdia, "hdlr");
        if (hdlr.Type == null || Ascii(data, hdlr.Body + 8) != "soun") return null;

        Box mdhd = FindChild(data, mdia, "mdhd");
        uint mediaTimescale = mdhd.Type == null ? 0 : U32(data, mdhd.Body + (data[mdhd.Body] == 1 ? 20 : 12));

        Box stbl = FindChild(data, FindChild(data, mdia, "minf"), "stbl");
        if (stbl.Type == null) return null;

        Box stsd = FindChild(data, stbl, "stsd");
        if (stsd.Type == null || stsd.Body + 16 > stsd.End) return null;

        PcmTrack track = ParseSampleEntry(data, stsd.Body + 8, stsd.End, mediaTimescale);
        if (track == null) return null;
        if (!ReadChunks(data, stbl, track)) return null;

        ApplyEditList(data, trak, track, movieTimescale, mediaTimescale);
        return track;
    }

    private static PcmTrack ParseSampleEntry(byte[] data, int entry, int limit, uint mediaTimescale)
    {
        int entrySize = (int)U32(data, entry);
        int entryEnd = Math.Min(limit, entry + entrySize);
        if (entry + 36 > entryEnd) return null;

        string format = Ascii(data, entry + 4);
        int version = U16(data, entry + 16);
        int channels = U16(data, entry + 24);
        int bits = U16(data, entry + 26);
        int sampleRate = (int)(U32(data, entry + 32) >> 16);

        // ISO formats keep their extra fields in child boxes; QuickTime versions 1 and 2
        // add fields before the children.
        bool isoFormat = format == "ipcm" || format == "fpcm";
        int children = entry + 36;
        if (!isoFormat && version == 1) children = entry + 52;
        if (!isoFormat && version == 2) children = entry + 72;
        if (children > entryEnd) return null;

        var track = new PcmTrack { Format = format, Channels = channels, SampleRate = sampleRate };
        int pcmFlags = -1, pcmSize = -1, endian = -1, isoRate = -1;
        FindAudioDetails(data, children, entryEnd, ref pcmFlags, ref pcmSize, ref endian, ref isoRate);

        switch (format)
        {
            case "ipcm":
            case "fpcm":
                track.IsFloat = format == "fpcm";
                track.BitsPerSample = pcmSize > 0 ? pcmSize : bits;
                track.LittleEndian = pcmFlags >= 0 && (pcmFlags & 1) == 1;
                if (isoRate > 0) track.SampleRate = isoRate;
                break;
            case "sowt":
                track.BitsPerSample = bits == 8 ? 8 : 16;
                track.LittleEndian = true;
                break;
            case "twos":
                track.BitsPerSample = bits == 8 ? 8 : 16;
                track.LittleEndian = false;
                break;
            case "in24":
            case "in32":
            case "fl32":
            case "fl64":
                track.BitsPerSample = format == "in24" ? 24 : format == "fl64" ? 64 : 32;
                track.IsFloat = format.StartsWith("fl", StringComparison.Ordinal);
                track.LittleEndian = endian == 1;
                break;
            case "raw ":
                track.BitsPerSample = 8;
                track.IsUnsigned = true;
                break;
            case "lpcm":
                if (version != 2) return null;
                uint flags = U32(data, entry + 60);
                track.SampleRate = (int)Math.Round(BitConverter.Int64BitsToDouble((long)U64(data, entry + 40)));
                track.Channels = (int)U32(data, entry + 48);
                track.BitsPerSample = (int)U32(data, entry + 56);
                track.IsFloat = (flags & 1) != 0;
                track.LittleEndian = (flags & 2) == 0;
                break;
            default:
                return null;
        }

        if (track.SampleRate <= 0) track.SampleRate = (int)mediaTimescale;
        bool supportedBits = track.IsFloat
            ? track.BitsPerSample == 32 || track.BitsPerSample == 64
            : track.BitsPerSample == 8 || track.BitsPerSample == 16 || track.BitsPerSample == 24 || track.BitsPerSample == 32;
        if (!supportedBits || track.Channels < 1 || track.Channels > 8 || track.SampleRate <= 0) return null;
        return track;
    }

    private static void FindAudioDetails(byte[] data, int start, int end,
        ref int pcmFlags, ref int pcmSize, ref int endian, ref int isoRate)
    {
        foreach (Box box in Children(data, start, end))
        {
            switch (box.Type)
            {
                case "pcmC":
                    if (box.Body + 6 <= box.End) { pcmFlags = data[box.Body + 4]; pcmSize = data[box.Body + 5]; }
                    break;
                case "enda":
                    if (box.Body + 2 <= box.End) endian = U16(data, box.Body);
                    break;
                case "srat":
                    if (box.Body + 8 <= box.End) isoRate = (int)U32(data, box.Body + 4);
                    break;
                case "wave":
                    FindAudioDetails(data, box.Body, box.End, ref pcmFlags, ref pcmSize, ref endian, ref isoRate);
                    break;
            }
        }
    }

    private static bool ReadChunks(byte[] data, Box stbl, PcmTrack track)
    {
        Box stsc = FindChild(data, stbl, "stsc");
        Box stsz = FindChild(data, stbl, "stsz");
        Box stco = FindChild(data, stbl, "stco");
        Box co64 = FindChild(data, stbl, "co64");
        if (stsc.Type == null || stsz.Type == null || (stco.Type == null && co64.Type == null)) return false;

        bool wide = co64.Type != null;
        Box offsets = wide ? co64 : stco;
        int chunkCount = (int)U32(data, offsets.Body + 4);
        int entrySize = wide ? 8 : 4;
        if (offsets.Body + 8 + (long)chunkCount * entrySize > offsets.End) return false;

        int stscCount = (int)U32(data, stsc.Body + 4);
        if (stsc.Body + 8 + (long)stscCount * 12 > stsc.End || stscCount == 0) return false;

        uint sampleSize = U32(data, stsz.Body + 4);
        int sampleCount = (int)U32(data, stsz.Body + 8);
        if (sampleSize == 0 && stsz.Body + 12 + (long)sampleCount * 4 > stsz.End) return false;

        int stscIndex = 0;
        int sample = 0;
        for (int chunk = 1; chunk <= chunkCount; chunk++)
        {
            while (stscIndex + 1 < stscCount && U32(data, stsc.Body + 8 + (stscIndex + 1) * 12) <= chunk)
                stscIndex++;
            long samplesInChunk = U32(data, stsc.Body + 8 + stscIndex * 12 + 4);

            long bytes;
            if (sampleSize == 1)
                bytes = samplesInChunk * track.BytesPerFrame;   // QuickTime counts frames here
            else if (sampleSize > 1)
                bytes = samplesInChunk * sampleSize;
            else
            {
                bytes = 0;
                for (long s = 0; s < samplesInChunk && sample + s < sampleCount; s++)
                    bytes += U32(data, stsz.Body + 12 + (int)(sample + s) * 4);
            }
            sample += (int)samplesInChunk;

            long offset = wide
                ? (long)U64(data, offsets.Body + 8 + (chunk - 1) * 8)
                : U32(data, offsets.Body + 8 + (chunk - 1) * 4);
            track.ChunkOffsets.Add(offset);
            track.ChunkSizes.Add(bytes);
        }
        return track.ChunkOffsets.Count > 0;
    }

    /// <summary>
    /// Honours the usual edit lists: empty edits before the sound start it later, and the
    /// first real edit's media time skips into the sound.
    /// </summary>
    private static void ApplyEditList(byte[] data, Box trak, PcmTrack track, uint movieTimescale, uint mediaTimescale)
    {
        Box elst = FindChild(data, FindChild(data, trak, "edts"), "elst");
        if (elst.Type == null) return;

        bool v1 = data[elst.Body] == 1;
        int count = (int)U32(data, elst.Body + 4);
        int size = v1 ? 20 : 12;
        for (int i = 0; i < count; i++)
        {
            int e = elst.Body + 8 + i * size;
            if (e + size > elst.End) return;
            long duration = v1 ? (long)U64(data, e) : U32(data, e);
            long mediaTime = v1 ? (long)U64(data, e + 8) : (int)U32(data, e + 4);

            if (mediaTime < 0)
            {
                if (movieTimescale > 0)
                    track.LeadingSilenceFrames += duration * track.SampleRate / movieTimescale;
                continue;
            }

            if (mediaTimescale > 0)
                track.SkipFrames = mediaTime * track.SampleRate / mediaTimescale;
            return;
        }
    }

    private static short ToInt16(byte[] buffer, int offset, PcmTrack track)
    {
        int size = track.BytesPerSample;
        if (track.IsFloat)
        {
            double value;
            if (size == 4)
            {
                uint bits = track.LittleEndian
                    ? (uint)(buffer[offset] | buffer[offset + 1] << 8 | buffer[offset + 2] << 16 | buffer[offset + 3] << 24)
                    : (uint)(buffer[offset + 3] | buffer[offset + 2] << 8 | buffer[offset + 1] << 16 | buffer[offset] << 24);
                value = BitConverter.ToSingle(BitConverter.GetBytes(bits), 0);
            }
            else
            {
                ulong bits = 0;
                for (int i = 0; i < 8; i++)
                    bits |= (ulong)buffer[offset + (track.LittleEndian ? i : 7 - i)] << (8 * i);
                value = BitConverter.Int64BitsToDouble((long)bits);
            }
            if (double.IsNaN(value)) return 0;
            return (short)Math.Round(Math.Max(-1.0, Math.Min(1.0, value)) * 32767.0);
        }

        if (size == 1)
            return track.IsUnsigned ? (short)((buffer[offset] - 128) << 8) : (short)((sbyte)buffer[offset] << 8);

        // Keep the two most significant bytes
        byte high = track.LittleEndian ? buffer[offset + size - 1] : buffer[offset];
        byte low = track.LittleEndian ? buffer[offset + size - 2] : buffer[offset + 1];
        return (short)(high << 8 | low);
    }

    private static void WriteWavHeader(Stream output, int channels, int sampleRate, long dataBytes)
    {
        var header = new byte[44];
        void Put(int at, string text) { Encoding.ASCII.GetBytes(text, 0, 4, header, at); }
        void Put32(int at, uint value) { header[at] = (byte)value; header[at + 1] = (byte)(value >> 8); header[at + 2] = (byte)(value >> 16); header[at + 3] = (byte)(value >> 24); }
        void Put16(int at, int value) { header[at] = (byte)value; header[at + 1] = (byte)(value >> 8); }

        Put(0, "RIFF");
        Put32(4, (uint)(36 + dataBytes));
        Put(8, "WAVE");
        Put(12, "fmt ");
        Put32(16, 16);
        Put16(20, 1);
        Put16(22, channels);
        Put32(24, (uint)sampleRate);
        Put32(28, (uint)(sampleRate * channels * 2));
        Put16(32, channels * 2);
        Put16(34, 16);
        Put(36, "data");
        Put32(40, (uint)dataBytes);
        output.Write(header, 0, header.Length);
    }

    private static void DeleteOldCopies(string folder, string videoName, string keepPath)
    {
        foreach (string file in Directory.GetFiles(folder, videoName + ".*"))
        {
            if (string.Equals(file, keepPath, StringComparison.OrdinalIgnoreCase)) continue;
            try { File.Delete(file); }
            catch (IOException) { }
        }
    }

    private static byte[] ReadTopLevelBox(FileStream stream, string type)
    {
        var header = new byte[16];
        long position = 0;
        while (position + 8 <= stream.Length)
        {
            stream.Position = position;
            if (ReadFully(stream, header, 8) < 8) return null;
            long size = U32(header, 0);
            string boxType = Ascii(header, 4);
            int headerSize = 8;
            if (size == 1)
            {
                if (ReadFully(stream, header, 8) < 8) return null;
                size = (long)U64(header, 0);
                headerSize = 16;
            }
            else if (size == 0)
                size = stream.Length - position;
            if (size < headerSize) return null;

            if (boxType == type)
            {
                long bodySize = size - headerSize;
                if (bodySize > 64L * 1024 * 1024) return null;
                var body = new byte[bodySize];
                return ReadFully(stream, body, (int)bodySize) == bodySize ? body : null;
            }
            position += size;
        }
        return null;
    }

    private static Box FindChild(byte[] data, Box parent, string type)
    {
        if (parent.Type == null) return default;
        foreach (Box box in Children(data, parent.Body, parent.End))
            if (box.Type == type) return box;
        return default;
    }

    private static IEnumerable<Box> Children(byte[] data, int start, int end)
    {
        int position = start;
        while (position + 8 <= end)
        {
            long size = U32(data, position);
            int headerSize = 8;
            if (size == 1)
            {
                if (position + 16 > end) yield break;
                size = (long)U64(data, position + 8);
                headerSize = 16;
            }
            else if (size == 0)
                size = end - position;
            if (size < headerSize || position + size > end) yield break;

            yield return new Box { Type = Ascii(data, position + 4), Body = position + headerSize, End = (int)(position + size) };
            position += (int)size;
        }
    }

    private static int ReadFully(Stream stream, byte[] buffer, int count)
    {
        int total = 0;
        while (total < count)
        {
            int read = stream.Read(buffer, total, count - total);
            if (read <= 0) break;
            total += read;
        }
        return total;
    }

    private static string Ascii(byte[] data, int at) => Encoding.ASCII.GetString(data, at, 4);
    private static ushort U16(byte[] data, int at) => (ushort)(data[at] << 8 | data[at + 1]);
    private static uint U32(byte[] data, int at) => (uint)(data[at] << 24 | data[at + 1] << 16 | data[at + 2] << 8 | data[at + 3]);
    private static ulong U64(byte[] data, int at) => (ulong)U32(data, at) << 32 | U32(data, at + 4);
}
