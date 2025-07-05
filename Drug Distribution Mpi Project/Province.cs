using MPI;
using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;

namespace Drug_Distribution_Mpi_Project
{
    public static class Province
    {
        public static void RunAsLeader(int provinceIndex, Intracommunicator provinceComm, InputData input)
        {
            int rank = provinceComm.Rank;
            int size = provinceComm.Size;
            Intracommunicator worldComm = Communicator.world;
            int worldRank = worldComm.Rank;

            int numDistributors = input.DistributorsPerProvince[provinceIndex];
            int totalOrders = 0;

            Queue<int> distributorQueue = new Queue<int>();
            Dictionary<int, bool> distributorStatus = new Dictionary<int, bool>();
            HashSet<int> externalDistributors = new HashSet<int>();

            // Initialize local distributors
            for (int i = 1; i < size; i++)
            {
                distributorQueue.Enqueue(i);
                distributorStatus[i] = false;
            }

            Console.WriteLine($"[Province {provinceIndex} | World Rank {worldRank}] Leader started, waiting for orders...");

            try
            {
                // Wait for order count from Master with timeout
                Console.WriteLine($"[Province {provinceIndex}] Waiting for order count from Master...");
                totalOrders = worldComm.Receive<int>(0, 2);

                // Send acknowledgment back to Master
                worldComm.Send(1, 0, 3); // Tag 3 for acknowledgment

                Console.WriteLine($"[Province {provinceIndex}] Received {totalOrders} orders to distribute, sent acknowledgment");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Province {provinceIndex}] Error receiving orders from Master: {ex.Message}");
                return;
            }

            if (totalOrders <= 0)
            {
                Console.WriteLine($"[Province {provinceIndex}] No orders to process, shutting down");
                SendTerminationSignals(provinceComm, size, distributorStatus, externalDistributors);
                return;
            }

            int nextOrderId = 1;
            int finishedOrders = 0;
            int lastReportedDistributorCount = distributorQueue.Count;

            // Send initial orders to all available distributors
            AssignInitialOrders(provinceComm, distributorQueue, distributorStatus, ref nextOrderId, totalOrders, provinceIndex);

            while (finishedOrders < totalOrders)
            {
                try
                {
                    // Check for termination signal from Master
                    Status terminationStatus = worldComm.ImmediateProbe(0, 99);
                    if (terminationStatus != null)
                    {
                        Console.WriteLine($"[Province {provinceIndex}] Received termination signal from Master");
                        break;
                    }

                    // Check for incoming external distributors
                    CheckForIncomingDistributors(worldComm, distributorQueue, distributorStatus, externalDistributors, provinceIndex);

                    // Assign orders to newly available distributors
                    AssignOrdersToDistributors(provinceComm, distributorQueue, distributorStatus, ref nextOrderId, totalOrders, provinceIndex);

                    // Check for completed orders (both local and external)
                    int completedThisIteration = ProcessCompletedOrders(provinceComm, distributorQueue, distributorStatus, worldComm, provinceIndex, externalDistributors);
                    finishedOrders += completedThisIteration;

                    // Check if we need more distributors
                    CheckAndRequestMoreDistributors(worldComm, distributorQueue, distributorStatus, totalOrders, finishedOrders, provinceIndex, ref lastReportedDistributorCount);

                    Thread.Sleep(50); // Small delay to prevent busy waiting
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Province {provinceIndex}] Error in main loop: {ex.Message}");
                    break;
                }
            }

            // Send termination signals
            SendTerminationSignals(provinceComm, size, distributorStatus, externalDistributors);
            SendExternalDistributorsBack(worldComm, externalDistributors, provinceIndex);

