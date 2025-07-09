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
                Console.WriteLine($"[Rank {rank}] has no assigned role - likely Master process");

                Intracommunicator masterComm = (Intracommunicator)worldComm.Split(999, rank);
                Master.Run(worldComm, input);
                masterComm.Dispose();
                return;
            }

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[Rank {rank}] is Assigned to Province {provinceIndex}");
            Console.ResetColor();

            Intracommunicator provinceComm = (Intracommunicator)worldComm.Split(provinceIndex, rank);

            int localRank = provinceComm.Rank;
            if (localRank == 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[Rank {rank}] --> LEADER of province {provinceIndex} (Local Rank: {localRank})");
                Console.ResetColor();
                Province.RunAsLeader(provinceIndex, provinceComm, input);
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine($"[Rank {rank}] --> DISTRIBUTOR in province {provinceIndex} (Local Rank: {localRank})");
                Console.ResetColor();
                Distributor.Run(provinceIndex, provinceComm, input);
            }
        }

        static int GetProvinceIndex(int rank, InputData input)
        {
            if (rank == 0) return -1;

            int currentRank = 1; 

            for (int i = 0; i < input.NumOfProvinces; i++)
            {
                int provinceSize = 1 + input.DistributorsPerProvince[i]; 

                if (rank >= currentRank && rank < currentRank + provinceSize)
                {
                    return i;
                }

                currentRank += provinceSize;
            }

            return -1;
        }

        public static int GetProvinceLeaderRank(int provinceIndex, InputData input)
        {
            int currentRank = 1; 

            for (int i = 0; i < provinceIndex; i++)
            {
                currentRank += 1 + input.DistributorsPerProvince[i];
            }

            return currentRank;
        }

        public static int GetTotalRanksNeeded(InputData input)
        {
            int totalRanks = 1; 

            for (int i = 0; i < input.NumOfProvinces; i++)
            {
                totalRanks += 1 + input.DistributorsPerProvince[i]; 
            }

            return totalRanks;
        }

        public static void PrintRankAssignments(InputData input)
        {
            Console.WriteLine("\n=== RANK ASSIGNMENT STRUCTURE ===");
            Console.WriteLine("Rank 0: Master Process");

            int currentRank = 1;
            for (int i = 0; i < input.NumOfProvinces; i++)
            {
                Console.WriteLine($"\nProvince {i}:");
                Console.WriteLine($"  Rank {currentRank}: Province {i} Leader");

                for (int j = 1; j <= input.DistributorsPerProvince[i]; j++)
                {
                    Console.WriteLine($"  Rank {currentRank + j}: Distributor {j} in Province {i}");
                }

                currentRank += 1 + input.DistributorsPerProvince[i];
            }
            Console.WriteLine("=====================================\n");
        }
    }
}