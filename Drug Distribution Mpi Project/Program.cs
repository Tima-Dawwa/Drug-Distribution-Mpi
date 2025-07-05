using Drug_Distribution_Mpi_Project.Helper;
using MPI;
using System;
using System.Diagnostics;
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
                    var stopwatch = new Stopwatch();
                    stopwatch.Start();

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
                        Console.WriteLine("Input collected interactively and saved");
                    }

                    Console.WriteLine("\n=== INPUT SUMMARY ===");
                    Console.WriteLine($"Number of Provinces: {input.NumOfProvinces}");

                    // Calculate total orders across all provinces
                    int totalOrders = 0;
                    for (int i = 0; i < input.NumOfProvinces; i++)
                    {
                        int provinceOrders = input.PharmaciesPerProvince[i] + input.ClinicsPerProvince[i] + input.HospitalsPerProvince[i];
                        totalOrders += provinceOrders;
                        Console.WriteLine($"Province {i}: {provinceOrders} orders, {input.DistributorsPerProvince[i]} distributors");
                    }
                    Console.WriteLine($"Average Delivery Time: {input.AvgDeliveryTime} seconds");
                    Console.WriteLine("====================\n");

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

                    RoleAssignHelper.PrintRankAssignments(input);

                    Console.WriteLine("Sending input data to all processes...");
                    for (int i = 1; i < size; i++)
                    {
                        comm.Send(input, i, 0);
                    }
                    Console.WriteLine("Master waiting for all processes to receive input data...\n");
                    comm.Barrier();

                    RoleAssignHelper.AssignAndRun(comm, rank, input);
                    comm.Barrier();

                    Console.WriteLine("\nAll processes synchronized. Master starting coordination...\n");
                    stopwatch.Stop();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("\nMASTER PROCESS COMPLETED SUCCESSFULLY!");
                    Console.WriteLine($"⏱ Total Parallel Execution Time: {stopwatch.Elapsed.TotalSeconds:F4} seconds");
                    Console.ResetColor();

                    // Calculate sequential time using the total orders
                    double sequentialTime = totalOrders * input.AvgDeliveryTime;
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"\n🕒 Estimated Sequential Execution Time: {sequentialTime:F4} seconds");
                    Console.ResetColor();
                }
                else
                {
                    input = comm.Receive<InputData>(0, 0);
                    comm.Barrier();

                    RoleAssignHelper.AssignAndRun(comm, rank, input);
                    comm.Barrier();
                }
            }
        }
    }
}