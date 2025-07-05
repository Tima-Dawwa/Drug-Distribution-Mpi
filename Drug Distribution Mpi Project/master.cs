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
        private static Dictionary<int, int> provinceOrderCounts = new Dictionary<int, int>();
        private static Dictionary<int, int> provinceCompletedOrders = new Dictionary<int, int>();

        public static void Run(Intracommunicator worldComm, InputData input)
        {
            Console.WriteLine("Master starting coordination...");

            try
            {
                // Initialize tracking structures
                InitializeTracking(input);

                // Send initial orders to provinces with confirmation
                SendInitialOrdersWithConfirmation(worldComm, input);

                // Main monitoring loop with timeout
                MonitorProvincesWithTimeout(worldComm, input);

                // Send termination signals to all processes
                SendTerminationSignals(worldComm, input);

                Console.WriteLine("Master finished coordinating all provinces");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Master error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        private static void InitializeTracking(InputData input)
        {
            int currentRank = 1;

            for (int i = 0; i < input.NumOfProvinces; i++)
            {
                provinceLeaderRanks[i] = currentRank;
                provinceCompletionStatus[i] = false;
                provinceOrderCounts[i] = input.OrdersPerProvince[i];
                provinceCompletedOrders[i] = 0;
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

        private static void SendInitialOrdersWithConfirmation(Intracommunicator worldComm, InputData input)
        {
            Console.WriteLine("Master sending initial orders to provinces...");

            // Send all order counts first
            for (int provinceIndex = 0; provinceIndex < input.NumOfProvinces; provinceIndex++)
            {
                int totalOrders = input.OrdersPerProvince[provinceIndex];
                int targetRank = provinceLeaderRanks[provinceIndex];
                Console.WriteLine($"Master notifying Province {provinceIndex} (Leader Rank {targetRank}) about {totalOrders} orders");
                worldComm.Send(totalOrders, targetRank, 2); // Tag 2 for order count
            }

            // Then wait for acknowledgments from all provinces
            for (int provinceIndex = 0; provinceIndex < input.NumOfProvinces; provinceIndex++)
            {
                int targetRank = provinceLeaderRanks[provinceIndex];
                try
                {
                    int ack = worldComm.Receive<int>(targetRank, 3); // Tag 3 for acknowledgment
                    Console.WriteLine($"Master received acknowledgment from Province {provinceIndex}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Master error receiving acknowledgment from Province {provinceIndex}: {ex.Message}");
                }
            }

            Console.WriteLine("All provinces have been notified and acknowledged");
        }

        private static void MonitorProvincesWithTimeout(Intracommunicator worldComm, InputData input)
        {
            int completedProvinces = 0;
            int maxIterations = 3000; // Increased timeout
            int currentIteration = 0;
            int consecutiveNoActivityCount = 0;

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
                        consecutiveNoActivityCount = 0;
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
                                Console.WriteLine($"Province {provinceIndex} completed all orders! ({completedProvinces}/{input.NumOfProvinces})");
                                Console.ResetColor();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Master error checking reports: {ex.Message}");
                }

                // Check if we can infer completion from available distributors
                if (!foundActivity)
                {
                    consecutiveNoActivityCount++;
                    CheckForImplicitCompletion(ref completedProvinces, input);
                }

                if (!foundActivity)
                {
                    Thread.Sleep(50); // Reduced sleep time for better responsiveness
                }

                // Print progress every 200 iterations
                if (currentIteration % 200 == 0)
                {
                    Console.WriteLine($"Master monitoring: {completedProvinces}/{input.NumOfProvinces} provinces completed (iteration {currentIteration})");
                    PrintProvinceStatus(input);
                }

                // If no activity for a long time, force check completion
                if (consecutiveNoActivityCount > 100)
                {
                    Console.WriteLine("Master: No activity detected for a while, checking for implicit completion...");
                    CheckForImplicitCompletion(ref completedProvinces, input);
                    consecutiveNoActivityCount = 0;
                }
            }

            if (currentIteration >= maxIterations)
            {
                Console.WriteLine($"Master reached maximum iterations ({maxIterations}). Forcing completion.");
                ForceCompletion(ref completedProvinces, input);
            }
        }

        private static void CheckForImplicitCompletion(ref int completedProvinces, InputData input)
        {
            for (int provinceIndex = 0; provinceIndex < input.NumOfProvinces; provinceIndex++)
            {
                if (!provinceCompletionStatus[provinceIndex])
                {
                    // Check if all distributors from this province are available
                    // This might indicate the province has completed its work
                    int expectedDistributors = input.DistributorsPerProvince[provinceIndex];
                    int availableFromThisProvince = availableDistributors[provinceIndex].Count;

                    // If we have reports of available distributors equal to expected count,
                    // it's likely the province is done
                    if (availableFromThisProvince >= expectedDistributors)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"Master: Inferring Province {provinceIndex} completion based on available distributors ({availableFromThisProvince}/{expectedDistributors})");
                        Console.ResetColor();

                        provinceCompletionStatus[provinceIndex] = true;
                        completedProvinces++;
                    }
                }
            }
        }

        private static void ForceCompletion(ref int completedProvinces, InputData input)
        {
            // Force completion by marking all provinces as complete
            for (int i = 0; i < input.NumOfProvinces; i++)
            {
                if (!provinceCompletionStatus[i])
                {
                    Console.WriteLine($"Force completing Province {i}");
                    provinceCompletionStatus[i] = true;
                    completedProvinces++;
                }
            }
        }

        private static void PrintProvinceStatus(InputData input)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("=== Province Status ===");
            for (int i = 0; i < input.NumOfProvinces; i++)
            {
                string status = provinceCompletionStatus[i] ? "COMPLETED" : "RUNNING";
                int availableCount = availableDistributors[i].Count;
                int totalDistributors = input.DistributorsPerProvince[i];
                Console.WriteLine($"Province {i}: {status} | Available Distributors: {availableCount}/{totalDistributors}");
            }
            Console.WriteLine("=====================");
            Console.ResetColor();
        }

        private static void SendTerminationSignals(Intracommunicator worldComm, InputData input)
        {
            Console.WriteLine("Master sending termination signals...");

            // Send termination to all non-master processes
            for (int rank = 1; rank < worldComm.Size; rank++)
            {
                try
                {
                    worldComm.Send(-1, rank, 99); // Tag 99 for termination
                    Thread.Sleep(10); // Small delay to ensure message delivery
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Master error sending termination to rank {rank}: {ex.Message}");
                }
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
            Console.WriteLine($"Master received report from Province {provinceIndex}: {report.ReportType}");
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

                    case ReportType.StatusUpdate:
                        HandleStatusUpdate(report, provinceIndex);
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

        private static void HandleStatusUpdate(ProvinceReport report, int provinceIndex)
        {
            provinceCompletedOrders[provinceIndex] = report.RemainingOrders;
            Console.WriteLine($"📊 Province {provinceIndex} progress: {report.RemainingOrders} orders remaining");
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
            foreach (var kvp in provinceLeaderRanks)
            {
                if (kvp.Value == leaderRank)
                {
                    return kvp.Key;
                }
            }
            return -1;
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