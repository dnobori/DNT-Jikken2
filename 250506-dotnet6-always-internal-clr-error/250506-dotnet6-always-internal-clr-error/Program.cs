
using System.Text;

public static class TestClass250506
{
    public static void Test()
    {
        TestFunc1(Encoding.UTF8, () => 123);
    }

    public static Encoding TestFunc1(Encoding encoding1, Func<int> proc)
    {
        bool flag1 = false; // [Break point here]
        if (encoding1 == null || encoding1.CodePage == 12345)
        {
            Console.WriteLine("Hello 1");
        }
        else
        {
            try
            {
                string str1 = "aaa";
                string str2 = encoding1.GetString(encoding1.GetBytes("bbb"));
                Console.WriteLine("Hello 2");
            }
            catch
            {
            }
        }

        if (flag1) return encoding1!;

        return Encoding.GetEncoding(proc());
    }
}
internal class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Hello, World!");
        TestClass250506.Test();
    }
}

