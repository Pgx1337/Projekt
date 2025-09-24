using System;
using System.Collections.Generic;
using System.Linq;
using System.Data.SqlClient;
using System.Configuration;

namespace RouletteGame
{
    public enum BetType
    {
        SingleNumber,
        Red,
        Black,
        Green,
        Even,
        Odd,
        FirstHalf,      // 1-18
        SecondHalf,     // 19-36
        FirstDozen,     // 1-12
        SecondDozen,    // 13-24
        ThirdDozen,     // 25-36
        FirstColumn,    // 1,4,7,...,34
        SecondColumn,   // 2,5,8,...,35
        ThirdColumn,    // 3,6,9,...,36
        Tiers,          // 27,13,36,11,30,8,23,10,5,24,16,33
        Orphelins,      // 17,34,6,1,20,14,31,9
        Voisins,        // 22,18,29,7,28,12,35,3,26,0,32,15,19,4,21,2,25
        ZeroNeighbors   // 26,3,35,12,28,7,29,18,22,32,15,19,4,21,2,25
    }

    public class Bet
    {
        public BetType Type { get; set; }
        public int? Number { get; set; }
        public decimal Amount { get; set; }
        public decimal PayoutMultiplier { get; set; }

        public Bet(BetType type, decimal amount, int? number = null)
        {
            Type = type;
            Amount = amount;
            Number = number;
            PayoutMultiplier = GetPayoutMultiplier(type);
        }

        private static decimal GetPayoutMultiplier(BetType type)
        {
            return type switch
            {
                BetType.SingleNumber => 36m,
                BetType.Red or BetType.Black or BetType.Even or BetType.Odd 
                or BetType.FirstHalf or BetType.SecondHalf => 2m,
                BetType.Green => 36m,
                BetType.FirstDozen or BetType.SecondDozen or BetType.ThirdDozen 
                or BetType.FirstColumn or BetType.SecondColumn or BetType.ThirdColumn => 3m,
                BetType.Tiers or BetType.Orphelins or BetType.Voisins or BetType.ZeroNeighbors => 36m / 5m,
                _ => 1m
            };
        }
    }

    public class RouletteWheel
    {
        private readonly Random _random;
        private readonly int[] _redNumbers = { 1, 3, 5, 7, 9, 12, 14, 16, 18, 19, 21, 23, 25, 27, 30, 32, 34, 36 };
        private readonly int[] _blackNumbers = { 2, 4, 6, 8, 10, 11, 13, 15, 17, 20, 22, 24, 26, 28, 29, 31, 33, 35 };
        private readonly int[] _tiersNumbers = { 27, 13, 36, 11, 30, 8, 23, 10, 5, 24, 16, 33 };
        private readonly int[] _orphelinsNumbers = { 17, 34, 6, 1, 20, 14, 31, 9 };
        private readonly int[] _voisinsNumbers = { 22, 18, 29, 7, 28, 12, 35, 3, 26, 0, 32, 15, 19, 4, 21, 2, 25 };

        public RouletteWheel()
        {
            _random = new Random();
        }

        public int Spin()
        {
            return _random.Next(0, 37);
        }

        public bool IsRed(int number) => _redNumbers.Contains(number);
        public bool IsBlack(int number) => _blackNumbers.Contains(number);
        public bool IsGreen(int number) => number == 0;
        public bool IsEven(int number) => number != 0 && number % 2 == 0;
        public bool IsOdd(int number) => number != 0 && number % 2 == 1;
        public bool IsFirstHalf(int number) => number >= 1 && number <= 18;
        public bool IsSecondHalf(int number) => number >= 19 && number <= 36;
        
        public bool IsInDozen(int number, int dozen)
        {
            return dozen switch
            {
                1 => number >= 1 && number <= 12,
                2 => number >= 13 && number <= 24,
                3 => number >= 25 && number <= 36,
                _ => false
            };
        }

