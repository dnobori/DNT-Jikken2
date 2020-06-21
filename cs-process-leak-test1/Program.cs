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
                    Console.WriteLine(i);
                    GC.Collect();
                }

                try
                {
                    ProcessStartInfo info = new ProcessStartInfo()
                    {
                        FileName = "/bin/uname",
                        UseShellExecute = false,

                        RedirectStandardOutput = true,
                        RedirectStandardError = false,
                        RedirectStandardInput = false,

                        CreateNoWindow = false,
                        Arguments = "-a",
                        WorkingDirectory = "/",
                    };

                    var Proc = Process.Start(info);

                    Proc.WaitForExit();

                    Proc.StandardOutput.ReadToEnd();
                    Proc.StandardOutput.Close();
                    Proc.StandardOutput.Dispose();

                    Proc.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }
        }
    }
}
