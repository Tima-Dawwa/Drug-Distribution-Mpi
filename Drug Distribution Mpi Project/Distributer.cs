using MPI;
using System;
using System.Threading;

namespace Drug_Distribution_Mpi_Project
{
    public static class Distributor
    {
        public static void Run(int provinceIndex, Intracommunicator provinceComm, InputData input)
        {
            int rank = provinceComm.Rank;
            int worldRank = Communicator.world.Rank;
            bool isReallocated = false;
            int currentProvinceIndex = provinceIndex;

            Console.WriteLine($"[Distributor Rank {rank} | World Rank {worldRank} | Province {provinceIndex}] Starting distributor process");

            while (true)
            {
                // Check for reallocation commands from Master
                if (CheckForReallocation(worldRank, ref isReallocated, ref currentProvinceIndex))
                {
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine($"[Distributor {worldRank}] Reallocated from Province {provinceIndex} to Province {currentProvinceIndex}");
                    Console.ResetColor();
                }

                // Receive tasks from appropriate province leader
                var task = ReceiveTask(provinceComm, worldRank, isReallocated);

                if (task == null)
                {
                    // No task available, wait a bit and try again
                    Thread.Sleep(100);
                    continue;
                }

                if (task.OrderId == -1)
                {
                    Console.WriteLine($"[Distributor {worldRank}] Received termination signal. Exiting.");
                    break;
                }

                Console.WriteLine($"[Distributor {worldRank} | Province {currentProvinceIndex}] Processing order {task.OrderId}...");

                // Simulate processing time (reduced from original to avoid long waits)
                Thread.Sleep(100);

                Console.WriteLine($"[Distributor {worldRank}] ✅ Completed order {task.OrderId}");

                // Send completion notification to appropriate province leader
                SendCompletionNotification(provinceComm, task, isReallocated, worldRank);
            }

            Console.WriteLine($"[Distributor {worldRank}] Shutting down.");
        }

        private static bool CheckForReallocation(int worldRank, ref bool isReallocated, ref int currentProvinceIndex)
        {
            try
            {
                Status status = Communicator.world.ImmediateProbe(0, 11); // Tag 11 for reallocation commands

                if (status != null)
                {
                    var reallocationCommand = Communicator.world.Receive<ReallocationCommand>(0, 11);
                    isReallocated = true;
                    currentProvinceIndex = reallocationCommand.TargetProvinceIndex;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Distributor {worldRank}] Error checking reallocation: {ex.Message}");
            }

            return false;
        }

        private static DeliveryTask ReceiveTask(Intracommunicator provinceComm, int worldRank, bool isReallocated)
        {
            try
            {
                Status status;

                if (isReallocated)
                {
                    // Check for tasks from Master (via world communicator)
                    status = Communicator.world.ImmediateProbe(MPI.Unsafe.MPI_ANY_SOURCE, 0);

                    if (status != null)
                    {
                        int orderId = Communicator.world.Receive<int>(status.Source, 0);

                        return new DeliveryTask
                        {
                            OrderId = orderId,
                            ProvinceLeaderRank = status.Source
                        };
                    }
                }
                else
                {
                    // Check for tasks from local province leader
                    status = provinceComm.ImmediateProbe(0, 0);

                    if (status != null)
                    {
                        int orderId = provinceComm.Receive<int>(0, 0);

                        return new DeliveryTask
                        {
                            OrderId = orderId,
                            ProvinceLeaderRank = 0 // Local province leader
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Distributor {worldRank}] Error receiving task: {ex.Message}");
            }

            return null;
        }

        private static void SendCompletionNotification(Intracommunicator provinceComm, DeliveryTask task,
            bool isReallocated, int worldRank)
        {
            try
            {
                if (isReallocated)
                {
                    // Send completion to the province we're currently helping via world communicator
                    Communicator.world.Send(task.OrderId, task.ProvinceLeaderRank, 1);
                    Console.WriteLine($"[Distributor {worldRank}] Sent completion of order {task.OrderId} to external province leader {task.ProvinceLeaderRank}");
                }
                else
                {
                    // Send to local province leader
                    provinceComm.Send(task.OrderId, 0, 1);
                    Console.WriteLine($"[Distributor {worldRank}] Sent completion of order {task.OrderId} to local province leader");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Distributor {worldRank}] Error sending completion notification: {ex.Message}");
            }
        }

        private class DeliveryTask
        {
            public int OrderId { get; set; }
            public int ProvinceLeaderRank { get; set; }
        }
    }
}