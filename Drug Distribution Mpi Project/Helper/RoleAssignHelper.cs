using System;
using MPI;

namespace Drug_Distribution_Mpi_Project.Helper
{
    class RoleAssignHelper
    {
        public static void AssignAndRun(Intracommunicator worldComm, int rank, InputData input)
        {
            int totalProvinces = input.NumOfProvinces;
            int ranksPerProvince = input.DistributorsPerProvince + 1; 

            int provinceIndex = (rank - 1) / ranksPerProvince;

            if (provinceIndex < 0 || provinceIndex >= totalProvinces)
            {
                Console.WriteLine($"[Rank {rank}] has no assigned role.");
                return;
            }

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[Rank {rank}] → Belongs to province number {provinceIndex}");
            Console.ResetColor();

            Intracommunicator provinceComm = (Intracommunicator)worldComm.Split(provinceIndex, rank);

            int localRank = provinceComm.Rank;

            if (localRank == 0)
            {
                Province.RunAsLeader(provinceIndex, provinceComm, worldComm, input);
            }
            else
            {
                Distributor.Run(provinceIndex, provinceComm, input);

            }
        }
    }
}
