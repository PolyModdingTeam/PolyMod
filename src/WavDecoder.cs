using System.Buffers.Binary;

namespace PolyMod;

/// <summary>
/// Decodes RIFF WAVE files into the interleaved float samples Unity wants.
/// </summary>
public static class WavDecoder
{
	/// <summary>
	/// The decoded contents of a WAVE file.
	/// </summary>
	/// <param name="samples">Interleaved samples in the -1 to 1 range, channel by channel.</param>
	/// <param name="channels">Number of channels the samples are interleaved across.</param>
	/// <param name="frequency">Sample rate in hertz.</param>
	public record WavData(float[] samples, int channels, int frequency)
	{
		/// <summary>Number of sample frames, which is the length Unity asks for.</summary>
		public int lengthSamples => samples.Length / channels;
	}

	private const ushort FORMAT_PCM = 1;
	private const ushort FORMAT_FLOAT = 3;
	private const ushort FORMAT_EXTENSIBLE = 0xFFFE;

	private const int HEADER_LENGTH = 12;
	private const int CHUNK_HEADER_LENGTH = 8;
	private const int FORMAT_CHUNK_LENGTH = 16;
	private const int EXTENSIBLE_CHUNK_LENGTH = 40;

	/// <summary>
	/// Decodes a WAVE file held in memory.
	/// </summary>
	/// <param name="data">The whole file, starting at its RIFF header.</param>
	/// <returns>The samples plus the channel count and sample rate needed to play them.</returns>
	/// <exception cref="InvalidDataException">The file is not a WAVE file this can decode.</exception>
	public static WavData Decode(byte[] data)
	{
		ReadOnlySpan<byte> file = data;
		if (file.Length < HEADER_LENGTH || ReadId(file, 0) != "RIFF" || ReadId(file, 8) != "WAVE")
			throw new InvalidDataException("Not a RIFF WAVE file.");

		ushort format = 0;
		ushort channels = 0;
		ushort bitsPerSample = 0;
		int frequency = 0;
		int dataOffset = -1;
		int dataLength = 0;

		int offset = HEADER_LENGTH;
		while (offset + CHUNK_HEADER_LENGTH <= file.Length)
		{
			string id = ReadId(file, offset);
			uint declared = BinaryPrimitives.ReadUInt32LittleEndian(file.Slice(offset + 4, 4));
			int payload = offset + CHUNK_HEADER_LENGTH;
			int size = (int)Math.Min(declared, (uint)(file.Length - payload));

			if (id == "fmt " && size >= FORMAT_CHUNK_LENGTH)
			{
				format = BinaryPrimitives.ReadUInt16LittleEndian(file.Slice(payload, 2));
				channels = BinaryPrimitives.ReadUInt16LittleEndian(file.Slice(payload + 2, 2));
				frequency = BinaryPrimitives.ReadInt32LittleEndian(file.Slice(payload + 4, 4));
				bitsPerSample = BinaryPrimitives.ReadUInt16LittleEndian(file.Slice(payload + 14, 2));
				if (format == FORMAT_EXTENSIBLE && size >= EXTENSIBLE_CHUNK_LENGTH)
					format = BinaryPrimitives.ReadUInt16LittleEndian(file.Slice(payload + 24, 2));
			}
			else if (id == "data")
			{
				dataOffset = payload;
				dataLength = size;
			}

			offset = payload + size + (size & 1);
		}

		if (channels < 1) throw new InvalidDataException("WAVE file declares no channels.");
		if (frequency < 1) throw new InvalidDataException("WAVE file declares no sample rate.");
		if (bitsPerSample < 8 || bitsPerSample % 8 != 0)
			throw new InvalidDataException($"Unsupported WAVE bit depth {bitsPerSample}.");
		if (dataOffset < 0) throw new InvalidDataException("WAVE file has no data chunk.");

		int bytesPerSample = bitsPerSample / 8;
		int count = dataLength / bytesPerSample;
		count -= count % channels;
		if (count < 1) throw new InvalidDataException("WAVE file holds no samples.");

		float[] samples = ReadSamples(file.Slice(dataOffset), format, bitsPerSample, count);
		return new WavData(samples, channels, frequency);
	}

	private static float[] ReadSamples(ReadOnlySpan<byte> payload, ushort format, ushort bitsPerSample, int count)
	{
		float[] samples = new float[count];
		int step = bitsPerSample / 8;

		if (format == FORMAT_PCM)
		{
			switch (bitsPerSample)
			{
				case 8:
					for (int i = 0; i < count; i++)
						samples[i] = (payload[i] - 128) / 128f;
					return samples;
				case 16:
					for (int i = 0; i < count; i++)
						samples[i] = BinaryPrimitives.ReadInt16LittleEndian(payload.Slice(i * step, 2)) / 32768f;
					return samples;
				case 24:
					for (int i = 0; i < count; i++)
					{
						int at = i * step;
						int value = payload[at] | (payload[at + 1] << 8) | ((sbyte)payload[at + 2] << 16);
						samples[i] = value / 8388608f;
					}
					return samples;
				case 32:
					for (int i = 0; i < count; i++)
						samples[i] = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(i * step, 4)) / 2147483648f;
					return samples;
			}
		}
		else if (format == FORMAT_FLOAT)
		{
			switch (bitsPerSample)
			{
				case 32:
					for (int i = 0; i < count; i++)
						samples[i] = BitConverter.Int32BitsToSingle(
							BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(i * step, 4))
						);
					return samples;
				case 64:
					for (int i = 0; i < count; i++)
						samples[i] = (float)BitConverter.Int64BitsToDouble(
							BinaryPrimitives.ReadInt64LittleEndian(payload.Slice(i * step, 8))
						);
					return samples;
			}
		}

		throw new InvalidDataException(
			$"Unsupported WAVE format {format} at {bitsPerSample} bits per sample. "
				+ "Save the file as PCM or float WAVE."
		);
	}

	private static string ReadId(ReadOnlySpan<byte> file, int offset)
	{
		return new string(new char[]
		{
			(char)file[offset],
			(char)file[offset + 1],
			(char)file[offset + 2],
			(char)file[offset + 3],
		});
	}
}
