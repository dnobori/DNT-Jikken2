using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace cs_linux_samba_inconsistency
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Random rand = new Random((int)DateTime.Now.Ticks);
            string dirName = @"\\lts\DataRoot\tmp\test1\";

            Directory.CreateDirectory(dirName);

            while (true)
            {
                for (int i = 0; i < 32; i++)
                {
                    string fileName = $"test.{i:D4}.dat";
                    string filePath = Path.Combine(dirName, fileName);

                    Console.WriteLine(filePath);

                    int size = rand.Next() % 10_000_000;

                    byte[] randomData = new byte[size];
                    byte[] readData = new byte[size];

                    rand.NextBytes(randomData);

                    await using (var f = File.Create(filePath))
                    {
                        await f.WriteAsync(randomData, 0, randomData.Length);
                    }

                    await using (var f = File.OpenRead(filePath))
                    {
                        await f.ReadAsync(readData);
                    }

                    int r = randomData.AsSpan().SequenceCompareTo(readData.AsSpan());
                    if (r != 0)
                    {
                        Console.WriteLine($"*** Different !!!!!!!!!");
                    }
                }
            }
        }
    }
}
