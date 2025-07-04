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
                    Console.WriteLine($"[Distributor {worldRank}] No more tasks. Exiting.");
                    break;
                }

                Console.WriteLine($"[Distributor {worldRank} | Province {currentProvinceIndex}] Processing order {task.OrderId}...");

                // Simulate delivery time
                Thread.Sleep(input.AvgDeliveryTime * 1000);

                Console.WriteLine($"[Distributor {worldRank}] Finished order {task.OrderId}");

                // Send completion notification to appropriate province leader
                if (isReallocated)
                {
                    // Send completion to the province we're currently helping
                    Communicator.world.Send(task.OrderId, task.ProvinceLeaderRank, 1);
                }
                else
                {
                    // Send to local province leader
                    provinceComm.Send(task.OrderId, 0, 1);
                }
            }
        }

        private static bool CheckForReallocation(int worldRank, ref bool isReallocated, ref int currentProvinceIndex)
        {
            Status status = Communicator.world.ImmediateProbe(0, 11); // Tag 11 for reallocation commands

            if (status != null)
            {
                var reallocationCommand = Communicator.world.Receive<ReallocationCommand>(0, 11);
                isReallocated = true;
                currentProvinceIndex = reallocationCommand.TargetProvinceIndex;
                return true;
            }

            return false;
        }

        private static DeliveryTask ReceiveTask(Intracommunicator provinceComm, int worldRank, bool isReallocated)
        {
            Status status;

            if (isReallocated)
            {
                // Check for tasks from Master (via world communicator)
                status = Communicator.world.ImmediateProbe(MPI.Unsafe.MPI_ANY_SOURCE, 0);

                if (status != null)
                {
                    int orderId = Communicator.world.Receive<int>(status.Source, 0);

                    if (orderId == -1)
                        return null;

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

                    if (orderId == -1)
                        return null;

                    return new DeliveryTask
                    {
                        OrderId = orderId,
                        ProvinceLeaderRank = 0 // Local province leader
                    };
                }
            }

            return null;
        }

        private class DeliveryTask
        {
            public int OrderId { get; set; }
            public int ProvinceLeaderRank { get; set; }
        }
    }
}