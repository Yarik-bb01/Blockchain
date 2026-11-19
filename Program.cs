using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

class Transaction
{
    public string TransactionId { get; set; }
    public string FromUser { get; set; }
    public string ToUser { get; set; }
    public decimal Amount { get; set; }

    public Transaction(string transactionId, string fromUser, string toUser, decimal amount)
    {
        TransactionId = transactionId;
        FromUser = fromUser;
        ToUser = toUser;
        Amount = amount;
    }

    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(TransactionId)
            && !string.IsNullOrWhiteSpace(FromUser)
            && !string.IsNullOrWhiteSpace(ToUser)
            && FromUser != ToUser
            && Amount > 0;
    }
}

class Block
{
    public int Index { get; set; }
    public string Data { get; set; }
    public string Author { get; set; }
    public DateTime Timestamp { get; set; }
    public string PreviousHash { get; set; }
    public string Hash { get; set; } = "";
    public int Nonce { get; set; }
    public List<Transaction> Transactions { get; set; } = new();

    public Block(int index, string data, string author, string previousHash,
        List<Transaction>? transactions = null)
    {
        Index = index;
        Data = data;
        Author = author;
        Timestamp = DateTime.UtcNow;
        PreviousHash = previousHash;
        Transactions = transactions ?? new List<Transaction>();
    }
}

class HashingService
{
    public static string ComputeHash(Block block)
    {
        string transactionData = string.Join(";", block.Transactions.Select(t =>
            $"{t.TransactionId},{t.FromUser},{t.ToUser},{t.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture)}"));
        string text = $"{block.Index}|{block.Data}|{block.Author}|{block.Timestamp:O}|{block.PreviousHash}|{block.Nonce}|{transactionData}";
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes);
    }
}

class Blockchain
{
    public List<Block> Chain { get; set; } = new();
    public int Difficulty { get; set; } = 2;

    public void AddBlock(string data, string author, List<Transaction>? transactions = null)
    {
        string previousHash = Chain.Count == 0 ? "0" : Chain[^1].Hash;
        Block block = new Block(Chain.Count, data, author, previousHash, transactions);
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

    public Block? FindByIndex(int index) => Chain.Find(block => block.Index == index);

    public List<Block> FindByText(string text) =>
        Chain.FindAll(block => block.Data.Contains(text, StringComparison.OrdinalIgnoreCase));

    public bool IsValid() => IsValid(out _);

    public bool IsValid(out int wrongIndex)
    {
        wrongIndex = -1;
        for (int i = 0; i < Chain.Count; i++)
        {
            Block block = Chain[i];
            if (block.Hash != HashingService.ComputeHash(block)
                || !block.Hash.StartsWith(new string('0', Difficulty))
                || block.PreviousHash != (i == 0 ? "0" : Chain[i - 1].Hash)
                || block.Index != i)
            {
                wrongIndex = block.Index;
                return false;
            }
        }
        return true;
    }

    // Повна перевірка цілісності блокчейна з конкретними причинами помилок.
    public bool AuditBlockchain()
    {
        Console.WriteLine("\n========== BLOCKCHAIN AUDIT ==========");
        bool isValid = true;
        HashSet<string> transactionIds = new(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < Chain.Count; i++)
        {
            Block block = Chain[i];
            bool blockHasErrors = false;

            if (block.Index != i)
            {
                Console.WriteLine($"[ERROR] Block #{block.Index}: неправильний Index, очікувався {i}.");
                blockHasErrors = true;
            }

            string expectedPreviousHash = i == 0 ? "0" : Chain[i - 1].Hash;
            if (block.PreviousHash != expectedPreviousHash)
            {
                Console.WriteLine($"[ERROR] Block #{block.Index}: PreviousHash не відповідає попередньому блоку.");
                blockHasErrors = true;
            }

            string calculatedHash = HashingService.ComputeHash(block);
            if (block.Hash != calculatedHash)
            {
                Console.WriteLine($"[ERROR] Block #{block.Index}: Hash mismatch — дані блоку були змінені.");
                blockHasErrors = true;
            }

            if (!block.Hash.StartsWith(new string('0', Difficulty)))
            {
                Console.WriteLine($"[ERROR] Block #{block.Index}: Hash не відповідає складності майнінгу.");
                blockHasErrors = true;
            }

            foreach (Transaction transaction in block.Transactions)
            {
                if (!transaction.IsValid())
                {
                    Console.WriteLine($"[ERROR] Block #{block.Index}: невалідна транзакція {transaction.TransactionId} (перевірте ID, користувачів та Amount > 0).");
                    blockHasErrors = true;
                }

                if (!transactionIds.Add(transaction.TransactionId))
                {
                    Console.WriteLine($"[ERROR] Block #{block.Index}: повторний TransactionId '{transaction.TransactionId}'.");
                    blockHasErrors = true;
                }
            }

            if (!blockHasErrors)
                Console.WriteLine($"[OK] Block #{block.Index}: перевірку пройдено.");
            else
                isValid = false;
        }

        Console.WriteLine(isValid ? "RESULT: BLOCKCHAIN VALID" : "RESULT: BLOCKCHAIN INVALID");
        return isValid;
    }

    public void PrintReport(bool auditResult)
    {
        List<Transaction> allTransactions = Chain.SelectMany(b => b.Transactions).ToList();
        decimal totalAmount = allTransactions.Sum(t => t.Amount);

        var userOperations = allTransactions
            .SelectMany(t => new[] { t.FromUser, t.ToUser })
            .GroupBy(user => user)
            .Select(group => new { User = group.Key, Count = group.Count() })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.User)
            .FirstOrDefault();

        Console.WriteLine("\n========== AUDIT REPORT ==========");
        Console.WriteLine($"Кількість блоків: {Chain.Count}");
        Console.WriteLine($"Загальна кількість транзакцій: {allTransactions.Count}");
        Console.WriteLine($"Загальна сума переказів: {totalAmount:F2}");
        Console.WriteLine(userOperations == null
            ? "Найактивніший користувач: немає даних"
            : $"Найактивніший користувач: {userOperations.User} ({userOperations.Count} операцій)");
        Console.WriteLine($"Перевірка Blockchain: {(auditResult ? "VALID" : "INVALID")}");
        Console.WriteLine("==================================");
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
        foreach (Transaction transaction in block.Transactions)
            Console.WriteLine($"  TX {transaction.TransactionId}: {transaction.FromUser} -> {transaction.ToUser}, {transaction.Amount:F2}");
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
            Block candidate = new Block(block.Index, block.Data, block.Author, block.PreviousHash,
                block.Transactions.Select(t => new Transaction(t.TransactionId, t.FromUser, t.ToUser, t.Amount)).ToList());
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
                if (nonce > int.MaxValue - threadCount) break;
            }
        });

        if (found == 0) throw new Exception("Hash was not found");
        block.Nonce = resultNonce;
        block.Hash = resultHash;
    }
}

