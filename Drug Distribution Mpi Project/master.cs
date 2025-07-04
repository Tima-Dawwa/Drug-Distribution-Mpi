using MPI;
using System;
using System.Threading;

namespace Drug_Distribution_Mpi_Project
{
    public static class Master
    {
        public static void Run(Intracommunicator worldComm, int size)
        {
            InputData input = Input.GetInput();

            for (int i = 1; i < size; i++)
            {
                worldComm.Send(input, i, 0);
            }

            Console.WriteLine("Master has distributed input to all ranks");

            for (int provinceIndex = 0; provinceIndex < input.NumOfProvinces; provinceIndex++)
            {
                int totalOrders = input.OrdersPerProvince[provinceIndex];

                for (int j = 0; j < totalOrders; j++)
                {
                    int targetRank = GetProvinceLeaderRank(provinceIndex, input);

                    Console.WriteLine($"Master sending order {j} to Province {provinceIndex} (Leader Rank {targetRank})");
                    worldComm.Send(j, targetRank, 1);
                    Thread.Sleep(500);
                }
            }

            Console.WriteLine("Master finished sending orders");
        }

        private static int GetProvinceLeaderRank(int provinceIndex, InputData input)
        {
            return 1 + provinceIndex * (input.DistributorsPerProvince[provinceIndex] + 1);
        }
    }
}
