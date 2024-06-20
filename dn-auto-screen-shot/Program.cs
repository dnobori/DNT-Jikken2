using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Threading;

namespace dn_auto_screen_shot
{
    public class SsData
    {
        public DateTimeOffset Dt;
        public string Hash;
        public byte[] Data;
    }

    internal class Program
    {
        static HashAlgorithm sha1Hash = SHA1.Create();

        static void Main(string[] args)
        {
            string destDir;
            if (args.Length < 1)
            {
                Console.Write("Please specify the dest dir: ");
                destDir = Console.ReadLine();
            }
            else
            {
                destDir = args[0];
            }

            DateTime startDt = DateTime.Now;

            if (destDir.Trim().Length == 0)
            {
                Console.WriteLine("Error: dest dir not specified");
                return;
            }

            int seqno = 0;

            string lastHash = "";
            while (true)
            {
                var ret = TakeScreenShotIfChanged(lastHash);

                if (ret != null)
                {
                    lastHash = ret.Hash;

                    var dt = ret.Dt.LocalDateTime;

                    string fn = startDt.ToString("yyyyMMddHHmmss") + "-" + seqno.ToString("D8") + "-" + dt.ToString("yyyyMMdd") + "-" + dt.ToString("HHmmss") + ".png";
                    seqno++;

                    string fullPath = Path.Combine(destDir, fn);

                    string dirName = Path.GetDirectoryName(fullPath);

                    try
                    {
                        if (Directory.Exists(dirName) == false)
                        {
                            Directory.CreateDirectory(dirName);
                        }
                    }
                    catch { }

                    try
                    {
                        File.WriteAllBytes(fullPath, ret.Data);
                        Console.WriteLine($"OK: {Path.GetFileNameWithoutExtension(fn)}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error writing to {fullPath}");
                        Console.WriteLine(ex.ToString());
                    }
                }

                Thread.Sleep(10);
            }
        }

        static SsData TakeScreenShotIfChanged(string lastHash)
        {
            var screen0 = Screen.AllScreens.Where(x => x.Bounds != null).OrderBy(x => x.Bounds.Left).ThenBy(x => x.Bounds.Top).ThenBy(x => x.DeviceName).First().Bounds;

            Rectangle recv = new Rectangle(screen0.X, screen0.Y, screen0.Width, screen0.Height);

            using (Bitmap bmp = new Bitmap(recv.Width, recv.Height))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(recv.X, recv.Y, 0, 0, recv.Size);
                    var now = DateTimeOffset.Now;

                    MemoryStream bmpMs = new MemoryStream();
                    bmp.Save(bmpMs, ImageFormat.Bmp);
                    byte[] tmpData = bmpMs.ToArray();

                    byte[] hash = sha1Hash.ComputeHash(tmpData);

                    string hashStr = BitConverter.ToString(hash);

                    if (lastHash == hashStr)
                    {
                        return null;
                    }

                    MemoryStream pngMs = new MemoryStream();

                    bmp.Save(pngMs, System.Drawing.Imaging.ImageFormat.Png);

                    SsData ret = new SsData
                    {
                        Data = pngMs.ToArray(),
                        Hash = hashStr,
                        Dt = now,
                    };

                    return ret;
                }
            }
        }
    }
}
