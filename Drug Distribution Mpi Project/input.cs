using System;

namespace Drug_Distribution_Mpi_Project
{
    class Input
    {
        public static InputData Read()
        {
            var data = new InputData();

            Console.WriteLine("🔢 Enter the number of provinces:");
            data.NumOfProvinces = int.Parse(Console.ReadLine());

            Console.WriteLine("🏥 Enter the number of clinics for each province:");
            data.ClinicsPerProvince = int.Parse(Console.ReadLine());

            Console.WriteLine("💊 Enter the number of pharmacies for each province:");
            data.PharmaciesPerProvince = int.Parse(Console.ReadLine());

            Console.WriteLine("🚚 Enter the number of distributors for each province:");
            data.DistributorsPerProvince = int.Parse(Console.ReadLine());

            Console.WriteLine("⏱️ Enter the median time to distribute a single order:");
            data.AvgDeliveryTime = int.Parse(Console.ReadLine());

            return data;
        }
    }
    public class InputData
    {
        public int NumOfProvinces { get; set; }
        public int ClinicsPerProvince { get; set; }
        public int PharmaciesPerProvince { get; set; }
        public int DistributorsPerProvince { get; set; }
        public int AvgDeliveryTime { get; set; }
        public int[] OrdersPerProvince
        {
            get
            {
                int[] orders = new int[NumOfProvinces];
                int[] pharmacies = new int[PharmaciesPerProvince];
                int[] clinics = new int[ClinicsPerProvince];
                for (int i = 0; i < NumOfProvinces; i++)
                {
                    orders[i] = pharmacies[i] + clinics[i];
                }
                return orders;
            }
        }
    }
}
