using MPI;
using System;
using System.Threading;

namespace Drug_Distribution_Mpi_Project
{
    public static class Master
    {
        public static void Run(Intracommunicator worldComm, int size)
        {
            Console.WriteLine(" Starting a parallel drug distribution system using MPI...");

            InputData input = Input.Read();

            for (int i = 1; i < size; i++)
            {
                worldComm.Send(input, i, 0);
            }

            Console.WriteLine(" Master has distributed input to all ranks.");

            int totalOrders = 10;
            for (int i = 0; i < totalOrders; i++)
            {
                int provinceIndex = i % input.NumOfProvinces;
                int targetRank = GetProvinceLeaderRank(provinceIndex, input);

                Console.WriteLine($" Master sending order {i} to Province {provinceIndex} (Leader Rank {targetRank})");
                worldComm.Send(i, targetRank, 1); 
                Thread.Sleep(500); 
            }

            Console.WriteLine(" Master finished sending orders.");
        }

        private static int GetProvinceLeaderRank(int provinceIndex, InputData input)
        {
            return 1 + provinceIndex * (input.DistributorsPerProvince + 1);
        }
    }
}
