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
                InputData input;
                int rank = comm.Rank;
                int size = comm.Size;

                if (rank == 0)
                {
                    Console.WriteLine("🚀 Starting a parallel drug distribution system using MPI...");
                    input = Input.Read();
                    for (int i = 1; i < size; i++)
                    {
                        comm.Send(input, i, 0);
                    }
                }
                else
                {
                    _ = comm.Receive<InputData>(0, 0);
                }
            }
        }
    }
}
