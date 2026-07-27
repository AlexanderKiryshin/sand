using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

internal static class StripBoosterBg
{
    private static int Main(string[] args)
    {
        string dir = args[0];
        string[] names = { "Booster_Magnet.png", "Booster_Transparent.png", "Booster_Shuffle.png" };
        for (int i = 0; i < names.Length; i++)
        {
            string file = Path.Combine(dir, names[i]);
            if (!File.Exists(file)) { Console.WriteLine("Missing " + file); continue; }
            ProcessFile(file);
        }
        return 0;
    }

    private static void ProcessFile(string path)
    {
        byte[] pngBytes;
        using (Bitmap src = new Bitmap(path))
        {
            int w = src.Width;
            int h = src.Height;
            using (Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.DrawImage(src, 0, 0, w, h);
                }

                BitmapData data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
                int stride = data.Stride;
                byte[] bytes = new byte[Math.Abs(stride) * h];
                Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
                bool[] visited = new bool[w * h];
                Queue<int> queue = new Queue<int>();
                for (int x = 0; x < w; x++)
                {
                    Enq(bytes, visited, queue, stride, w, h, x, 0);
                    Enq(bytes, visited, queue, stride, w, h, x, h - 1);
                }
                for (int y = 0; y < h; y++)
                {
                    Enq(bytes, visited, queue, stride, w, h, 0, y);
                    Enq(bytes, visited, queue, stride, w, h, w - 1, y);
                }
                while (queue.Count > 0)
                {
                    int index = queue.Dequeue();
                    int x = index % w;
                    int y = index / w;
                    bytes[y * stride + x * 4 + 3] = 0;
                    Enq(bytes, visited, queue, stride, w, h, x - 1, y);
                    Enq(bytes, visited, queue, stride, w, h, x + 1, y);
                    Enq(bytes, visited, queue, stride, w, h, x, y - 1);
                    Enq(bytes, visited, queue, stride, w, h, x, y + 1);
                }
                Marshal.Copy(bytes, 0, data.Scan0, bytes.Length);
                bmp.UnlockBits(data);

                using (MemoryStream ms = new MemoryStream())
                {
                    bmp.Save(ms, ImageFormat.Png);
                    pngBytes = ms.ToArray();
                }
                Console.WriteLine(Path.GetFileName(path) + " cornerA=" + bmp.GetPixel(0,0).A + " bytes=" + pngBytes.Length);
            }
        }

        string outPath = path;
        for (int attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                using (FileStream fs = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    fs.Write(pngBytes, 0, pngBytes.Length);
                }
                Console.WriteLine("Wrote " + outPath);
                return;
            }
            catch (IOException ex)
            {
                Console.WriteLine("Retry " + attempt + ": " + ex.Message);
                Thread.Sleep(250);
            }
        }
        string alt = path + ".transparent.png";
        File.WriteAllBytes(alt, pngBytes);
        Console.WriteLine("Wrote alt " + alt);
    }

    private static void Enq(byte[] bytes, bool[] visited, Queue<int> queue, int stride, int w, int h, int x, int y)
    {
        if (x < 0 || y < 0 || x >= w || y >= h) return;
        int index = y * w + x;
        if (visited[index]) return;
        int o = y * stride + x * 4;
        byte b = bytes[o], g = bytes[o + 1], r = bytes[o + 2], a = bytes[o + 3];
        if (a >= 12)
        {
            int min = Math.Min(r, Math.Min(g, b));
            int max = Math.Max(r, Math.Max(g, b));
            if (max - min > 16) return;
            int avg = (r + g + b) / 3;
            if (avg < 70 || avg > 245) return;
        }
        visited[index] = true;
        queue.Enqueue(index);
    }
}
