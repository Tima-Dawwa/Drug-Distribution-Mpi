using System;
using System.IO;
using System.Linq;

namespace Drug_Distribution_Mpi_Project
{
    [Serializable]
    public class InputData
    {
        public int NumOfProvinces { get; set; }
        public int[] PharmaciesPerProvince { get; set; }
        public int[] ClinicsPerProvince { get; set; }
        public int[] HospitalsPerProvince { get; set; }
        public int[] DistributorsPerProvince { get; set; }
        public int AvgDeliveryTime { get; set; }
        [NonSerialized]
        private int[] _ordersCache;
        public int[] OrdersPerProvince
        {
            get
            {
                if (_ordersCache == null)
                {
                    _ordersCache = new int[NumOfProvinces];
                    for (int i = 0; i < NumOfProvinces; i++)
                        _ordersCache[i] = PharmaciesPerProvince[i] + ClinicsPerProvince[i] + HospitalsPerProvince[i];
                }
                return _ordersCache;
            }
        }
    }

    public static class Input
    {
        public static InputData GetInput()
        {
            var data = new InputData();

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("Enter the number of provinces: ");
            data.NumOfProvinces = ReadPositiveInt();
            Console.ResetColor();

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("Enter the number of pharmacies for each province (with spaces between them): ");
            data.PharmaciesPerProvince = ReadIntArray(data.NumOfProvinces);
            Console.ResetColor();

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("Enter the number of clinics for each province (with spaces between them): ");
            data.ClinicsPerProvince = ReadIntArray(data.NumOfProvinces);
            Console.ResetColor();

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("Enter the number of hospitals for each province (with spaces between them): ");
            data.HospitalsPerProvince = ReadIntArray(data.NumOfProvinces);
            Console.ResetColor();

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("Enter the number of distributors for each province (with spaces between them): ");
            data.DistributorsPerProvince = ReadIntArray(data.NumOfProvinces);
            Console.ResetColor();

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("Enter the median time to distribute a single order: ");
            data.AvgDeliveryTime = ReadPositiveInt();
            Console.ResetColor();

            int totalProcesses = 1;
            for (int i = 0; i < data.NumOfProvinces; i++)
            {
                totalProcesses += 1 + data.DistributorsPerProvince[i];
            }
            string projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\.."));
            string runnerFolder = Path.Combine(projectRoot, "Runner");
            string processFile = Path.Combine(runnerFolder, "process_count.txt");

            File.WriteAllText(processFile, totalProcesses.ToString());

            Console.WriteLine($"Processes: {totalProcesses} in: {processFile}");
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
                Console.Write("Please enter a positive number.\nTry again: ");
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
                        Console.WriteLine($"You have to enter exactly {expectedLength} numbers.\nTry again: ");
                        Console.ResetColor();
                        continue;
                    }

                    if (numbers.Any(n => n < 0))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("All numbers must be positive or zero.\nTry again: ");
                        Console.ResetColor();
                        continue;
                    }

                    return numbers;
                }
                catch
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Invalid entry. Please ensure you enter only numbers separated by spaces");
                    Console.ResetColor();
                }
            }
        }

        public static void SaveToTextFile(InputData data, string path = "input_data.txt")
        {
            string projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\.."));
            string runnerFolder = Path.Combine(projectRoot, "Runner");
            string inputFile = Path.Combine(runnerFolder, path);

            using (StreamWriter writer = new StreamWriter(inputFile))
            {
                writer.WriteLine(data.NumOfProvinces);
                writer.WriteLine(string.Join(" ", data.PharmaciesPerProvince));
                writer.WriteLine(string.Join(" ", data.ClinicsPerProvince));
                writer.WriteLine(string.Join(" ", data.HospitalsPerProvince));
                writer.WriteLine(string.Join(" ", data.DistributorsPerProvince));
                writer.WriteLine(data.AvgDeliveryTime);
            }
        }

        public static InputData LoadFromTextFile(string path = "input_data.txt")
        {
            string projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\.."));

            string runnerFolder = Path.Combine(projectRoot, "Runner");

            string inputFile = Path.Combine(runnerFolder, path);

            var lines = File.ReadAllLines(inputFile);
            return new InputData
            {
                NumOfProvinces = int.Parse(lines[0]),
                PharmaciesPerProvince = lines[1].Split(' ').Select(int.Parse).ToArray(),
                ClinicsPerProvince = lines[2].Split(' ').Select(int.Parse).ToArray(),
                HospitalsPerProvince = lines[3].Split(' ').Select(int.Parse).ToArray(),
                DistributorsPerProvince = lines[4].Split(' ').Select(int.Parse).ToArray(),
                AvgDeliveryTime = int.Parse(lines[5])
            };
        }
    }
}