using System;

public class TestClass
{
    public static void Main()
    {
        Console.WriteLine("Hello.\n");

        while (true)
        {
            Console.Write("Input>");

            string? tmp = Console.ReadLine();

            if (string.IsNullOrEmpty(tmp))
            {
                break;
            }

            Console.WriteLine(tmp);

            Console.WriteLine();
        }

        Console.WriteLine("");
        Console.WriteLine("Exit.");
    }
}
