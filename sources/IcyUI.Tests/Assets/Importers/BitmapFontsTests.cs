// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using CommunityToolkit.HighPerformance;
using Icy.Assets.Importers.BitmapFonts;
using Xunit;

namespace Icy.Tests.Assets.Importers
{
    public class BitmapFontsTests
    {
        [Fact]
        public void TestParsingFontInfo()
        {
            using var reader = new StreamReader("Resources/TestFont.fnt");
            string line = reader.ReadLine()!;
            BitmapFontInfo info = BitmapFontInfo.Parse(line);
        }

        [Fact]
        public void TestBinaryLoadingFontInfo()
        {
            using var stream = File.OpenRead("Resources/comic10.fnt");
            stream.Read<uint>(); // Skip definition
            stream.ReadByte(); // Skip block type
            int size = stream.Read<int>();
            BitmapFontInfo info = BitmapFontInfo.Read(stream, size);
        }

        [Fact]
        public void TestParsingFontCommon()
        {
            using var reader = new StreamReader("Resources/TestFont.fnt");
            reader.ReadLine(); // Skip info
            string line = reader.ReadLine()!;
            BitmapFontCommon common = BitmapFontCommon.Parse(line);
        }

        [Fact]
        public void TestBinaryLoadingFontCommon()
        {
            using var stream = File.OpenRead("Resources/comic10.fnt");
            stream.Read<uint>(); // Skip definition
            stream.ReadByte(); // Skip block type
            int size = stream.Read<int>(); // Get info section size
            stream.Seek(size, SeekOrigin.Current); // Skip info section
            stream.ReadByte(); // Skip block type
            size = stream.Read<int>(); // Get common section size
            BitmapFontCommon common = BitmapFontCommon.Read(stream);
        }

        [Fact]
        public void TestBinaryLoadingFont()
        {
            using var stream = File.OpenRead("Resources/comic10.fnt");
            var font = BitmapFont.Load(stream);
        }

        [Fact]
        public void TestParsingFont()
        {
            using var stream = File.OpenRead("Resources/TestFont.fnt");
            var font = BitmapFont.Load(stream);
        }

        [Fact]
        public void TestLoadingXml()
        {
            using var stream = File.OpenRead("Resources/TestFont.xml");
            var font = BitmapFont.Load(stream);
        }
    }
}