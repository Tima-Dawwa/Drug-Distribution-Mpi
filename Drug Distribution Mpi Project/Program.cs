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
                    Console.WriteLine("=== STARTING PARALLEL DRUG DISTRIBUTION SYSTEM ===");
                    Console.WriteLine($"Total MPI Processes: {size}");

                    string projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\.."));
                    string runnerFolder = Path.Combine(projectRoot, "Runner");
                    string inputFile = Path.Combine(runnerFolder, "input_data.txt");

                    if (File.Exists(inputFile))
                    {
                        input = Input.LoadFromTextFile();
                        // تيما لا تمسحي هاد الكومنت
                        //try
                        //{
                        //    File.Delete(inputFile);
                        //}
                        //catch (Exception ex)
                        //{
                        //    Console.WriteLine($"could not delete {inputFile}: {ex.Message}");
                        //}
                    }
                    else
                    {
                        input = Input.GetInput();
                        Input.SaveToTextFile(input);
                        Console.WriteLine("✅ Input collected interactively and saved");
                    }

                    // Print input summary
                    Console.WriteLine("\n=== INPUT SUMMARY ===");
                    Console.WriteLine($"Number of Provinces: {input.NumOfProvinces}");
                    for (int i = 0; i < input.NumOfProvinces; i++)
                    {
                        int totalOrders = input.PharmaciesPerProvince[i] + input.ClinicsPerProvince[i] + input.HospitalsPerProvince[i];
                        Console.WriteLine($"Province {i}: {totalOrders} orders, {input.DistributorsPerProvince[i]} distributors");
                    }
                    Console.WriteLine($"Average Delivery Time: {input.AvgDeliveryTime} seconds");
                    Console.WriteLine("====================\n");

                    // Validate process count
                    int requiredProcesses = RoleAssignHelper.GetTotalRanksNeeded(input);
                    if (size != requiredProcesses)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"❌ ERROR: Process count mismatch!");
                        Console.WriteLine($"   Required: {requiredProcesses} processes");
                        Console.WriteLine($"   Actual: {size} processes");
                        Console.ResetColor();
                        return;
                    }

                    // Print rank assignments
                    RoleAssignHelper.PrintRankAssignments(input);

                    // Send input to all other processes
                    Console.WriteLine("Sending input data to all processes...");
                    for (int i = 1; i < size; i++)
                    {
                        comm.Send(input, i, 0);
                     
                    }
                    // FIRST BARRIER: Wait for all processes to receive input data
                    Console.WriteLine("Master waiting for all processes to receive input data...");

                    // SECOND BARRIER: Wait for all processes to complete role assignment and initialization
                    Console.WriteLine("Master waiting for all processes to complete role assignment...");

                    Console.WriteLine("All processes synchronized. Master starting coordination...\n");
                    Master.Run(comm, input);

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("\nMASTER PROCESS COMPLETED SUCCESSFULLY!");
                    Console.ResetColor();
                }
                else
                {
                    // Receive input from master
                    input = comm.Receive<InputData>(0, 0);

                    // FIRST BARRIER: Confirm input data received
                    comm.Barrier();

                    // Assign role and run
                    RoleAssignHelper.AssignAndRun(comm, rank, input);

                    // SECOND BARRIER: Confirm role assignment complete
                    comm.Barrier();
                }
            }
        }
    }
}