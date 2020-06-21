using System;
using System.Diagnostics;

namespace cs_process_leak_test1
{
    class Program
    {
        static void Main(string[] args)
        {
            for (int i = 0; ; i++)
            {
                if ((i % 100) == 0)
                {
                    long mem = GC.GetTotalMemory(false);
                    Console.WriteLine(mem);
                    GC.Collect();
                }

                try
                {
                    using (var proc = Process.Start("/bin/true"))
                    {
                        proc.WaitForExit();
                        proc.Kill(false);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }
        }
    }
}
