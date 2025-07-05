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
            bool terminationRequested = false;

            Console.WriteLine($"[Distributor Rank {rank} | Province {provinceIndex} | World Rank {worldRank}] Starting distributor process");

            while (!terminationRequested)
            {
                // Check for termination signals first
                if (CheckForTermination(worldRank))
                {
                    Console.WriteLine($"[Distributor {worldRank}] Received termination signal. Exiting.");
                    terminationRequested = true;
                    break;
                }

                // Check for reallocation commands from Master
                if (CheckForReallocation(worldRank, ref isReallocated, ref currentProvinceIndex))
                {
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine($"[Distributor {worldRank}] Reallocated from Province {provinceIndex} to Province {currentProvinceIndex}");
                    Console.ResetColor();
                }

                // Receive tasks from appropriate source
                var task = ReceiveTask(provinceComm, worldRank, isReallocated, currentProvinceIndex);

                if (task == null)
                {
                    // No task available, wait a bit and try again
                    Thread.Sleep(50);
                    continue;
                }

                if (task.OrderId == -1)
                {
                    Console.WriteLine($"[Distributor {worldRank}] Received task termination signal. Exiting.");
                    terminationRequested = true;
                    break;
                }

                Console.WriteLine($"[Distributor {worldRank} | Province {currentProvinceIndex}] Processing order {task.OrderId}...");

                // Simulate processing time based on input delivery time
                Thread.Sleep(input.AvgDeliveryTime * 10); // Scale down for simulation

                Console.WriteLine($"✓ [Distributor {worldRank}] completed order {task.OrderId}");

                // Send completion notification to the province that assigned this task
                SendCompletionNotification(provinceComm, task, isReallocated, worldRank, currentProvinceIndex);

                // After completing a task, report availability
                ReportAvailability(worldRank, isReallocated, currentProvinceIndex);
            }

            Console.WriteLine($"[Distributor {worldRank}] Shutting down.");
        }

        private static bool CheckForTermination(int worldRank)
        {
            try
            {
                Status status = Communicator.world.ImmediateProbe(0, 99); // Tag 99 for termination

                if (status != null)
                {
                    int terminationSignal = Communicator.world.Receive<int>(0, 99);
                    return terminationSignal == -1;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Distributor {worldRank}] Error checking termination: {ex.Message}");
            }

            return false;
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

        private static DeliveryTask ReceiveTask(Intracommunicator provinceComm, int worldRank, bool isReallocated, int currentProvinceIndex)
        {
            try
            {
                Status status;

                if (isReallocated)
                {
                    // Check for tasks from any province leader via world communicator
                    status = Communicator.world.ImmediateProbe(MPI.Unsafe.MPI_ANY_SOURCE, 0);

                    if (status != null)
                    {
                        int orderId = Communicator.world.Receive<int>(status.Source, 0);

                        return new DeliveryTask
                        {
                            OrderId = orderId,
                            AssigningProvinceLeaderRank = status.Source, // Track who assigned this task
                            IsFromExternalProvince = true
                        };
                    }
                }
                else
                {
                    // Check for tasks from local province leader
                    status = provinceComm.ImmediateProbe(0, 0); // Tag 0 for task assignment

                    if (status != null)
                    {
                        int orderId = provinceComm.Receive<int>(0, 0);

                        return new DeliveryTask
                        {
                            OrderId = orderId,
                            AssigningProvinceLeaderRank = 0, // Local province leader (rank 0 in province communicator)
                            IsFromExternalProvince = false
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
            bool isReallocated, int worldRank, int currentProvinceIndex)
        {
            try
            {
                if (task.IsFromExternalProvince)
                {
                    // Send completion to the province leader who assigned this task via world communicator
                    Communicator.world.Send(task.OrderId, task.AssigningProvinceLeaderRank, 1); // Tag 1 for completion
                    Console.WriteLine($"\n[Distributor {worldRank}] Sent completion of order {task.OrderId} to assigning province leader {task.AssigningProvinceLeaderRank}");
                }
                else
                {
                    // Send to local province leader
                    provinceComm.Send(task.OrderId, 0, 1); // Tag 1 for completion
                    Console.WriteLine($"\n[Distributor {worldRank}] Sent completion of order {task.OrderId} to local province leader");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Distributor {worldRank}] Error sending completion notification: {ex.Message}");
            }
        }

        private static void ReportAvailability(int worldRank, bool isReallocated, int currentProvinceIndex)
        {
            try
            {
                // Report availability to Master
                var availabilityReport = new ProvinceReport
                {
                    ProvinceLeaderRank = worldRank, // Use world rank as identifier
                    ReportType = ReportType.DistributorAvailable,
                    DistributorRank = worldRank
                };

                Communicator.world.Send(availabilityReport, 0, 10); // Tag 10 for reports to Master
                Console.WriteLine($"\n[Distributor {worldRank}] Reported availability to Master");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Distributor {worldRank}] Error reporting availability: {ex.Message}");
            }
        }

        private class DeliveryTask
        {
            public int OrderId { get; set; }
            public int AssigningProvinceLeaderRank { get; set; }
            public bool IsFromExternalProvince { get; set; }
        }
    }
}