using MPI;
using System;
using System.Threading;
using System.Collections.Generic;

namespace Drug_Distribution_Mpi_Project
{
    public static class Province
    {
        public static void RunAsLeader(int provinceIndex, Intracommunicator provinceComm, InputData input)
        {
            int rank = provinceComm.Rank;
            int size = provinceComm.Size;

            int numDistributors = input.DistributorsPerProvince[provinceIndex];
            int totalOrders = input.PharmaciesPerProvince[provinceIndex]; 

            Queue<int> distributorQueue = new Queue<int>();
            bool[] distributorStatus = new bool[numDistributors]; //busy distributors = true , available distributors = false

            for (int i = 1; i < size; i++) //initialize all as available
                distributorQueue.Enqueue(i);

            int nextOrderId = 1;
            int finishedOrders = 0;

            Console.WriteLine($"[Province {provinceIndex} | Rank {rank}] started distributing. Total Orders: {totalOrders}");

            while (finishedOrders < totalOrders)
            {
                //if there are available distributors, give them requests
                while (distributorQueue.Count > 0 && nextOrderId <= totalOrders)
                {
                    int distributor = distributorQueue.Dequeue();
                    distributorStatus[distributor - 1] = true;

                    provinceComm.Send(nextOrderId, distributor, 0); 
                    Console.WriteLine($"[Province {provinceIndex}] Sent Order {nextOrderId} to Distributor {distributor}");
                    nextOrderId++;
                }

                //checks if there is a message comming from any distributor with the tag "1"
                //if there is, it will return a the state of it
                //if not, the state will be null
                Status status = provinceComm.ImmediateProbe(MPI.Unsafe.MPI_ANY_SOURCE, 1);
                if (status != null)
                {
                    int finishedDistributor = status.Source;
                    provinceComm.Receive<int>(finishedDistributor, 1); 
                    distributorStatus[finishedDistributor - 1] = false;
                    distributorQueue.Enqueue(finishedDistributor);
                    finishedOrders++;

                    Console.WriteLine($"[Province {provinceIndex}] Distributor {finishedDistributor} finished. Orders left: {totalOrders - finishedOrders}");
                }

                Thread.Sleep(10); 
            }

            for (int i = 1; i < numDistributors ; i++)
            {
                provinceComm.Send(-1, i, 0);
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[Province {provinceIndex}] has finished distributing all orders ✅");
            Console.ResetColor();
        }
    }
}