class Program
{
    static async Task Main()
    {
        Blockchain blockchain = new Blockchain();

        blockchain.AddBlock("Genesis", "System", new List<Transaction>
        {
            new("TX001", "System", "Ivan", 1000),
            new("TX002", "System", "Olena", 1500)
        });
        blockchain.AddBlock("Реєстрація користувача", "Ivan", new List<Transaction>
        {
            new("TX003", "Ivan", "Petro", 500),
            new("TX004", "Olena", "Ivan", 250)
        });
        blockchain.AddBlock("Покупка навушників", "Olena", new List<Transaction>
        {
            new("TX005", "Petro", "Olena", 1200),
            new("TX006", "Ivan", "Shop", 800)
        });
        blockchain.AddBlock("Переказ коштів", "Petro", new List<Transaction>
        {
            new("TX007", "Olena", "Petro", 700),
            new("TX008", "Petro", "Ivan", 300)
        });
        blockchain.AddBlock("Оплата доставки", "Ivan", new List<Transaction>
        {
            new("TX009", "Ivan", "Delivery", 150),
            new("TX010", "Olena", "Delivery", 100)
        });

        Console.WriteLine("=== BLOCKCHAIN ===");
        blockchain.PrintChain();

        bool firstAudit = blockchain.AuditBlockchain();
        blockchain.PrintReport(firstAudit);

        // Демонстрація: змінюємо Amount у транзакції вже створеного блоку.
        Console.WriteLine("\n=== TAMPERING TEST ===");
        Console.WriteLine("Змінюємо Amount транзакції TX005 у блоці #2: 1200 -> 9999");
        blockchain.Chain[2].Transactions[0].Amount = 9999;

        bool secondAudit = blockchain.AuditBlockchain();
        blockchain.PrintReport(secondAudit);

        // Зберігаємо попередню демонстрацію асинхронного майнінгу.
        Console.WriteLine("\n=== ASYNC MINING ===");
        await blockchain.AddBlockAsync("Async record", "Ivan", 2);
        Console.WriteLine($"Block #{blockchain.Chain[^1].Index} mined.");
    }
}
