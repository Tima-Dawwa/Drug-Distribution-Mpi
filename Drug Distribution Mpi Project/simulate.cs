using MPI;
using System;

namespace Drug_Distribution_Mpi_Project
{
    public static class Simulation
    {
        public static void AssignRole(Intracommunicator worldComm, int rank, InputData input)
        {
            int totalProvinces = input.NumOfProvinces;
            int ranksPerProvince = input.DistributorsPerProvince + 1; //the additional 1 is for the leader

            int provinceIndex = (rank - 1) / ranksPerProvince; //the province number which this rank belongs
            if (provinceIndex >= totalProvinces)
            {
                Console.WriteLine($"[Rank {rank}] have no role");
                return;
            }

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[Rank {rank}] → Belongs to province number {provinceIndex}");
            Console.ResetColor();

            int color = provinceIndex;
            Intracommunicator provinceComm = (Intracommunicator)worldComm.Split(color, rank); //split is for creating a sub-communicator for each province

            int localRank = provinceComm.Rank; 
            if (localRank == 0) // head of province is 0
            {
                Province.RunAsLeader(provinceIndex, provinceComm, worldComm, input);
            }
            else
            {
                //Distributor 
            }
        }
    }
}
