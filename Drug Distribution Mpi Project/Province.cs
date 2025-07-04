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

            Console.WriteLine($"[Province {provinceIndex} | Rank {rank}] Leader started, waiting for orders...");

            // Wait for order count from Master
            totalOrders = worldComm.Receive<int>(0, 2);
            Console.WriteLine($"[Province {provinceIndex}] Received {totalOrders} orders to distribute");

            int nextOrderId = 1;
            int finishedOrders = 0;
            int lastReportedDistributorCount = distributorQueue.Count;

            // Send initial orders to all available distributors
            AssignInitialOrders(provinceComm, distributorQueue, distributorStatus, ref nextOrderId, totalOrders, provinceIndex);

            while (finishedOrders < totalOrders)
            {
                // Check for incoming external distributors
                CheckForIncomingDistributors(worldComm, distributorQueue, distributorStatus, externalDistributors);

                // Assign orders to newly available distributors
                AssignOrdersToDistributors(provinceComm, distributorQueue, distributorStatus, ref nextOrderId, totalOrders, provinceIndex);

                // Check for completed orders (both local and external)
                finishedOrders += ProcessCompletedOrders(provinceComm, distributorQueue, distributorStatus, worldComm, provinceIndex, externalDistributors);

                // Check if we need more distributors
                CheckAndRequestMoreDistributors(worldComm, distributorQueue, distributorStatus, totalOrders, finishedOrders, provinceIndex, ref lastReportedDistributorCount);

                Thread.Sleep(50); // Increased sleep to reduce CPU usage
            }

            // Send termination signals
            SendTerminationSignals(provinceComm, size, distributorStatus, externalDistributors);
            SendExternalDistributorsBack(worldComm, externalDistributors);

            // Report completion to Master
            ReportCompletion(worldComm, rank);

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

                provinceComm.Send(nextOrderId, distributor, 0);
                Console.WriteLine($"[Province {provinceIndex}] Sent initial Order {nextOrderId} to Distributor {distributor}");
                nextOrderId++;
            }
        }

        private static void CheckForIncomingDistributors(Intracommunicator worldComm, Queue<int> distributorQueue,
            Dictionary<int, bool> distributorStatus, HashSet<int> externalDistributors)
        {
            Status status = worldComm.ImmediateProbe(0, 12);
            if (status != null)
            {
                int incomingDistributorRank = worldComm.Receive<int>(0, 12);
                distributorQueue.Enqueue(incomingDistributorRank);
                distributorStatus[incomingDistributorRank] = false;
                externalDistributors.Add(incomingDistributorRank);

                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine($"[Province] Received external distributor {incomingDistributorRank} for assistance");
                Console.ResetColor();
            }
        }

        private static void AssignOrdersToDistributors(Intracommunicator provinceComm, Queue<int> distributorQueue,
            Dictionary<int, bool> distributorStatus, ref int nextOrderId, int totalOrders, int provinceIndex)
        {
            while (distributorQueue.Count > 0 && nextOrderId <= totalOrders)
            {
                int distributor = distributorQueue.Dequeue();
                distributorStatus[distributor] = true;

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
        }

        private static int ProcessCompletedOrders(Intracommunicator provinceComm, Queue<int> distributorQueue,
            Dictionary<int, bool> distributorStatus, Intracommunicator worldComm, int provinceIndex, HashSet<int> externalDistributors)
        {
            int completedOrders = 0;

            // Check local distributors first
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

            // Check external distributors - use ANY_SOURCE to avoid blocking
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

        private static void SendExternalDistributorsBack(Intracommunicator worldComm, HashSet<int> externalDistributors)
        {
            Console.WriteLine($"[Province] Sending {externalDistributors.Count} external distributors back...");
            foreach (int externalDistributor in externalDistributors)
            {
                try
                {
                    worldComm.Send(-1, externalDistributor, 0);
                    Console.WriteLine($"[Province] Sent external distributor {externalDistributor} back to their province");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Province] Error sending back external distributor {externalDistributor}: {ex.Message}");
                }
            }
        }

        private static void ReportCompletion(Intracommunicator worldComm, int rank)
        {
            var report = new ProvinceReport
            {
                ProvinceLeaderRank = rank,
                ReportType = ReportType.AllOrdersCompleted,
                RemainingOrders = 0
            };

            worldComm.Send(report, 0, 10);
            Console.WriteLine($"[Province] Sent completion report to Master");
        }

        private static void ReportDistributorAvailable(Intracommunicator worldComm, int distributorRank, int provinceIndex)
        {
            var report = new ProvinceReport
            {
                ProvinceLeaderRank = Communicator.world.Rank,
                ReportType = ReportType.DistributorAvailable,
                DistributorRank = distributorRank
            };

            worldComm.Send(report, 0, 10);
        }

        private static void RequestMoreDistributors(Intracommunicator worldComm, int remainingOrders, int availableDistributors, int provinceIndex)
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

        private static bool IsLocalDistributor(int distributorRank, Intracommunicator provinceComm)
        {
            return distributorRank < provinceComm.Size;
        }
    }
}