using System;
using System.Linq;

namespace Drug_Distribution_Mpi_Project
{
    public class InputData
    {
        public int NumOfProvinces { get; set; }
        public int[] PharmaciesPerProvince { get; set; }
        public int[] DistributorsPerProvince { get; set; }
        public int AvgDeliveryTime { get; set; }
    }

    public static class Input
    {
        public static InputData GetInput()
        {
            var data = new InputData();

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("🔢 Enter the number of provinces:");
            data.NumOfProvinces = ReadPositiveInt();
            Console.ResetColor();

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("💊 Enter the number of pharmacies for each province (with spaces between them):");
            data.PharmaciesPerProvince = ReadIntArray(data.NumOfProvinces);
            Console.ResetColor();

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("🚚 Enter the number of distributors for each province (with spaces between them):");
            data.DistributorsPerProvince = ReadIntArray(data.NumOfProvinces);
            Console.ResetColor();

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("⏱️ Enter the median time to distribute a single order:");
            data.AvgDeliveryTime = ReadPositiveInt();
            Console.ResetColor();

            return data;
        }

        private static int ReadPositiveInt()
        {
            while (true)
            {
                string input = Console.ReadLine();
                if (int.TryParse(input, out int value) && value > 0)
                    return value;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("❌ Please enter a positive number. \n Try again :");
                Console.ResetColor();
            }
        }

        private static int[] ReadIntArray(int expectedLength)
        {
            while (true)
            {
                var input = Console.ReadLine();

                try
                {
                    var numbers = input.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();

                    if (numbers.Length != expectedLength)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"❌ You have to enter exactly {expectedLength} number.\n Try again :");
                        Console.ResetColor();
                        continue;
                    }

                    if (numbers.Any(n => n < 0))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("❌ All numbers must be positive or zero.\n Try again :");
                        Console.ResetColor();
                        continue;
                    }

                    return numbers;
                }
                catch
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("❌ Invalid entry. Please ensure you enter only numbers separated by spaces.");
                    Console.ResetColor();
                }
            }
        }
    }
}
