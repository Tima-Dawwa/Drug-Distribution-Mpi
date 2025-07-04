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

        public static void Run(Intracommunicator worldComm, InputData input)
        {
            Console.WriteLine("Master starting coordination...");

            try
            {
                // Initialize tracking structures
                InitializeTracking(input);

                // Send initial orders to provinces
                SendInitialOrders(worldComm, input);

                // Main monitoring loop with timeout
                MonitorProvincesWithTimeout(worldComm, input);

                Console.WriteLine("Master finished coordinating all provinces");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Master error: {ex.Message}");
            }
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

            Console.WriteLine($"Master initialized tracking for {input.NumOfProvinces} provinces");
        }

        private static void SendInitialOrders(Intracommunicator worldComm, InputData input)
        {
            Console.WriteLine("Master sending initial orders to provinces...");

            for (int provinceIndex = 0; provinceIndex < input.NumOfProvinces; provinceIndex++)
            {
                int totalOrders = input.OrdersPerProvince[provinceIndex];
                int targetRank = provinceLeaderRanks[provinceIndex];

                Console.WriteLine($"Master notifying Province {provinceIndex} (Leader Rank {targetRank}) about {totalOrders} orders");

                try
                {
                    // Send order count to province leader
                    worldComm.Send(totalOrders, targetRank, 2); // Tag 2 for order count
                    Thread.Sleep(50); // Small delay between sends
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Master error sending to Province {provinceIndex}: {ex.Message}");
                }
            }
        }

        private static void MonitorProvincesWithTimeout(Intracommunicator worldComm, InputData input)
        {
            int completedProvinces = 0;
            int maxIterations = 1000; // Prevent infinite loops
            int currentIteration = 0;

            Console.WriteLine("Master starting monitoring loop...");

            while (completedProvinces < input.NumOfProvinces && currentIteration < maxIterations)
            {
                currentIteration++;
                bool foundActivity = false;

                // Check for reports from province leaders
                try
                {
                    Status status = worldComm.ImmediateProbe(MPI.Unsafe.MPI_ANY_SOURCE, 10); // Tag 10 for reports

                    if (status != null)
                    {
                        foundActivity = true;
                        var report = worldComm.Receive<ProvinceReport>(status.Source, 10);
                        ProcessProvinceReport(worldComm, report, input);

                        if (report.ReportType == ReportType.AllOrdersCompleted)
                        {
                            int provinceIndex = GetProvinceIndexFromLeaderRank(report.ProvinceLeaderRank);
                            if (provinceIndex >= 0 && !provinceCompletionStatus[provinceIndex])
                            {
                                provinceCompletionStatus[provinceIndex] = true;
                                completedProvinces++;
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine($"✅ Province {provinceIndex} completed all orders! ({completedProvinces}/{input.NumOfProvinces})");
                                Console.ResetColor();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Master error checking reports: {ex.Message}");
                }

                if (!foundActivity)
                {
                    Thread.Sleep(100); // Wait before next check
                }

                // Print progress every 100 iterations
                if (currentIteration % 100 == 0)
                {
                    Console.WriteLine($"Master monitoring: {completedProvinces}/{input.NumOfProvinces} provinces completed (iteration {currentIteration})");
                }
            }

            if (currentIteration >= maxIterations)
            {
                Console.WriteLine($"Master reached maximum iterations ({maxIterations}). Forcing completion.");
            }
        }

        private static void ProcessProvinceReport(Intracommunicator worldComm, ProvinceReport report, InputData input)
        {
            int provinceIndex = GetProvinceIndexFromLeaderRank(report.ProvinceLeaderRank);

            if (provinceIndex < 0)
            {
                Console.WriteLine($"Master received report from unknown province leader {report.ProvinceLeaderRank}");
                return;
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"📊 Master received report from Province {provinceIndex}: {report.ReportType}");
            Console.ResetColor();

            try
            {
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
            catch (Exception ex)
            {
                Console.WriteLine($"Master error processing report: {ex.Message}");
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

                    try
                    {
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
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Master error reallocating distributor: {ex.Message}");
                        // Put distributor back in available list
                        availableDistributors[sourceProvinceIndex].Add(distributorToMove);
                    }
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
            var kvp = provinceLeaderRanks.FirstOrDefault(x => x.Value == leaderRank);
            return kvp.Key == 0 && kvp.Value == 0 ? -1 : kvp.Key;
        }
    }

    [Serializable]
    public class ProvinceReport
    {
        public int ProvinceLeaderRank { get; set; }
        public ReportType ReportType { get; set; }
        public int DistributorRank { get; set; }
        public int RemainingOrders { get; set; }
        public int ActiveDistributors { get; set; }
    }

    [Serializable]
    public class ReallocationCommand
    {
        public int TargetProvinceIndex { get; set; }
        public int TargetProvinceLeaderRank { get; set; }
        public int SourceProvinceIndex { get; set; }
    }

    [Serializable]
    public enum ReportType
    {
        DistributorAvailable,
        NeedMoreDistributors,
        AllOrdersCompleted,
        StatusUpdate
    }
}