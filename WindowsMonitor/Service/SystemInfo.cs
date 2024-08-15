using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;

namespace WindowsMonitor.Service
{
    public static class SystemInfo
    {
        //TODO: voor elke info categorie hier een eigen searcher definiëren met query.
        private static ManagementObjectSearcher _searcher = new ManagementObjectSearcher();
        private static ManagementScope _scope = new ManagementScope("root\\CIMV2");
        private static ObjectQuery _query = new ObjectQuery();

        public static TimeSpan UpTime
        {
            get
            {
                using (var uptime = new PerformanceCounter("System", "System Up Time"))
                {
                    uptime.NextValue(); //Call this an extra time before reading its value
                    return TimeSpan.FromSeconds(uptime.NextValue());
                }
            }
        }

        public static string HostName
        {
            get
            {
                try
                {
                    return Dns.GetHostName();
                }
                catch (Exception)
                {
                    throw new Exception("Error while reading hostname");
                }
            }
        }

        public static string UserName
        {
            get
            {
                try
                {
                    string query = "SELECT UserName FROM Win32_ComputerSystem";
                    using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(query))
                    using (ManagementObjectCollection results = searcher.Get())
                    {
                        foreach (ManagementObject result in results)
                        {
                            string userName = result["UserName"] as string;
                            if (!string.IsNullOrEmpty(userName))
                            {
                                return userName;
                            }
                        }
                    }

                    return string.Empty;
                }
                catch (Exception)
                {
                    throw new Exception("Error while reading username");
                }
            }
        }

        public static string OperatingSystem
        {
            get
            {
                var nameObj = (from x in new ManagementObjectSearcher("SELECT Caption FROM Win32_OperatingSystem").Get().Cast<ManagementObject>()
                               select x.GetPropertyValue("Caption")).FirstOrDefault();

                var name = (nameObj != null) ? nameObj.ToString() : "Unknown";
                return name ?? string.Empty;
            }
        }

        public static double TotalMemory
        {
            get
            {
                ObjectQuery wql = new ObjectQuery("SELECT * FROM Win32_OperatingSystem");
                ManagementObjectSearcher searcher = new ManagementObjectSearcher(wql);
                ManagementObjectCollection results = searcher.Get();

                double totalMemory = 0;
                foreach (ManagementObject result in results)
                {
                    totalMemory = Int32.Parse(result["TotalVisibleMemorySize"].ToString());
                    /*Console.WriteLine("Total Visible Memory: {0} KB", result["TotalVisibleMemorySize"]);
                    Console.WriteLine("Free Physical Memory: {0} KB", result["FreePhysicalMemory"]);*/
                }

                return totalMemory / 1000000;
            }
        }

        public static double FreeMemory
        {
            get
            {
                ObjectQuery wql = new ObjectQuery("SELECT * FROM Win32_OperatingSystem");
                ManagementObjectSearcher searcher = new ManagementObjectSearcher(wql);
                ManagementObjectCollection results = searcher.Get();

                double totalMemory = 0;
                foreach (ManagementObject result in results)
                {
                    totalMemory = Int32.Parse(result["FreePhysicalMemory"].ToString());
                }

                return totalMemory / 1000000;
            }
        }

        public static double UsedMemory
        {
            get
            {
                return TotalMemory - FreeMemory;
            }
        }

        public static DriveInfo[] Disks
        {
            get
            {
                return DriveInfo.GetDrives();
            }
        }

        public static double CPU
        {
            get
            {
                ManagementObjectSearcher objMOS = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM  Win32_Processor");
                ManagementObjectCollection results = objMOS.Get();

                var cpu = 0.0;
                //string cpu = string.Empty;
                foreach (var result in results)
                {
                    if (double.TryParse(result["Name"].ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsedResult))
                    {
                        cpu = parsedResult;
                    }
                }

                return cpu;
            }
        }

        public static string CPULoad
        {
            get
            {

                _searcher.Scope = _scope;
                _query.QueryString = "SELECT LoadPercentage FROM Win32_Processor";
                _searcher.Query = _query;

                //ManagementObjectSearcher objMOS = new("root\\CIMV2", "SELECT LoadPercentage FROM  Win32_Processor");
                var results = _searcher.Get();

                string cpu = string.Empty;
                foreach (var result in results)
                {
                    cpu = result.GetPropertyValue("LoadPercentage").ToString() ?? string.Empty;
                }

                return cpu;
            }
        }

        public static string CPUArchitecture
        {
            get
            {
                ManagementObjectSearcher objMOS = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM  Win32_OperatingSystem");
                ManagementObjectCollection results = objMOS.Get();

                string cpuarch = "";
                foreach (ManagementObject result in results)
                {
                    cpuarch = result["OSArchitecture"].ToString();
                }

                return cpuarch;
            }
        }

        public static string IP
        {
            get
            {
                string hostName = Dns.GetHostName(); // Retrive the Name of HOST

                // Get the IP
                var clientAddressList = Dns.GetHostEntry(hostName).AddressList.Where(x => x.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6).ToList();
                return clientAddressList[0].ToString();
            }
        }

        public static DateTime TimeStamp
        {
            get
            {
                var now = DateTime.Now;
                return new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second);
            }
        }
    }


}