        public bool IsInColumn(int number, int column)
        {
            return column switch
            {
                1 => number % 3 == 1 && number != 0,
                2 => number % 3 == 2 && number != 0,
                3 => number % 3 == 0 && number != 0,
                _ => false
            };
        }

        public bool IsTiers(int number) => _tiersNumbers.Contains(number);
        public bool IsOrphelins(int number) => _orphelinsNumbers.Contains(number);
        public bool IsVoisins(int number) => _voisinsNumbers.Contains(number);
    }

    public class DatabaseManager
    {
        private readonly string _connectionString;

        public DatabaseManager()
        {
            _connectionString = ConfigurationManager.ConnectionStrings["RouletteDB"].ConnectionString;
        }

        public decimal GetAccountBalance(int accountId)
        {
            decimal balance = 0;
            
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                string query = "SELECT AccountBalance FROM Accounts WHERE AccountId = @AccountId";
                
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@AccountId", accountId);
                    
                    connection.Open();
                    var result = command.ExecuteScalar();
                    
                    if (result != null && result != DBNull.Value)
                    {
                        balance = Convert.ToDecimal(result);
                    }
                }
            }
            
            return balance;
        }

        public bool UpdateAccountBalance(int accountId, decimal newBalance)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                string query = "UPDATE Accounts SET AccountBalance = @Balance WHERE AccountId = @AccountId";
                
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Balance", newBalance);
                    command.Parameters.AddWithValue("@AccountId", accountId);
                    
                    connection.Open();
                    int rowsAffected = command.ExecuteNonQuery();
                    
                    return rowsAffected > 0;
                }
            }
        }

        public void RecordTransaction(int accountId, decimal amount, string transactionType, string description)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                string query = @"INSERT INTO Transactions (AccountId, Amount, TransactionType, Description, TransactionDate)
                             VALUES (@AccountId, @Amount, @TransactionType, @Description, GETDATE())";
                
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@AccountId", accountId);
                    command.Parameters.AddWithValue("@Amount", amount);
                    command.Parameters.AddWithValue("@TransactionType", transactionType);
                    command.Parameters.AddWithValue("@Description", description);
                    
                    connection.Open();
                    command.ExecuteNonQuery();
                }
            }
        }
    }

    public class RouletteGame
    {
        private readonly RouletteWheel _wheel;
        private readonly DatabaseManager _dbManager;
        private decimal _playerBalance;
        private readonly List<Bet> _currentBets;
        private readonly int _accountId;

        public RouletteGame(int accountId)
        {
            _wheel = new RouletteWheel();
            _dbManager = new DatabaseManager();
            _accountId = accountId;
            _playerBalance = _dbManager.GetAccountBalance(accountId);
            _currentBets = new List<Bet>();
        }

        public void PlaceBet(Bet bet)
        {
            // Refresh balance from database to ensure we have latest amount
            RefreshBalance();

            if (bet.Amount > _playerBalance)
            {
                Console.WriteLine("Insufficient balance!");
                return;
            }

            _currentBets.Add(bet);
            _playerBalance -= bet.Amount;
            
            // Update database
            if (_dbManager.UpdateAccountBalance(_accountId, _playerBalance))
            {
                _dbManager.RecordTransaction(_accountId, -bet.Amount, "BET", $"Placed {bet.Type} bet");
                Console.WriteLine($"Bet placed: {bet.Type} - ${bet.Amount}");
            }
            else
            {
                Console.WriteLine("Error updating balance in database!");
            }
        }

        public void SpinWheel()
        {
            if (_currentBets.Count == 0)
            {
                Console.WriteLine("No bets placed!");
                return;
            }

            int winningNumber = _wheel.Spin();
            Console.WriteLine($"\n=== SPINNING THE WHEEL ===");
            Console.WriteLine($"The ball lands on: {winningNumber}");

            string color = _wheel.IsRed(winningNumber) ? "RED" : 
                          _wheel.IsBlack(winningNumber) ? "BLACK" : "GREEN";
            Console.WriteLine($"Color: {color}");

            decimal totalWinnings = 0;

            foreach (var bet in _currentBets)
            {
                if (IsWinningBet(bet, winningNumber))
                {
                    decimal winAmount = bet.Amount * bet.PayoutMultiplier;
                    totalWinnings += winAmount;
                    Console.WriteLine($"WIN! {bet.Type} bet pays ${winAmount}");
                }
            }

            _playerBalance += totalWinnings;
            
            // Update database with winnings
            if (_dbManager.UpdateAccountBalance(_accountId, _playerBalance))
            {
                if (totalWinnings > 0)
                {
                    _dbManager.RecordTransaction(_accountId, totalWinnings, "WIN", $"Roulette winnings from spin");
                }
                Console.WriteLine($"Total winnings: ${totalWinnings}");
                Console.WriteLine($"New balance: ${_playerBalance}");
            }
            else
            {
                Console.WriteLine("Error updating balance in database!");
            }

            _currentBets.Clear();
        }

        private bool IsWinningBet(Bet bet, int winningNumber)
        {
            return bet.Type switch
            {
                BetType.SingleNumber => bet.Number == winningNumber,
                BetType.Red => _wheel.IsRed(winningNumber),
                BetType.Black => _wheel.IsBlack(winningNumber),
                BetType.Green => _wheel.IsGreen(winningNumber),
                BetType.Even => _wheel.IsEven(winningNumber),
                BetType.Odd => _wheel.IsOdd(winningNumber),
                BetType.FirstHalf => _wheel.IsFirstHalf(winningNumber),
                BetType.SecondHalf => _wheel.IsSecondHalf(winningNumber),
                BetType.FirstDozen => _wheel.IsInDozen(winningNumber, 1),
                BetType.SecondDozen => _wheel.IsInDozen(winningNumber, 2),
                BetType.ThirdDozen => _wheel.IsInDozen(winningNumber, 3),
                BetType.FirstColumn => _wheel.IsInColumn(winningNumber, 1),
                BetType.SecondColumn => _wheel.IsInColumn(winningNumber, 2),
                BetType.ThirdColumn => _wheel.IsInColumn(winningNumber, 3),
                BetType.Tiers => _wheel.IsTiers(winningNumber),
                BetType.Orphelins => _wheel.IsOrphelins(winningNumber),
                BetType.Voisins => _wheel.IsVoisins(winningNumber),
                BetType.ZeroNeighbors => winningNumber == 0 || _wheel.IsVoisins(winningNumber),
                _ => false
            };
        }

        public void RefreshBalance()
        {
            _playerBalance = _dbManager.GetAccountBalance(_accountId);
        }

        public void DisplayBalance()
        {
            RefreshBalance(); // Always get latest balance from database
            Console.WriteLine($"Current balance: ${_playerBalance}");
        }

        public void DisplayBettingOptions()
        {
            Console.WriteLine("\n=== BETTING OPTIONS ===");
            Console.WriteLine("1. Single number (0-36) - 36:1");
            Console.WriteLine("2. Red - 2:1");
            Console.WriteLine("3. Black - 2:1");
            Console.WriteLine("4. Green (0) - 36:1");
            Console.WriteLine("5. Even - 2:1");
            Console.WriteLine("6. Odd - 2:1");
            Console.WriteLine("7. First half (1-18) - 2:1");
            Console.WriteLine("8. Second half (19-36) - 2:1");
            Console.WriteLine("9. First dozen (1-12) - 3:1");
            Console.WriteLine("10. Second dozen (13-24) - 3:1");
            Console.WriteLine("11. Third dozen (25-36) - 3:1");
            Console.WriteLine("12. First column - 3:1");
            Console.WriteLine("13. Second column - 3:1");
            Console.WriteLine("14. Third column - 3:1");
            Console.WriteLine("15. Tiers du cylindre - ~7:1");
            Console.WriteLine("16. Orphelins - ~7:1");
            Console.WriteLine("17. Voisins du zero - ~7:1");
            Console.WriteLine("18. Zero neighbors - ~7:1");
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== ROULETTE GAME ===");
            Console.WriteLine("Welcome to the roulette table!");

            Console.Write("Please enter your Account ID: ");
            if (!int.TryParse(Console.ReadLine(), out int accountId))
            {
                Console.WriteLine("Invalid Account ID!");
                return;
            }

            RouletteGame game = new RouletteGame(accountId);
            bool playing = true;

            while (playing)
            {
                Console.WriteLine("\n=== MAIN MENU ===");
                Console.WriteLine("1. View balance");
                Console.WriteLine("2. View betting options");
                Console.WriteLine("3. Place bet");
                Console.WriteLine("4. Spin wheel");
                Console.WriteLine("5. Refresh balance from database");
                Console.WriteLine("6. Exit");
                Console.Write("Choose an option: ");

                if (!int.TryParse(Console.ReadLine(), out int choice))
                {
                    Console.WriteLine("Invalid input!");
                    continue;
                }

                switch (choice)
                {
                    case 1:
                        game.DisplayBalance();
                        break;

                    case 2:
                        game.DisplayBettingOptions();
                        break;

                    case 3:
                        PlaceBetMenu(game);
                        break;

                    case 4:
                        game.SpinWheel();
                        break;

                    case 5:
                        game.RefreshBalance();
                        game.DisplayBalance();
                        break;

                    case 6:
                        playing = false;
                        Console.WriteLine("Thanks for playing!");
                        break;

                    default:
                        Console.WriteLine("Invalid option!");
                        break;
                }
            }
        }

        static void PlaceBetMenu(RouletteGame game)
        {
            Console.WriteLine("\n=== PLACE BET ===");
            Console.Write("Enter bet amount: ");
            
            if (!decimal.TryParse(Console.ReadLine(), out decimal amount) || amount <= 0)
            {
                Console.WriteLine("Invalid amount!");
                return;
            }

            Console.WriteLine("Choose bet type (1-18): ");
            game.DisplayBettingOptions();
            Console.Write("Enter bet type: ");

            if (!int.TryParse(Console.ReadLine(), out int betType) || betType < 1 || betType > 18)
            {
                Console.WriteLine("Invalid bet type!");
                return;
            }

            Bet bet = betType switch
            {
                1 => CreateSingleNumberBet(amount),
                2 => new Bet(BetType.Red, amount),
                3 => new Bet(BetType.Black, amount),
                4 => new Bet(BetType.Green, amount),
                5 => new Bet(BetType.Even, amount),
                6 => new Bet(BetType.Odd, amount),
                7 => new Bet(BetType.FirstHalf, amount),
                8 => new Bet(BetType.SecondHalf, amount),
                9 => new Bet(BetType.FirstDozen, amount),
                10 => new Bet(BetType.SecondDozen, amount),
                11 => new Bet(BetType.ThirdDozen, amount),
                12 => new Bet(BetType.FirstColumn, amount),
                13 => new Bet(BetType.SecondColumn, amount),
                14 => new Bet(BetType.ThirdColumn, amount),
                15 => new Bet(BetType.Tiers, amount),
                16 => new Bet(BetType.Orphelins, amount),
                17 => new Bet(BetType.Voisins, amount),
                18 => new Bet(BetType.ZeroNeighbors, amount),
                _ => null
            };

            if (bet != null)
            {
                game.PlaceBet(bet);
            }
        }

        static Bet CreateSingleNumberBet(decimal amount)
        {
            Console.Write("Enter number (0-36): ");
            if (!int.TryParse(Console.ReadLine(), out int number) || number < 0 || number > 36)
            {
                Console.WriteLine("Invalid number!");
                return null;
            }
            return new Bet(BetType.SingleNumber, amount, number);
        }
    }
}