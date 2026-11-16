using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

class Block
{
    public int Index { get; set; }
    public string Data { get; set; }
    public string Author { get; set; }
    public DateTime Timestamp { get; set; }
    public string PreviousHash { get; set; }
    public string Hash { get; set; } = "";
    public int Nonce { get; set; }

    public Block(int index, string data, string author, string previousHash)
    {
        Index = index;
        Data = data;
        Author = author;
        Timestamp = DateTime.UtcNow;
        PreviousHash = previousHash;
    }
}

class HashingService
{
    public static string ComputeHash(Block block)
    {
        string text = $"{block.Index}|{block.Data}|{block.Author}|{block.Timestamp:O}|{block.PreviousHash}|{block.Nonce}";
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes);
    }
}

class Blockchain
{
    public List<Block> Chain { get; set; } = new();
    public int Difficulty { get; set; } = 2;

    public void AddBlock(string data, string author)
    {
        string previousHash = Chain.Count == 0 ? "0" : Chain[^1].Hash;
        Block block = new Block(Chain.Count, data, author, previousHash);
        MiningService.MineSingle(block, Difficulty);
        Chain.Add(block);
    }

    public async Task AddBlockAsync(string data, string author, int difficulty)
    {
        string previousHash = Chain.Count == 0 ? "0" : Chain[^1].Hash;
        Block block = new Block(Chain.Count, data, author, previousHash);
        Task mining = Task.Run(() => MiningService.MineSingle(block, difficulty));

        while (!mining.IsCompleted)
        {
            Console.WriteLine("Main program is working while mining...");
            await Task.WhenAny(mining, Task.Delay(300));
        }

        await mining;
        Chain.Add(block);
    }

    public Block? FindByIndex(int index)
    {
        return Chain.Find(block => block.Index == index);
    }

    public List<Block> FindByText(string text)
    {
        return Chain.FindAll(block => block.Data.Contains(text, StringComparison.OrdinalIgnoreCase));
    }

    public bool IsValid()
    {
        return IsValid(out _);
    }

    public bool IsValid(out int wrongIndex)
    {
        wrongIndex = -1;

        for (int i = 0; i < Chain.Count; i++)
        {
            Block block = Chain[i];
            bool wrongHash = block.Hash != HashingService.ComputeHash(block);
            bool wrongDifficulty = !block.Hash.StartsWith(new string('0', Difficulty));
            bool wrongPrevious = block.PreviousHash != (i == 0 ? "0" : Chain[i - 1].Hash);
            bool wrongIndexNumber = block.Index != i;

            if (wrongHash || wrongDifficulty || wrongPrevious || wrongIndexNumber)
            {
                wrongIndex = block.Index;
                return false;
            }
        }

        return true;
    }

    public void PrintBlock(Block block)
    {
        Console.WriteLine($"Block #{block.Index}");
        Console.WriteLine($"Data: {block.Data}");
        Console.WriteLine($"Author: {block.Author}");
        Console.WriteLine($"Timestamp: {block.Timestamp:yyyy-MM-dd HH:mm:ss} UTC");
        Console.WriteLine($"PreviousHash: {block.PreviousHash}");
        Console.WriteLine($"Hash: {block.Hash}");
        Console.WriteLine($"Nonce: {block.Nonce}");
        Console.WriteLine();
    }

    public void PrintChain()
    {
        foreach (Block block in Chain)
            PrintBlock(block);
    }
}

class MiningService
{
    public static void MineSingle(Block block, int difficulty)
    {
        string prefix = new string('0', difficulty);
        block.Nonce = 0;
        while (true)
        {
            string hash = HashingService.ComputeHash(block);
            if (hash.StartsWith(prefix))
            {
                block.Hash = hash;
                return;
            }
            block.Nonce++;
        }
    }

    public static void MineMulti(Block block, int difficulty, int threadCount)
    {
        string prefix = new string('0', difficulty);
        int found = 0;
        int resultNonce = 0;
        string resultHash = "";

        Parallel.For(0, threadCount, worker =>
        {
            Block candidate = new Block(block.Index, block.Data, block.Author, block.PreviousHash);
            candidate.Timestamp = block.Timestamp;

            for (int nonce = worker; Volatile.Read(ref found) == 0; nonce += threadCount)
            {
                candidate.Nonce = nonce;
                string hash = HashingService.ComputeHash(candidate);
                if (hash.StartsWith(prefix))
                {
                    if (Interlocked.CompareExchange(ref found, 1, 0) == 0)
                    {
                        resultNonce = nonce;
                        resultHash = hash;
                    }
                    break;
                }
                if (nonce > int.MaxValue - threadCount)
                    break;
            }
        });

        if (found == 0)
            throw new Exception("Hash was not found");

        block.Nonce = resultNonce;
        block.Hash = resultHash;
    }
}

