using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

internal static class IconBuilder
{
    private static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine("Usage: IconBuilder input.png output.ico");
            return 2;
        }

        int[] sizes = { 16, 24, 32, 48, 64, 128, 256 };
        List<byte[]> images = new List<byte[]>();
        using (Image source = Image.FromFile(args[0]))
        {
            foreach (int size in sizes)
            {
                using (Bitmap bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb))
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    graphics.CompositingMode = CompositingMode.SourceCopy;
                    graphics.CompositingQuality = CompositingQuality.HighQuality;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.SmoothingMode = SmoothingMode.HighQuality;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    graphics.Clear(Color.Transparent);
                    graphics.DrawImage(source, new Rectangle(0, 0, size, size));
                    using (MemoryStream memory = new MemoryStream())
                    {
                        bitmap.Save(memory, ImageFormat.Png);
                        images.Add(memory.ToArray());
                    }
                }
            }
        }

        using (FileStream stream = new FileStream(args[1], FileMode.Create, FileAccess.Write))
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            writer.Write((ushort)0);
            writer.Write((ushort)1);
            writer.Write((ushort)sizes.Length);

            int offset = 6 + (16 * sizes.Length);
            for (int i = 0; i < sizes.Length; i++)
            {
                writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
                writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((ushort)1);
                writer.Write((ushort)32);
                writer.Write((uint)images[i].Length);
                writer.Write((uint)offset);
                offset += images[i].Length;
            }

            foreach (byte[] image in images)
            {
                writer.Write(image);
            }
        }
        return 0;
    }
}
