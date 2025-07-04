using MPI;
using System;
using System.Threading;
using System.Collections.Generic;

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
            HashSet<int> externalDistributors = new HashSet<int>(); // Track distributors from other provinces

            // Initialize local distributors
            for (int i = 1; i < size; i++)
            {
                distributorQueue.Enqueue(i);
                distributorStatus[i] = false; // false = available, true = busy
            }

            Console.WriteLine($"[Province {provinceIndex} | Rank {rank}] Leader started, waiting for orders...");

            // Wait for order count from Master
            totalOrders = worldComm.Receive<int>(0, 2); // Tag 2 for order count
            Console.WriteLine($"[Province {provinceIndex}] Received {totalOrders} orders to distribute");

            int nextOrderId = 1;
            int finishedOrders = 0;
            int lastReportedDistributorCount = distributorQueue.Count;

            while (finishedOrders < totalOrders)
            {
                // Check for incoming external distributors
                CheckForIncomingDistributors(worldComm, distributorQueue, distributorStatus, externalDistributors);

                // Assign orders to available distributors
                AssignOrdersToDistributors(provinceComm, distributorQueue, distributorStatus, ref nextOrderId, totalOrders, provinceIndex);

                // Check for completed orders
                finishedOrders += ProcessCompletedOrders(provinceComm, distributorQueue, distributorStatus, worldComm, provinceIndex, externalDistributors);

                // Check if we need more distributors
                CheckAndRequestMoreDistributors(worldComm, distributorQueue, distributorStatus, totalOrders, finishedOrders, provinceIndex, ref lastReportedDistributorCount);

                Thread.Sleep(10);
            }

            // Send termination signals to local distributors
            SendTerminationSignals(provinceComm, size, distributorStatus, externalDistributors);

            // Send external distributors back to their provinces
            SendExternalDistributorsBack(worldComm, externalDistributors);

            // Report completion to Master
            ReportCompletion(worldComm, rank);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[Province {provinceIndex}] has finished distributing all orders ✅");
            Console.ResetColor();
        }

        private static void CheckForIncomingDistributors(Intracommunicator worldComm, Queue<int> distributorQueue,
            Dictionary<int, bool> distributorStatus, HashSet<int> externalDistributors)
        {
            Status status = worldComm.ImmediateProbe(0, 12); // Tag 12 for incoming distributors
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

            // Check local distributors
            Status localStatus = provinceComm.ImmediateProbe(MPI.Unsafe.MPI_ANY_SOURCE, 1);
            if (localStatus != null)
            {
                int finishedDistributor = localStatus.Source;
                int completedOrderId = provinceComm.Receive<int>(finishedDistributor, 1);

                distributorStatus[finishedDistributor] = false;
                distributorQueue.Enqueue(finishedDistributor);
                completedOrders++;

                Console.WriteLine($"[Province {provinceIndex}] Local Distributor {finishedDistributor} completed order {completedOrderId}");

                // Report available distributor to Master
                ReportDistributorAvailable(worldComm, finishedDistributor, provinceIndex);
            }

            // Check external distributors
            foreach (int externalDistributor in externalDistributors)
            {
                Status externalStatus = worldComm.ImmediateProbe(externalDistributor, 1);
                if (externalStatus != null)
                {
                    int completedOrderId = worldComm.Receive<int>(externalDistributor, 1);

                    distributorStatus[externalDistributor] = false;
                    distributorQueue.Enqueue(externalDistributor);
                    completedOrders++;

                    Console.WriteLine($"[Province {provinceIndex}] External Distributor {externalDistributor} completed order {completedOrderId}");
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
            if (remainingOrders > 5 && availableDistributors < 2 && availableDistributors < lastReportedDistributorCount)
            {
                RequestMoreDistributors(worldComm, remainingOrders, availableDistributors, provinceIndex);
                lastReportedDistributorCount = availableDistributors;
            }
        }

        private static void SendTerminationSignals(Intracommunicator provinceComm, int size,
            Dictionary<int, bool> distributorStatus, HashSet<int> externalDistributors)
        {
            for (int i = 1; i < size; i++)
            {
                if (!externalDistributors.Contains(i))
                {
                    provinceComm.Send(-1, i, 0);
                }
            }
        }

        private static void SendExternalDistributorsBack(Intracommunicator worldComm, HashSet<int> externalDistributors)
        {
            foreach (int externalDistributor in externalDistributors)
            {
                worldComm.Send(-1, externalDistributor, 0); // Send termination signal
                Console.WriteLine($"[Province] Sent external distributor {externalDistributor} back to their province");
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

            worldComm.Send(report, 0, 10); // Tag 10 for reports
        }

        private static void ReportDistributorAvailable(Intracommunicator worldComm, int distributorRank, int provinceIndex)
        {
            var report = new ProvinceReport
            {
                ProvinceLeaderRank = Communicator.world.Rank,
                ReportType = ReportType.DistributorAvailable,
                DistributorRank = distributorRank
            };

            worldComm.Send(report, 0, 10); // Tag 10 for reports
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

            worldComm.Send(report, 0, 10); // Tag 10 for reports

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