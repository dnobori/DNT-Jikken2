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
        Console.WriteLine("おこしやす。じゃんけんの宿『グー・チョキ・パー』どす。ごゆっくりしていっておくれやす！");
        Console.WriteLine("0: グー, 1: チョキ, 2: パー でございます。");
        Console.WriteLine("お好きなだけ遊んでおくれやす。おいとまの際は \"Q\" とお書きくださいな。");
        Console.WriteLine();

        while (true)
        {
            var playerHand = ReadPlayerHandOrQuit();
            if (playerHand == null)
            {
                Console.WriteLine("本日はお付き合いありがとさんどした。またお越しやす。");
                break;
            }

            var computerHand = GetComputerHand();

            Console.WriteLine($"あなた: {HandLabel(playerHand.Value)}");
            Console.WriteLine($"お宿: {HandLabel(computerHand)}");

            var result = Judge(playerHand.Value, computerHand);
            Console.WriteLine(ResultMessage(result));

            Console.WriteLine();
            Console.WriteLine("— 次の手を思いつかはったら、いつでもどうぞ。おいとまは \"Q\" と書いておくれやす。");
            Console.WriteLine();
        }
    }

    private static Hand? ReadPlayerHandOrQuit()
    {
        while (true)
        {
            Console.Write("お手をどうぞ (0: グー, 1: チョキ, 2: パー, Q: おいとま): ");
            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
            {
                continue;
            }

            if (input.Trim().Equals("q", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (int.TryParse(input, out var value) &&
                Enum.IsDefined(typeof(Hand), value))
            {
                return (Hand)value;
            }

            Console.WriteLine("0, 1, 2 か Q をお選びくださいな。");
        }
    }

    private static Hand GetComputerHand()
    {
        var value = Random.Next(0, 3);
        return (Hand)value;
    }

    private static string HandLabel(Hand hand)
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
            0 => "引き分けどすな。",
            1 => "申し訳おへん、お宿の勝ちでおす。",
            2 => "ようお勝ちなはりましたな！",
            _ => "判じかねますわ。"
        };
    }
}