            // Report completion to Master
            ReportCompletion(worldComm, worldRank, provinceIndex);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[Province {provinceIndex}] has finished distributing all orders ✅");
            Console.ResetColor();
        }

        private static void AssignInitialOrders(Intracommunicator provinceComm, Queue<int> distributorQueue,
            Dictionary<int, bool> distributorStatus, ref int nextOrderId, int totalOrders, int provinceIndex)
        {
            Console.WriteLine($"[Province {provinceIndex}] Assigning initial orders...");

            // Assign orders to all initially available distributors
            while (distributorQueue.Count > 0 && nextOrderId <= totalOrders)
            {
                int distributor = distributorQueue.Dequeue();
                distributorStatus[distributor] = true;

                try
                {
                    provinceComm.Send(nextOrderId, distributor, 0);
                    Console.WriteLine($"[Province {provinceIndex}] Sent initial Order {nextOrderId} to Distributor {distributor}");
                    nextOrderId++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Province {provinceIndex}] Error sending initial order to distributor {distributor}: {ex.Message}");
                    distributorStatus[distributor] = false;
                    distributorQueue.Enqueue(distributor);
                }
            }
        }

        private static void CheckForIncomingDistributors(Intracommunicator worldComm, Queue<int> distributorQueue,
            Dictionary<int, bool> distributorStatus, HashSet<int> externalDistributors, int provinceIndex)
        {
            try
            {
                Status status = worldComm.ImmediateProbe(0, 12);
                if (status != null)
                {
                    int incomingDistributorRank = worldComm.Receive<int>(0, 12);
                    distributorQueue.Enqueue(incomingDistributorRank);
                    distributorStatus[incomingDistributorRank] = false;
                    externalDistributors.Add(incomingDistributorRank);

                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine($"[Province {provinceIndex}] Received external distributor {incomingDistributorRank} for assistance");
                    Console.ResetColor();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Province {provinceIndex}] Error checking for incoming distributors: {ex.Message}");
            }
        }

        private static void AssignOrdersToDistributors(Intracommunicator provinceComm, Queue<int> distributorQueue,
            Dictionary<int, bool> distributorStatus, ref int nextOrderId, int totalOrders, int provinceIndex)
        {
            while (distributorQueue.Count > 0 && nextOrderId <= totalOrders)
            {
                int distributor = distributorQueue.Dequeue();
                distributorStatus[distributor] = true;

                try
                {
                    if (IsLocalDistributor(distributor, provinceComm))
                    {
                        provinceComm.Send(nextOrderId, distributor, 0);
                    }
                    else
                    {
                        // Send order to external distributor via world communicator
                        Communicator.world.Send(nextOrderId, distributor, 0);
                    }

                    Console.WriteLine($"[Province {provinceIndex}] Sent Order {nextOrderId} to Distributor {distributor}");
                    nextOrderId++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Province {provinceIndex}] Error sending order to distributor {distributor}: {ex.Message}");
                    distributorStatus[distributor] = false;
                    distributorQueue.Enqueue(distributor);
                }
            }
        }

        private static int ProcessCompletedOrders(Intracommunicator provinceComm, Queue<int> distributorQueue,
            Dictionary<int, bool> distributorStatus, Intracommunicator worldComm, int provinceIndex, HashSet<int> externalDistributors)
        {
            int completedOrders = 0;

            // Check local distributors first
            try
            {
                while (true)
                {
                    Status localStatus = provinceComm.ImmediateProbe(MPI.Unsafe.MPI_ANY_SOURCE, 1);
                    if (localStatus == null) break;

                    int finishedDistributor = localStatus.Source;
                    int completedOrderId = provinceComm.Receive<int>(finishedDistributor, 1);

                    if (distributorStatus.ContainsKey(finishedDistributor))
                    {
                        distributorStatus[finishedDistributor] = false;
                        distributorQueue.Enqueue(finishedDistributor);
                        completedOrders++;

                        Console.WriteLine($"[Province {provinceIndex}] Local Distributor {finishedDistributor} completed order {completedOrderId}");
                        ReportDistributorAvailable(worldComm, finishedDistributor, provinceIndex);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Province {provinceIndex}] Error processing local completions: {ex.Message}");
            }

            // Check external distributors
            try
            {
                while (true)
                {
                    Status externalStatus = worldComm.ImmediateProbe(MPI.Unsafe.MPI_ANY_SOURCE, 1);
                    if (externalStatus == null) break;

                    // Only process if it's from an external distributor we're tracking
                    if (externalDistributors.Contains(externalStatus.Source))
                    {
                        int completedOrderId = worldComm.Receive<int>(externalStatus.Source, 1);

                        if (distributorStatus.ContainsKey(externalStatus.Source))
                        {
                            distributorStatus[externalStatus.Source] = false;
                            distributorQueue.Enqueue(externalStatus.Source);
                            completedOrders++;

                            Console.WriteLine($"[Province {provinceIndex}] External Distributor {externalStatus.Source} completed order {completedOrderId}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Province {provinceIndex}] Error processing external completions: {ex.Message}");
            }

            return completedOrders;
        }

        private static void CheckAndRequestMoreDistributors(Intracommunicator worldComm, Queue<int> distributorQueue,
            Dictionary<int, bool> distributorStatus, int totalOrders, int finishedOrders, int provinceIndex, ref int lastReportedDistributorCount)
        {
            int availableDistributors = distributorQueue.Count;
            int remainingOrders = totalOrders - finishedOrders;

            // Request help if we have many orders remaining but few available distributors
            if (remainingOrders > 3 && availableDistributors == 0 && lastReportedDistributorCount != availableDistributors)
            {
                RequestMoreDistributors(worldComm, remainingOrders, availableDistributors, provinceIndex);
                lastReportedDistributorCount = availableDistributors;
            }
        }

        private static void SendTerminationSignals(Intracommunicator provinceComm, int size,
            Dictionary<int, bool> distributorStatus, HashSet<int> externalDistributors)
        {
            Console.WriteLine($"[Province] Sending termination signals to local distributors...");
            for (int i = 1; i < size; i++)
            {
                if (!externalDistributors.Contains(i))
                {
                    try
                    {
                        provinceComm.Send(-1, i, 0);
                        Console.WriteLine($"[Province] Sent termination to local distributor {i}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Province] Error sending termination to distributor {i}: {ex.Message}");
                    }
                }
            }
        }

        private static void SendExternalDistributorsBack(Intracommunicator worldComm, HashSet<int> externalDistributors, int provinceIndex)
        {
            Console.WriteLine($"[Province {provinceIndex}] Sending {externalDistributors.Count} external distributors back...");
            foreach (int externalDistributor in externalDistributors)
            {
                try
                {
                    worldComm.Send(-1, externalDistributor, 0);
                    Console.WriteLine($"[Province {provinceIndex}] Sent external distributor {externalDistributor} back to their province");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Province {provinceIndex}] Error sending back external distributor {externalDistributor}: {ex.Message}");
                }
            }
        }

        private static void ReportCompletion(Intracommunicator worldComm, int worldRank, int provinceIndex)
        {
            try
            {
                var report = new ProvinceReport
                {
                    ProvinceLeaderRank = worldRank,
                    ReportType = ReportType.AllOrdersCompleted,
                    RemainingOrders = 0
                };

                worldComm.Send(report, 0, 10);
                Console.WriteLine($"[Province {provinceIndex}] Sent completion report to Master");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Province {provinceIndex}] Error sending completion report: {ex.Message}");
            }
        }

        private static void ReportDistributorAvailable(Intracommunicator worldComm, int distributorRank, int provinceIndex)
        {
            try
            {
                var report = new ProvinceReport
                {
                    ProvinceLeaderRank = Communicator.world.Rank,
                    ReportType = ReportType.DistributorAvailable,
                    DistributorRank = distributorRank
                };

                worldComm.Send(report, 0, 10);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Province {provinceIndex}] Error reporting distributor available: {ex.Message}");
            }
        }

        private static void RequestMoreDistributors(Intracommunicator worldComm, int remainingOrders, int availableDistributors, int provinceIndex)
        {
            try
            {
                var report = new ProvinceReport
                {
                    ProvinceLeaderRank = worldComm.Rank,
                    ReportType = ReportType.NeedMoreDistributors,
                    RemainingOrders = remainingOrders,
                    ActiveDistributors = availableDistributors
                };

                worldComm.Send(report, 0, 10);

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[Province {provinceIndex}] Requesting help: {remainingOrders} orders remaining, {availableDistributors} distributors available");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Province {provinceIndex}] Error requesting more distributors: {ex.Message}");
            }
        }

        private static bool IsLocalDistributor(int distributorRank, Intracommunicator provinceComm)
        {
            return distributorRank < provinceComm.Size;
        }
    }
}