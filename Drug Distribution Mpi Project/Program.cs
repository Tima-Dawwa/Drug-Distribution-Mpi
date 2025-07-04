using Drug_Distribution_Mpi_Project.Helper;
using MPI;
using System;
using System.IO;

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
                string inputPath = Path.Combine("Runner", "input_data.txt");

                if (rank == 0)
                {
                    string projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\.."));
                    string runnerFolder = Path.Combine(projectRoot, "Runner");
                    string inputFile = Path.Combine(runnerFolder, "input_data.txt");

                    if (File.Exists(inputFile))
                    {
                        input = Input.LoadFromTextFile();
                        try
                        {
                            File.Delete(inputFile);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"could not delete {inputFile}: {ex.Message}");
                        }
                    }
                    else
                    {
                        input = Input.GetInput();
                        Input.SaveToTextFile(input);
                    }

                    for (int i = 1; i < size; i++)
                    {
                        comm.Send(input, i, 0);
                    }

                    Master.Run(comm, input);

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
