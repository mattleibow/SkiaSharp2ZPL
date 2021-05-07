using SkiaSharp;
using System;
using System.Linq;
using Xunit;

namespace SkiaSharp1bpp.Tests
{
	public class UnitTests
	{
		[Fact]
		public void CompressedSkiaSharpQRIsCorrect()
		{
			using var src = SKImage.FromEncodedData("skiasharp-qr.png");
			using var grayBitmap = Processor.GenerateGrayBitmap(src);
			using var stream = Processor.Get1bppBytes(grayBitmap);
			var bytes = stream.ToArray();

			var codes = Processor.GetCompressedHex(bytes, grayBitmap.Width / 8);

			Assert.Equal(CompressedSkiaSharpQR, codes);
		}

		[Theory]
		[InlineData("skiasharp-qr.png", true, true)]
		[InlineData("skiasharp-qr.png", false, true)]
		[InlineData("skiasharp-qr.png", true, false)]
		[InlineData("skiasharp-qr.png", false, false)]
		[InlineData("skiasharp-logo.png", true, true)]
		[InlineData("skiasharp-logo.png", false, true)]
		[InlineData("skiasharp-logo.png", true, false)]
		[InlineData("skiasharp-logo.png", false, false)]
		public void GeneratePrinterCodeDoesNotThrow(string filename, bool invert, bool compress)
		{
			using var src = SKImage.FromEncodedData(filename);

			var codes = Processor.GeneratePrinterCode(src, invert, compress);
		}

		[Theory]
		[InlineData(0, "")]
		[InlineData(1, "G")]
		[InlineData(5, "K")]
		[InlineData(10, "P")]
		[InlineData(15, "U")]
		[InlineData(19, "Y")]
		[InlineData(20, "g")]
		[InlineData(21, "gG")]
		[InlineData(30, "gP")]
		[InlineData(40, "h")]
		[InlineData(180, "o")]
		[InlineData(181, "oG")]
		public void GetRepeatCode(int count, string expectedCode)
		{
			var code = Processor.GetRepeatCode(count);

			Assert.Equal(expectedCode, code);
		}

		[Theory]
		[InlineData(",", new byte[] { 0b_0000_0000 })]
		[InlineData("!", new byte[] { 0b_1111_1111 })]
		[InlineData("01", new byte[] { 0b_0000_0001 })]
		[InlineData("10", new byte[] { 0b_0001_0000 })]
		[InlineData("0F", new byte[] { 0b_0000_1111 })]
		[InlineData("F0", new byte[] { 0b_1111_0000 })]
		[InlineData(",", new byte[] { 0b_0000_0000, 0b_0000_0000 })]
		[InlineData("!", new byte[] { 0b_1111_1111, 0b_1111_1111 })]
		[InlineData("!", new byte[] { 0b_1111_1111, 0b_1111_1111, 0b_1111_1111 })]
		[InlineData("01!", new byte[] { 0b_0000_0001, 0b_1111_1111, 0b_1111_1111 })]
		[InlineData("JF01", new byte[] { 0b_1111_1111, 0b_1111_1111, 0b_0000_0001 })]
		[InlineData("LF01", new byte[] { 0b_1111_1111, 0b_1111_1111, 0b_1111_1111, 0b_0000_0001 })]
		public void GetCompressedHex(string expectedHex, byte[] pixels)
		{
			var hex = Processor.GetCompressedHex(pixels, pixels.Length);

			Assert.Equal(expectedHex, hex);
		}

		[Theory]
		[InlineData("LF01", 0b_1111_1111, 3, 0b_0000_0001)]
		[InlineData("XF01", 0b_1111_1111, 9, 0b_0000_0001)]
		[InlineData("Y01", 0b_0000_0000, 9, 0b_0000_0001)]
		[InlineData("gF01", 0b_1111_1111, 10, 0b_0000_0001)]
		[InlineData("gG01", 0b_0000_0000, 10, 0b_0000_0001)]
		public void GetCompressedHexRepeatingRowWithSuffex(string expectedHex, byte repeat, int repeatCount, byte suffix)
		{
			var pixels = Enumerable.Repeat(repeat, repeatCount)
				.Append(suffix)
				.ToArray();

			var hex = Processor.GetCompressedHex(pixels, pixels.Length);

			Assert.Equal(expectedHex, hex);
		}

