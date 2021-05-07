using SkiaSharp;
using System;
using System.Buffers;
using System.IO;
using System.Text;

namespace SkiaSharp1bpp
{
	public static class Processor
	{
		private const char RepeatRowCode = ':';
		private const char RepeatZeroCode = ',';
		private const char RepeatOneCode = '!';

		private const string RepeatLowCodes = " GHIJKLMNOPQRSTUVWXY";
		private const string RepeatHiCodes = " ghijklmnopqrstuvwxyz";

		public static string GeneratePrinterCode(SKImage image, bool invert = false, bool compress = true)
		{
			using var grayBitmap = GenerateGrayBitmap(image);

			using var stream = Get1bppBytes(grayBitmap, invert);

			var bytesLength = (int)stream.Length;

			var bytes = new ReadOnlySpan<byte>(stream.GetBuffer(), 0, bytesLength);

			var bytesWidth = grayBitmap.Width / 8;

			var data = compress
				? GetCompressedHex(bytes, bytesWidth)
				: GetUncompressedHex(bytes, bytesWidth);

			var xdpmm = 0;
			var ydpmm = 0;

			return
				$"^XA \n" +
				$"^FO{xdpmm},{ydpmm} ^GFA,{bytesLength},{bytesLength},{bytesWidth}, {data} ^FS \n" +
				$"^XZ";
		}

		public static string GetUncompressedHex(ReadOnlySpan<byte> bytes, int bytesWidth)
		{
			return Convert.ToHexString(bytes);
		}

		public static string GetCompressedHex(ReadOnlySpan<byte> bytes, int widthBytes)
		{
			if (bytes.Length % widthBytes != 0)
				throw new ArgumentException("The number of bytes bust be a multiple of the width bytes.", nameof(bytes));

			var builder = new StringBuilder();

			var rowCount = bytes.Length / widthBytes;

			var previousRow = (ReadOnlySpan<byte>)null;
			for (var i = 0; i < rowCount; i++)
			{
				var row = bytes.Slice(i * widthBytes, widthBytes);

				AppendRow(builder, row, previousRow);

				previousRow = row;
			}

			return builder.ToString();
		}

		private static void AppendRow(StringBuilder builder, ReadOnlySpan<byte> row, ReadOnlySpan<byte> previousRow)
		{
			if (previousRow != null && row.SequenceEqual(previousRow))
			{
				builder.Append(RepeatRowCode);
				return;
			}

			if (All(row, 0x00))
			{
				builder.Append(RepeatZeroCode);
				return;
			}

			if (All(row, 0xFF))
			{
				builder.Append(RepeatOneCode);
				return;
			}

			// if we couldn't take a fast route, break down the bytes into nibbles
			var nibbleCount = row.Length * 2;
			var nibbles = ArrayPool<byte>.Shared.Rent(nibbleCount);
			for (int i = 0; i < row.Length; i++)
			{
				nibbles[i * 2] = (byte)(row[i] >> 4);
				nibbles[i * 2 + 1] = (byte)(row[i] & 0x0F);
			}

			for (var i = 0; i < nibbleCount; i++)
			{
				var cPixel = nibbles[i];

				// count the duplicate nibbles
				var repeatCount = 0;
				for (var j = i; j < nibbleCount && repeatCount <= 400; j++)
				{
					if (cPixel == nibbles[j])
						repeatCount++;
					else
						break;
				}

				if (repeatCount > 2)
				{
					// there was at least one duplicate

					if (repeatCount == nibbleCount - i && (cPixel == 0x0 || cPixel == 0xF))
					{
						// handle repeating until the end of the row
						if (cPixel == 0x0)
						{
							builder.Append(RepeatZeroCode);
							break;
						}
						else if (cPixel == 0xF)
						{
							builder.Append(RepeatOneCode);
							break;
						}
					}
					else
					{
						// if the nibbles do not repeat until the end, add the
						// specific repeat count followed by the hex code

						builder.Append(GetRepeatCode(repeatCount));
						builder.Append(cPixel.ToString("X"));

						i += repeatCount - 1;
					}
				}
				else
				{
					// no dupliates, so continue with just the hex code

					builder.Append(cPixel.ToString("X"));
				}
			}

			ArrayPool<byte>.Shared.Return(nibbles);

			static bool All(ReadOnlySpan<byte> row, byte val)
			{
				for (int i = 0; i < row.Length; i++)
				{
					if (row[i] != val)
						return false;
				}

				return true;
			}
		}

		public static string GetRepeatCode(int repeatCount)
		{
			if (repeatCount > 419)
				throw new ArgumentOutOfRangeException(nameof(repeatCount));

			var high = repeatCount / 20;
			var low = repeatCount % 20;

			var repeatStr = "";
			if (high > 0)
				repeatStr += RepeatHiCodes[high];
			if (low > 0)
				repeatStr += RepeatLowCodes[low];
			return repeatStr;
		}

		public static SKBitmap GenerateGrayBitmap(SKBitmap bitmap)
		{
			using var image = SKImage.FromBitmap(bitmap);
			return GenerateGrayBitmap(image);
		}

		public static SKBitmap GenerateGrayBitmap(SKImage image)
		{
			var dstInfo = new SKImageInfo(
				GetByteDimension(image.Width),
				GetByteDimension(image.Height),
				SKColorType.Gray8,
				SKAlphaType.Opaque);

			var bitmap = new SKBitmap(dstInfo);

			// draw the multi-colored image onto a 8 bit canvas (1 color)
			using (var g = new SKCanvas(bitmap))
			{
				g.DrawImage(image, 0, 0);
			}

			return bitmap;
		}

		public static MemoryStream Get1bppBytes(SKBitmap bitmap, bool invert = false)
		{
			if (bitmap.Width % 8 != 0)
				throw new ArgumentException("Width must be a multiple of 8", nameof(bitmap));
			if (bitmap.Height % 8 != 0)
				throw new ArgumentException("Height must be a multiple of 8", nameof(bitmap));
			if (bitmap.BytesPerPixel != 1)
				throw new ArgumentException("The color should be 1 byte/pixel.", nameof(bitmap));

			var totalBytes = bitmap.ByteCount;

			var stream = new MemoryStream(totalBytes);

			using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
			{
				// init counters
				byte buffer = 0b_0000_0000;
				var bitCounter = 0;

				var pixels = bitmap.GetPixelSpan();
				for (var i = 0; i < pixels.Length; i++)
				{
					// shift the buffer and add the next bit
					buffer <<= 1;
					if (pixels[i] > 127 == invert)
						buffer += 1;

					// step the counters
					bitCounter++;

					// push the byte if we have:
					//  1. reached a full byte
					//  2. reached the end of the data
					if (bitCounter == 8 || i == pixels.Length - 1)
					{
						writer.Write(buffer);

						// reset all the counters
						buffer = 0b_0000_0000;
						bitCounter = 0;
					}
				}
			}

			stream.Position = 0;

			return stream;
		}

		private static int GetByteDimension(int dimension)
		{
			var mod = dimension % 8;
			if (mod == 0)
				return dimension;

			return dimension + (8 - mod);
		}
	}
}
