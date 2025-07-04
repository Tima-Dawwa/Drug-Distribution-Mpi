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

            Intracommunicator provinceComm = (Intracommunicator)worldComm.Split(provinceIndex, rank);

            int localRank = provinceComm.Rank;

            if (localRank == 0)
            {
                Province.RunAsLeader(provinceIndex, provinceComm, input);
            }
            else
            {
                Distributor.Run(provinceIndex, provinceComm, input);

            }
        }
        static int GetProvinceIndex(int rank, InputData input)
        {
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
            throw new Exception($"The rank {rank} does not belong to any province!");
        }
    }
}
