using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Runtime.ConstrainedExecution;

public class FontCharacter
{
    public int Height { get; set; }
    public int Width { get; set; }
    public Rectangle AtlasLocation { get; set; }
    public Bitmap Bitmap { get; set; }
    public int BufferOffset { get; set; }
    public int Index { get; set; }
}

public class FontAtlas
{
    public Dictionary<char, FontCharacter> Characters { get; set; } = new Dictionary<char, FontCharacter>();
    public int LineHeight { get; set; }
}

class Program
{
    static Bitmap CreateBitmapAtlas(FontAtlas fontAtlas)
    {
        int width = 0;
        int height = 0;

        foreach (var fontChar in fontAtlas.Characters.Values)
        {
            width += fontChar.Width;
            height = Math.Max(height, fontChar.Height);
        }

        Bitmap bmp = new Bitmap(width, height);
        Graphics raster = Graphics.FromImage(bmp);
        raster.Clear(Color.White);

        int x = 0;
        foreach (var fontChar in fontAtlas.Characters.Values)
        {
            raster.DrawImage(fontChar.Bitmap, x, 0);
            x += fontChar.Width;
            fontAtlas.Characters[(char)fontChar.Index].AtlasLocation = new Rectangle(x, 0, fontChar.Width, fontChar.Height);
        }

        return bmp;
    }

    static void ReadLetter(FontAtlas fontAtlas, FontCharacter fontChar, int letterNumber, BinaryReader byteStream)
    {
        int width = byteStream.ReadByte();
        int height = byteStream.ReadByte();

        Bitmap bitmap = new Bitmap(width, height);
        Graphics raster = Graphics.FromImage(bitmap);

        raster.Clear(Color.White);

        // Calculate the number of bytes to read per row
        int bytesPerRow = (width + 7) / 8;

        for (int y = 0; y < height; y++)
        {
            // Read the necessary bytes for the current row
            byte[] rowBits = byteStream.ReadBytes(bytesPerRow);

            for (int x = 0; x < width; x++)
            {
                // Calculate the byte and bit index for the current pixel
                int byteIndex = x / 8;
                int bitIndex = 7 - (x % 8);

                // Get the byte that contains the current pixel
                byte bits = rowBits[byteIndex];

                // Check if the current bit is set
                int bit = bits & (1 << bitIndex);

                // Set the pixel color based on the bit value
                bitmap.SetPixel(x, y, bit == 0 ? Color.White : Color.Black);
            }
        }

        fontChar.Height = height;
        fontChar.Width = width;
        fontChar.Bitmap = bitmap;

        fontAtlas.Characters.Add((char)letterNumber, fontChar);
    }

    static FontAtlas ReadFontFile(string filePath, int startChar, int endChar)
    {
        FontAtlas fontAtlas = new FontAtlas();
        List<FontCharacter> fontCharsToProcess = new List<FontCharacter>();

        using (BinaryReader reader = new BinaryReader(File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read)))
        {
            // Read the header
            int dummy = reader.ReadUInt16(); // Always zero (?)
            int numChar = reader.ReadUInt16();
            int height = reader.ReadUInt16();
            fontAtlas.LineHeight = height;

            numChar = Math.Min(256, numChar); // Limit the number of characters to 256
            fontAtlas.LineHeight = Math.Max(1, fontAtlas.LineHeight); // Ensure the line height is at least 1
            fontAtlas.LineHeight = Math.Min(128, fontAtlas.LineHeight); // Limit the line height to 128 (to fit in a byte

            for (int i = 0; i < numChar; i++)
            {
                FontCharacter fontChar = new FontCharacter();
                fontChar.BufferOffset = reader.ReadUInt16();
                fontChar.Index = i;
                fontCharsToProcess.Add(fontChar);
            }
        }

        using (BinaryReader reader = new BinaryReader(File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read)))
        {
            foreach (var fontChar in fontCharsToProcess)
            {
                reader.BaseStream.Seek(fontChar.BufferOffset, SeekOrigin.Begin);

                if (fontChar.Index >= startChar && fontChar.Index <= endChar)
                {
                    ReadLetter(fontAtlas, fontChar, fontChar.Index, reader);
                }
            }
        }

        return fontAtlas;
    }

    static void Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: SCIFontToBmp <input.font> <output.bmp> [startChar] [endChar]");
            return;
        }

        string inputFilePath = args[0];
        string outputBmpPath = args[1];
        string outputJsonPath = Path.ChangeExtension(outputBmpPath, ".json");

        int startChar = args.Length > 2 ? int.Parse(args[2]) : 0;
        int endChar = args.Length > 3 ? int.Parse(args[3]) : 255; // Assuming 256 characters by default

        FontAtlas fontAtlas = ReadFontFile(inputFilePath, startChar, endChar);
        Bitmap bmp = CreateBitmapAtlas(fontAtlas);
        bmp.Save(outputBmpPath, ImageFormat.Bmp);

        string json = JsonConvert.SerializeObject(fontAtlas);
        File.WriteAllText(outputJsonPath, json);
    }
 }