		[Theory]
		[InlineData("0!", 0b_0000_1111, 0b_1111_1111, 3)]
		[InlineData("F,", 0b_1111_0000, 0b_0000_0000, 9)]
		public void GetCompressedHexRepeatingRowWithPrefix(string expectedHex, byte prefix, byte repeat, int repeatCount)
		{
			var pixels = Enumerable.Repeat(repeat, repeatCount)
				.Prepend(prefix)
				.ToArray();

			var hex = Processor.GetCompressedHex(pixels, pixels.Length);

			Assert.Equal(expectedHex, hex);
		}

		[Theory]
		[InlineData("00", new byte[] { 0b_0000_0000 })]
		[InlineData("FF", new byte[] { 0b_1111_1111 })]
		[InlineData("01", new byte[] { 0b_0000_0001 })]
		[InlineData("10", new byte[] { 0b_0001_0000 })]
		[InlineData("0F", new byte[] { 0b_0000_1111 })]
		[InlineData("F0", new byte[] { 0b_1111_0000 })]
		[InlineData("FFFF01", new byte[] { 0b_1111_1111, 0b_1111_1111, 0b_0000_0001 })]
		public void GetUncompressedHex(string expectedHex, byte[] pixels)
		{
			var hex = Processor.GetUncompressedHex(pixels, pixels.Length);

			Assert.Equal(expectedHex, hex);
		}

		[Theory]
		[InlineData(0xFFFFFFFF)]
		[InlineData(0xFF000000)]
		[InlineData(0xFF0000FF)]
		[InlineData(0xFF00FF00)]
		[InlineData(0xFFFF0000)]
		public void GenerateGrayBitmapConvertsImageToGray(uint color)
		{
			var info = new SKImageInfo(8, 8);
			using var bmp = new SKBitmap(info);
			bmp.Erase(color);

			var gray = Processor.GenerateGrayBitmap(bmp);
			var grayColor = gray.GetPixel(0, 0);

			Assert.Equal(255, grayColor.Alpha);
			Assert.Equal(grayColor.Red, grayColor.Green);
			Assert.Equal(grayColor.Red, grayColor.Blue);
		}

		[Theory]
		[InlineData(1, 8, 8, 8)]
		[InlineData(8, 1, 8, 8)]
		[InlineData(5, 8, 8, 8)]
		[InlineData(8, 5, 8, 8)]
		[InlineData(8, 9, 8, 16)]
		[InlineData(9, 8, 16, 8)]
		public void GenerateGrayBitmapGeneratesCorrectImage(int width, int height, int correctWidth, int correctHeight)
		{
			var info = new SKImageInfo(width, height);
			using var bmp = new SKBitmap(info);
			bmp.Erase(SKColors.Blue);

			var gray = Processor.GenerateGrayBitmap(bmp);

			Assert.Equal(correctWidth, gray.Width);
			Assert.Equal(correctHeight, gray.Height);
			Assert.Equal(SKColorType.Gray8, gray.ColorType);
		}

		[Theory]
		[InlineData(SKColorType.Gray8)]
		[InlineData(SKColorType.Alpha8)]
		public void Get1bppBytesSupport8BitColorTypes(SKColorType colorType)
		{
			var info = new SKImageInfo(8, 8, colorType);

			using var bmp = new SKBitmap(info);

			using var stream = Processor.Get1bppBytes(bmp);
		}

		[Theory]
		[InlineData(SKColorType.Bgra8888)]
		[InlineData(SKColorType.Rgba8888)]
		[InlineData(SKColorType.Rgba16161616)]
		public void Get1bppBytesThrowsOnInvalidColorType(SKColorType colorType)
		{
			var info = new SKImageInfo(8, 8, colorType);

			using var bmp = new SKBitmap(info);

			Assert.Throws<ArgumentException>(() => Processor.Get1bppBytes(bmp));
		}

		[Theory]
		[InlineData(1, 8)]
		[InlineData(8, 1)]
		[InlineData(5, 8)]
		[InlineData(8, 5)]
		[InlineData(8, 9)]
		[InlineData(9, 8)]
		public void Get1bppBytesThrowsOnInvalidBounds(int width, int height)
		{
			var info = new SKImageInfo(width, height, SKColorType.Gray8);

			using var bmp = new SKBitmap(info);

			Assert.Throws<ArgumentException>(() => Processor.Get1bppBytes(bmp));
		}

