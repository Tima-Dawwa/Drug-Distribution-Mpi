using System;
using MPI;

namespace Drug_Distribution_Mpi_Project.Helper
{
    class RoleAssignHelper
    {
        public static void AssignAndRun(Intracommunicator worldComm, int rank, InputData input)
        {
            int totalProvinces = input.NumOfProvinces;
            int provinceIndex = GetProvinceIndex(rank, input);

            if (provinceIndex < 0 || provinceIndex >= totalProvinces)
            {
                Console.WriteLine($"[Rank {rank}] has no assigned role");
                return;
            }

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[Rank {rank}] → Belongs to province number {provinceIndex}");
            Console.ResetColor();

            // Create province communicator
            Intracommunicator provinceComm = (Intracommunicator)worldComm.Split(provinceIndex, rank);

            int localRank = provinceComm.Rank;

            if (localRank == 0)
            {
                Console.WriteLine($"[Rank {rank}] Acting as Province {provinceIndex} Leader");
                Province.RunAsLeader(provinceIndex, provinceComm, input);
            }
            else
            {
                Console.WriteLine($"[Rank {rank}] Acting as Distributor in Province {provinceIndex}");
                Distributor.Run(provinceIndex, provinceComm, input);
            }
        }

        static int GetProvinceIndex(int rank, InputData input)
        {
            int currentRank = 1;

            for (int i = 0; i < input.NumOfProvinces; i++)
            {
                int provinceSize = 1 + input.DistributorsPerProvince[i]; // 1 leader + distributors

                if (rank >= currentRank && rank < currentRank + provinceSize)
                {
                    return i;
                }

                currentRank += provinceSize;
            }

            // If we reach here, the rank doesn't belong to any province
            return -1;
        }

        // Helper method to get province leader rank from province index
        public static int GetProvinceLeaderRank(int provinceIndex, InputData input)
        {
            int currentRank = 1;

            for (int i = 0; i < provinceIndex; i++)
            {
                currentRank += 1 + input.DistributorsPerProvince[i];
            }

            return currentRank;
        }

        // Helper method to validate total ranks needed
        public static int GetTotalRanksNeeded(InputData input)
        {
            int totalRanks = 1; // Master rank

            for (int i = 0; i < input.NumOfProvinces; i++)
            {
                totalRanks += 1 + input.DistributorsPerProvince[i]; // 1 leader + distributors
            }

            return totalRanks;
        }
    }
}