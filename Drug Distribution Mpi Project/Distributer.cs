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
            int size = provinceComm.Size;

            Console.WriteLine($"[Distributor Rank {rank} | Province {provinceIndex}] Starting distributor process.");

            while (true)
            {
                var task = ReceiveTask(provinceComm);

                if (task == null)
                {
                    Console.WriteLine($"[Distributor Rank {rank}] No more tasks. Exiting.");
                    break;  
                }

                Console.WriteLine($"[Distributor Rank {rank}] Processing order {task.OrderId}...");
                Thread.Sleep(input.AvgDeliveryTime * 1000); 

                Console.WriteLine($"[Distributor Rank {rank}] Finished order {task.OrderId}.");

                provinceComm.Send(task.OrderId, 0, 1); 
            }
        }

        private class DeliveryTask
        {
            public int OrderId { get; set; }
        }

        private static DeliveryTask ReceiveTask(Intracommunicator provinceComm)
        {
          
            if (provinceComm.ImmediateProbe(0, 0))
            {
                int orderId = provinceComm.Receive<int>(0, 0);
                return new DeliveryTask { OrderId = orderId };
            }
            else
            {
             
                return null;
            }
        }
    }
}
