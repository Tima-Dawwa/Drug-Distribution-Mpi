using MPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Drug_Distribution_Mpi_Project
{
    public static class Master
    {
        private static Dictionary<int, List<int>> availableDistributors = new Dictionary<int, List<int>>();
        private static Dictionary<int, int> provinceLeaderRanks = new Dictionary<int, int>();
        private static Dictionary<int, bool> provinceCompletionStatus = new Dictionary<int, bool>();

        public static void Run(Intracommunicator worldComm, int size)
        {
            InputData input = Input.GetInput();

            // Initialize tracking structures
            InitializeTracking(input);

            // Send input to all ranks
            for (int i = 1; i < size; i++)
            {
                worldComm.Send(input, i, 0);
            }

            Console.WriteLine("Master has distributed input to all ranks");

            // Send initial orders to provinces
            SendInitialOrders(worldComm, input);

            // Main monitoring loop
            MonitorProvinces(worldComm, input);

            Console.WriteLine("Master finished coordinating all provinces");
        }

        private static void InitializeTracking(InputData input)
        {
            int currentRank = 1;

            for (int i = 0; i < input.NumOfProvinces; i++)
            {
                provinceLeaderRanks[i] = currentRank;
                provinceCompletionStatus[i] = false;
                availableDistributors[i] = new List<int>();

                // Initialize with all distributors as potentially available
                for (int j = 1; j <= input.DistributorsPerProvince[i]; j++)
                {
                    availableDistributors[i].Add(currentRank + j);
                }

                currentRank += input.DistributorsPerProvince[i] + 1;
            }
        }

        private static void SendInitialOrders(Intracommunicator worldComm, InputData input)
        {
            for (int provinceIndex = 0; provinceIndex < input.NumOfProvinces; provinceIndex++)
            {
                int totalOrders = input.OrdersPerProvince[provinceIndex];
                int targetRank = provinceLeaderRanks[provinceIndex];

                Console.WriteLine($"Master notifying Province {provinceIndex} (Leader Rank {targetRank}) about {totalOrders} orders");

                // Send order count to province leader
                worldComm.Send(totalOrders, targetRank, 2); // Tag 2 for order count
                Thread.Sleep(100);
            }
        }

        private static void MonitorProvinces(Intracommunicator worldComm, InputData input)
        {
            int completedProvinces = 0;

            while (completedProvinces < input.NumOfProvinces)
            {
                // Check for reports from province leaders
                Status status = worldComm.ImmediateProbe(MPI.Unsafe.MPI_ANY_SOURCE, 10); // Tag 10 for reports

                if (status != null)
                {
                    var report = worldComm.Receive<ProvinceReport>(status.Source, 10);
                    ProcessProvinceReport(worldComm, report, input);

                    if (report.ReportType == ReportType.AllOrdersCompleted)
                    {
                        int provinceIndex = GetProvinceIndexFromLeaderRank(report.ProvinceLeaderRank);
                        if (!provinceCompletionStatus[provinceIndex])
                        {
                            provinceCompletionStatus[provinceIndex] = true;
                            completedProvinces++;
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"✅ Province {provinceIndex} completed all orders! ({completedProvinces}/{input.NumOfProvinces})");
                            Console.ResetColor();
                        }
                    }
                }

                Thread.Sleep(50);
            }
        }

        private static void ProcessProvinceReport(Intracommunicator worldComm, ProvinceReport report, InputData input)
        {
            int provinceIndex = GetProvinceIndexFromLeaderRank(report.ProvinceLeaderRank);

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"📊 Master received report from Province {provinceIndex}: {report.ReportType}");
            Console.ResetColor();

            switch (report.ReportType)
            {
                case ReportType.DistributorAvailable:
                    HandleDistributorAvailable(report, provinceIndex);
                    break;

                case ReportType.NeedMoreDistributors:
                    HandleDistributorShortage(worldComm, report, provinceIndex, input);
                    break;

                case ReportType.AllOrdersCompleted:
                    HandleProvinceCompletion(provinceIndex);
                    break;
            }
        }

        private static void HandleDistributorAvailable(ProvinceReport report, int provinceIndex)
        {
            if (!availableDistributors[provinceIndex].Contains(report.DistributorRank))
            {
                availableDistributors[provinceIndex].Add(report.DistributorRank);
                Console.WriteLine($"📋 Distributor {report.DistributorRank} in Province {provinceIndex} is now available");
            }
        }

        private static void HandleDistributorShortage(Intracommunicator worldComm, ProvinceReport report, int needyProvinceIndex, InputData input)
        {
            // Find an available distributor from another province
            for (int sourceProvinceIndex = 0; sourceProvinceIndex < input.NumOfProvinces; sourceProvinceIndex++)
            {
                if (sourceProvinceIndex == needyProvinceIndex || provinceCompletionStatus[sourceProvinceIndex])
                    continue;

                if (availableDistributors[sourceProvinceIndex].Count > 0)
                {
                    int distributorToMove = availableDistributors[sourceProvinceIndex][0];
                    availableDistributors[sourceProvinceIndex].RemoveAt(0);

                    // Send reallocation command
                    var reallocationCommand = new ReallocationCommand
                    {
                        TargetProvinceIndex = needyProvinceIndex,
                        TargetProvinceLeaderRank = provinceLeaderRanks[needyProvinceIndex],
                        SourceProvinceIndex = sourceProvinceIndex
                    };

                    worldComm.Send(reallocationCommand, distributorToMove, 11); // Tag 11 for reallocation

                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine($"🔄 Master reallocating Distributor {distributorToMove} from Province {sourceProvinceIndex} to Province {needyProvinceIndex}");
                    Console.ResetColor();

                    // Notify target province about incoming help
                    worldComm.Send(distributorToMove, provinceLeaderRanks[needyProvinceIndex], 12); // Tag 12 for incoming distributor

                    return;
                }
            }

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"⚠️ No available distributors found to help Province {needyProvinceIndex}");
            Console.ResetColor();
        }

        private static void HandleProvinceCompletion(int provinceIndex)
        {
            Console.WriteLine($"📋 Province {provinceIndex} completed - distributors available for reallocation");
        }

        private static int GetProvinceIndexFromLeaderRank(int leaderRank)
        {
            return provinceLeaderRanks.FirstOrDefault(x => x.Value == leaderRank).Key;
        }

        private static int GetProvinceLeaderRank(int provinceIndex, InputData input)
        {
            return provinceLeaderRanks[provinceIndex];
        }
    }

  
    public class ProvinceReport
    {
        public int ProvinceLeaderRank { get; set; }
        public ReportType ReportType { get; set; }
        public int DistributorRank { get; set; }
        public int RemainingOrders { get; set; }
        public int ActiveDistributors { get; set; }
    }

    public class ReallocationCommand
    {
        public int TargetProvinceIndex { get; set; }
        public int TargetProvinceLeaderRank { get; set; }
        public int SourceProvinceIndex { get; set; }
    }

    public enum ReportType
    {
        DistributorAvailable,
        NeedMoreDistributors,
        AllOrdersCompleted,
        StatusUpdate
    }
}