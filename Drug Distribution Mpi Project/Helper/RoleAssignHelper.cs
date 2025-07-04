using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MPI;


namespace Drug_Distribution_Mpi_Project.Helper
{
    class RoleAssignHelper
    {
        public static void AssignAndRun(Intracommunicator world, int rank, InputData input)
        {
            int numProvinces = input.NumOfProvinces;
            if (rank <= numProvinces)
            {
                //Province.Run(world, rank, input);
            }
            else
            {
                //Distributor.Run(world, rank, input);
            }
        }
    }
}
