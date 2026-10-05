// Reproduces the geometry of ThirdPerson.svg in independent PNG icon frames.
// No external drawing library is required. Source and resulting icon: MIT.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Collections.Generic;

internal static class BuildIcon
{
    private static Bitmap Draw(int size)
    {
        Bitmap image = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(image)) {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);
            g.ScaleTransform(size / 64f, size / 64f);
            using (GraphicsPath bg = new GraphicsPath()) {
                bg.AddArc(2, 2, 26, 26, 180, 90);
                bg.AddArc(36, 2, 26, 26, 270, 90);
                bg.AddArc(36, 36, 26, 26, 0, 90);
                bg.AddArc(2, 36, 26, 26, 90, 90); bg.CloseFigure();
                using (Brush brush = new SolidBrush(Color.FromArgb(31, 86, 148))) g.FillPath(brush, bg);
            }
            using (Pen quote = new Pen(Color.White, 5)) {
                quote.StartCap = quote.EndCap = LineCap.Round; quote.LineJoin = LineJoin.Round;
                g.DrawLines(quote, new PointF[] { new PointF(23,15),new PointF(14,24),new PointF(23,33) });
                g.DrawLines(quote, new PointF[] { new PointF(35,15),new PointF(26,24),new PointF(35,33) });
            }
            using (Pen arrow = new Pen(Color.FromArgb(114, 227, 237), 5)) {
                arrow.StartCap = arrow.EndCap = LineCap.Round; arrow.LineJoin = LineJoin.Round;
                g.DrawLine(arrow,21,46,49,46);
                g.DrawLines(arrow, new PointF[] { new PointF(41,38),new PointF(49,46),new PointF(41,54) });
            }
        }
        return image;
    }
    private static int Main(string[] args)
    {
        int[] sizes = { 16,20,24,32,40,48,64,128,256 };
        List<byte[]> frames = new List<byte[]>();
        foreach (int size in sizes) using (Bitmap bitmap = Draw(size)) using (MemoryStream png = new MemoryStream()) {
            bitmap.Save(png, ImageFormat.Png); frames.Add(png.ToArray());
            if (size == 256 && args.Length > 1) bitmap.Save(args[1], ImageFormat.Png);
        }
        using (BinaryWriter file = new BinaryWriter(File.Create(args[0]))) {
            file.Write((ushort)0); file.Write((ushort)1); file.Write((ushort)sizes.Length);
            int offset = 6 + 16 * sizes.Length;
            for (int n=0;n<sizes.Length;n++) {
                file.Write((byte)(sizes[n] == 256 ? 0 : sizes[n]));
                file.Write((byte)(sizes[n] == 256 ? 0 : sizes[n]));
                file.Write((byte)0); file.Write((byte)0); file.Write((ushort)1); file.Write((ushort)32);
                file.Write(frames[n].Length); file.Write(offset); offset += frames[n].Length;
            }
            foreach (byte[] frame in frames) file.Write(frame);
        }
        return 0;
    }
}
