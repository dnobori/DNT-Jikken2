using System;

internal class Program
{
    private enum Hand
    {
        Rock = 0,
        Scissors = 1,
        Paper = 2
    }

    private static readonly Random Random = new();

    static void Main(string[] args)
    {
        Console.WriteLine("じゃんけんゲームを始めます。");
        Console.WriteLine("0: グー, 1: チョキ, 2: パー");

        var playerHand = ReadPlayerHand();
        var computerHand = GetComputerHand();

        Console.WriteLine($"あなた: {ToJapanese(playerHand)}");
        Console.WriteLine($"コンピューター: {ToJapanese(computerHand)}");

        var result = Judge(playerHand, computerHand);
        Console.WriteLine(ResultMessage(result));
    }

    private static Hand ReadPlayerHand()
    {
        while (true)
        {
            Console.Write("あなたの手を数字で入力してください: ");
            var input = Console.ReadLine();

            if (int.TryParse(input, out var value) &&
                Enum.IsDefined(typeof(Hand), value))
            {
                return (Hand)value;
            }

            Console.WriteLine("0, 1, 2 のいずれかを入力してください。");
        }
    }

    private static Hand GetComputerHand()
    {
        var value = Random.Next(0, 3);
        return (Hand)value;
    }

    private static string ToJapanese(Hand hand)
    {
        return hand switch
        {
            Hand.Rock => "グー",
            Hand.Scissors => "チョキ",
            Hand.Paper => "パー",
            _ => hand.ToString()
        };
    }

    private static int Judge(Hand player, Hand computer)
    {
        // 0 = draw, 1 = lose, 2 = win
        return ((int)player - (int)computer + 3) % 3;
    }

    private static string ResultMessage(int resultCode)
    {
        return resultCode switch
        {
            0 => "あいこです。",
            1 => "あなたの負けです。",
            2 => "あなたの勝ちです！",
            _ => "結果が判定できませんでした。"
        };
    }
}
