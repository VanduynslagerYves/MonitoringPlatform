using System.ComponentModel;
using System.Configuration.Install;
using System.ServiceProcess;

namespace WindowsMonitor.Install
{
    [RunInstaller(true)]
    public class MonitorInstaller : Installer
    {
        private ServiceProcessInstaller processInstaller;
        private ServiceInstaller serviceInstaller;

        public MonitorInstaller()
        {
            processInstaller = new ServiceProcessInstaller();
            serviceInstaller = new ServiceInstaller();

            // The service runs under the system account
            processInstaller.Account = ServiceAccount.LocalSystem;

            // The service is started automatically
            serviceInstaller.StartType = ServiceStartMode.Automatic;

            // ServiceName must equal those on ServiceBase derived classes
            serviceInstaller.ServiceName = "WindowsMonitor";
            // Set the service description
            serviceInstaller.Description = "Kata Windows Monitor Service";

            // Add installers to collection. Order is not important
            Installers.Add(serviceInstaller);
            Installers.Add(processInstaller);
        }

        //Start the service after installing
        public override void Install(System.Collections.IDictionary stateSaver)
        {
            base.Install(stateSaver);
            using (ServiceController sc = new ServiceController("WindowsMonitor"))
            {
                sc.Start();
            }
        }
    }
}