		private const string CompressedSkiaSharpQR =
			"," +
			"::::::::::::::::::::::::::::::::::::::O01gQF8N07IFEgH07gLF8I01gQF8," +
			":::::::::::::::::O01JF8gG01JF8I01NFEJ07NF8W07IFEW01JF8gG01JF8," +
			":::::::::::::::::O01JF8I01SF8I01JF8N07RFEN01JF8I01JF8N07NF8I01JF8I01SF8I01JF8," +
			":::::::::::::::::O01JF8I01SF8I01JF8I01NFEJ07IFEW01WFEN01JF8I01SF8I01JF8," +
			":::::::::::::::::O01JF8I01SF8I01JF8R01JF8I01WFEJ07WF8I01JF8I01SF8I01JF8," +
			":::::::::::::::::O01JF8gG01JF8I01JF8I01JF8N07gGFEN01JF8I01JF8gG01JF8," +
			":::::::::::::::::O01gQF8I01JF8I01JF8I01JF8I01JF8I01JF8I01JF8I01JF8I01gQF8," +
			":::::::::::::::::iI01NFEJ07IFES07NF8," +
			":::::::::::::::::O01gHF8I01gHF8I01JF8I01NFEJ07IFEJ07IFEJ07IFEJ07IFEJ07IFEJ07IFE," +
			":::::::::::::::::O01JF8I01NFEgH07IFEgH07NF8N07NF8I01SF8R01JF8," +
			":::::::::::::::::O01JF8W07RFEJ07IFEJ07NF8W07NF8I01JF8R01JF8," +
			":::::::::::::::::gI07NF8N07gGFEN01JF8I01JF8R01JF8W07IFEJ07IFE," +
			":::::::::::::::::O01NFEJ07IFEN01JF8N07IFEJ07IFEgH07IFEJ07IFEgH07NF8," +
			":::::::::::::::::T07WF8N07NF8I01JF8I01WFEJ07gUF8R01JF8," +
			":::::::::::::::::O01SF8I01SF8I01JF8I01JF8N07RFEJ07NF8I01NFEN01SF8," +
			":::::::::::::::::O01JF8I01JF8N07IFEgL01NFEJ07IFES07gLF8N07IFE," +
			":::::::::::::::::O01NFEJ07IFEN01JF8I01JF8R01JF8I01WFEJ07IFEJ07IFEJ07IFEJ07NF8," +
			":::::::::::::::::O01JF8N07RFEhJ01WFEJ07WF8I01JF8I01JF8," +
			":::::::::::::::::O01JF8I01NFEN01NFEJ07WF8N07IFEN01JF8I01JF8I01SF8I01JF8," +
			":::::::::::::::::O01JF8I01JF8I01JF8N07RFEJ07IFEN01JF8gG01SF8W07IFE," +
			":::::::::::::::::O01JF8I01gHF8I01JF8N07IFES07RFEJ07gLF8I01SF8," +
			":::::::::::::::::hK01SF8I01SF8I01JF8I01JF8I01JF8R01gHF8," +
			":::::::::::::::::O01gQF8I01JF8W07WF8I01SF8I01JF8I01SF8," +
			":::::::::::::::::O01JF8gG01JF8N07NF8I01NFEJ07IFES07NF8R01JF8," +
			":::::::::::::::::O01JF8I01SF8I01JF8I01NFEN01JF8I01NFEJ07IFEN01gHF8I01NFE," +
			":::::::::::::::::O01JF8I01SF8I01JF8I01NFEgL01JF8N07NF8W07WF8," +
			":::::::::::::::::O01JF8I01SF8I01JF8I01JF8N07NF8N07IFEJ07hNFE," +
			":::::::::::::::::O01JF8gG01JF8I01NFEJ07IFEN01NFEW01NFEJ07IFEJ07IFEJ07IFE," +
			":::::::::::::::::O01gQF8I01WFEgH07IFEJ07IFEN01SF8I01JF8," +
			":::::::::::::::::," +
			"::::::::::::::::::::::::::::::::::::::";
	}
}
