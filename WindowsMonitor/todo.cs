/*Hieronder "niet-veranderlijke values": oplossing zoeken zodat deze niet constant verstuurd worden.
using WindowsMonitor.Service;

SystemInfo.IP,
SystemInfo.OperatingSystem,
SystemInfo.HostName,

 public static string GetMachineGuid()
{
    string machineGuid = string.Empty;
    try
    {
        machineGuid = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography").GetValue("MachineGuid").ToString();
    }
    catch (Exception ex)
    {
        Console.WriteLine("Error retrieving Machine GUID: " + ex.Message);
    }
    return machineGuid;
}*/

//bij starten van service alles uitlezen en verzenden, daarna enkel wijzigingen doorsturen.
//Laatste record (enkel username, ip, os, hostnaam) hier bewaren. indien volgende record wijzigingen heeft, die wijziging mee verzenden: backend beslist dan wat er moet gebeuren (update)
//of zoals hieronder met watchers (beter, minder info uit te lezen)
/*
 * private static ManagementEventWatcher watcher;

public static void Main()
{
    // Set up WMI query for IP address change events
    var query = new WqlEventQuery("SELECT * FROM __InstanceModificationEvent WITHIN 10 WHERE TargetInstance ISA 'Win32_NetworkAdapterConfiguration'");

    // Initialize the event watcher
    watcher = new ManagementEventWatcher(query);
    watcher.EventArrived += OnIpAddressChanged;
    watcher.Start();

    Console.WriteLine("Monitoring IP address changes. Press any key to exit...");
    Console.ReadKey();

    watcher.Stop();
}

private static void OnIpAddressChanged(object sender, EventArrivedEventArgs e)
{
    Console.WriteLine("IP address change detected!");

    // You can handle the IP address change here
    // For example, retrieve the new IP address
    var networkConfig = new ManagementObject(e.NewEvent["TargetInstance"] as ManagementBaseObject);
    var ipAddress = networkConfig["IPAddress"] as string[];

    if (ipAddress != null && ipAddress.Length > 0)
    {
        Console.WriteLine("New IP Address: " + ipAddress[0]);
        // Perform actions based on the new IP address
    }
}
 */

/* voorbeeldschema voor nosql
 *
 *{
    "hostname": "katalystpc",
    "ip_address": "192.168.1.100",
    "os": "windows home",
    "username: "yves",
    "data": [
        {
            "timestamp": ISODate("2024-06-20T10:00:00Z"),
            "cpuload": 10
        },
        {
            "timestamp": ISODate("2024-06-20T10:05:00Z"),
            "cpuload": 15
        },
        {
            "timestamp": ISODate("2024-06-20T10:10:00Z"),
            "cpuload": 12
        }
    ]
  }
 */