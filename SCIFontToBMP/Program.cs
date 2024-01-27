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
    public Bitmap Bitmap { get; set; }
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
        }

        return bmp;
    }

    static void ReadLetter(FontAtlas fontAtlas, int letterNumber, BinaryReader byteStream)
    {
        int width = byteStream.ReadByte();
        int height = byteStream.ReadByte();

        Bitmap bitmap = new Bitmap(width, height);
        Graphics raster = Graphics.FromImage(bitmap);
        
        //raster.Clear(Color.White);

        for (int y = 0; y < height; y++)
        {
            byte bits = byteStream.ReadByte();

            for (int x = 0; x < width; x++)
            {
                int bit = bits & (1 << (7 - x));
                bitmap.SetPixel(x, y, bit == 0 ? Color.White : Color.Black);
            }
        }


        FontCharacter fontChar = new FontCharacter();
        fontChar.Height = height;
        fontChar.Width = width;
        fontChar.Bitmap = bitmap;


        fontAtlas.Characters.Add((char)letterNumber, fontChar);


    }

    static FontAtlas ReadFontFile(string filePath)
    {
        FontAtlas fontAtlas = new FontAtlas();

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

            // Convert this code to C#
            bool first = true;
            for (int i = 0; i < numChar; i++)
            {
                int wOffset = reader.ReadUInt16();

                BinaryReader byteStreamLetter = new BinaryReader(File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read));
                byteStreamLetter.BaseStream.Seek(wOffset, SeekOrigin.Begin);

                ReadLetter(fontAtlas, i, byteStreamLetter);
            }
        }

        return fontAtlas;
    }

    static void Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: SCIFontToBmp <input.font> <output.bmp>");
            return;
        }

        string inputFilePath = args[0];
        string outputBmpPath = args[1];
        string outputJsonPath = Path.ChangeExtension(outputBmpPath, ".json");

        FontAtlas fontAtlas = ReadFontFile(inputFilePath);
        Bitmap bmp = CreateBitmapAtlas(fontAtlas);
        bmp.Save(outputBmpPath, ImageFormat.Bmp);

        string json = JsonConvert.SerializeObject(fontAtlas);
        File.WriteAllText(outputJsonPath, json);
    }
 }

