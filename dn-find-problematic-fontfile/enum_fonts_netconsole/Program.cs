using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;

namespace enum_fonts_netconsole
{
    internal class Program
    {
        static void Main(string[] args)
        {
            PrivateFontCollection pf = new PrivateFontCollection();

            //pf.AddFontFile(@"c:\windows\fonts\ERASLGHT.TTF");

            add_all(pf, @"c:\windows\fonts");
            add_all(pf, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Windows\Fonts"));

            foreach (var f in pf.Families)
            {
                Console.WriteLine(f.Name);
            }
        }

        static void add_all(PrivateFontCollection pf, string dir)
        {
            var files = Directory.EnumerateFiles(dir);
            foreach (var file in files)
            {
                Debug.WriteLine(file);
                //Console.WriteLine(file);
                pf.AddFontFile(file);
            }
        }
    }
}