class Program
{
    static async Task Main()
    {
        Blockchain blockchain = new Blockchain();
        blockchain.AddBlock("Genesis", "System");
        blockchain.AddBlock("Реєстрація користувача", "Ivan");
        blockchain.AddBlock("Покупка навушників", "Olena");
        blockchain.AddBlock("Переказ коштів", "Petro");
        blockchain.AddBlock("Оплата доставки", "Ivan");
        blockchain.AddBlock("Повернення товару", "Olena");

        Console.WriteLine("=== Blockchain: історія подій ===");
        blockchain.PrintChain();
        Console.WriteLine($"Blockchain valid: {blockchain.IsValid()}");

        Console.WriteLine("\n=== Explorer: пошук за Index = 2 ===");
        Block? foundBlock = blockchain.FindByIndex(2);
        if (foundBlock != null)
            blockchain.PrintBlock(foundBlock);
        else
            Console.WriteLine("Блок не знайдено");

        Console.WriteLine("=== Explorer: пошук за текстом 'покупка' ===");
        List<Block> foundEvents = blockchain.FindByText("покупка");
        foreach (Block block in foundEvents)
            blockchain.PrintBlock(block);
        if (foundEvents.Count == 0)
            Console.WriteLine("Подій не знайдено");

        Console.WriteLine("\nAsync mining (difficulty 5):");
        await blockchain.AddBlockAsync("Async record", "Ivan", 5);
        Console.WriteLine($"Block #{blockchain.Chain[^1].Index} mined, nonce: {blockchain.Chain[^1].Nonce}");
        Console.WriteLine($"Blockchain valid: {blockchain.IsValid()}");

        int difficulty = 5;
        int threads = Math.Max(2, Math.Min(Environment.ProcessorCount, 4));
        string lastHash = blockchain.Chain[^1].Hash;
        Block single = new Block(blockchain.Chain.Count, "Benchmark", "Olena", lastHash);
        Block multi = new Block(blockchain.Chain.Count, "Benchmark", "Olena", lastHash);
        multi.Timestamp = single.Timestamp;

        Console.WriteLine($"\nMining comparison (difficulty {difficulty}, threads {threads}):");

        Stopwatch timer = Stopwatch.StartNew();
        MiningService.MineSingle(single, difficulty);
        timer.Stop();
        double singleTime = timer.Elapsed.TotalSeconds;

        timer.Restart();
        await Task.Run(() => MiningService.MineMulti(multi, difficulty, threads));
        timer.Stop();
        double multiTime = timer.Elapsed.TotalSeconds;

        Console.WriteLine($"Single-thread time: {singleTime:F4} s, nonce: {single.Nonce}");
        Console.WriteLine($"Multi-thread time:  {multiTime:F4} s, nonce: {multi.Nonce}");
        Console.WriteLine($"Faster: {(multiTime < singleTime ? "Multi-thread" : "Single-thread")}");
        Console.WriteLine($"Both hashes valid: {single.Hash.StartsWith(new string('0', difficulty)) && multi.Hash.StartsWith(new string('0', difficulty))}");

        blockchain.Chain.Add(multi);
        Console.WriteLine($"Blockchain valid after multi-thread mining: {blockchain.IsValid()}");

        blockchain.Chain[1].Data = "Змінена подія";
        Console.WriteLine("\nChanged Data of block #1 (second block)");
        bool valid = blockchain.IsValid(out int wrongIndex);
        Console.WriteLine($"Blockchain valid: {valid}");
        Console.WriteLine($"First invalid block: #{wrongIndex}");

        blockchain.Chain[1].Data = "Реєстрація користувача";
        blockchain.Chain[1].Author = "Another author";
        Console.WriteLine("\nChanged Author of block #1 (previous homework)");
        Console.WriteLine($"Blockchain valid: {blockchain.IsValid()}");
    }
}
