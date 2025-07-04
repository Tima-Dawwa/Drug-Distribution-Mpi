using Drug_Distribution_Mpi_Project.Helper;
using MPI;
using System;


namespace Drug_Distribution_Mpi_Project
{
    class Program
    {
        static void Main(string[] args)
        {
            using (new MPI.Environment(ref args))
            {
                Intracommunicator comm = Communicator.world;
                int rank = comm.Rank;
                int size = comm.Size;
                InputData input;

                if (rank == 0)
                {
                    Console.WriteLine("Starting a parallel drug distribution system using MPI...");

                    Master.Run(comm, size);

                    Console.WriteLine("Master is ready (Rank 0)");
                }
                else
                {
                    input = comm.Receive<InputData>(0, 0);

                    RoleAssignHelper.AssignAndRun(comm, rank, input);
                }
            }
        }
    }
}
