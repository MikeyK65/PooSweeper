using Microsoft.Extensions.DependencyInjection;

namespace PooSweep
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[UnhandledException] IsTerminating={e.IsTerminating} Exception={e.ExceptionObject}");
                }
                catch { }
            };

            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[UnobservedTaskException] Exception={e.Exception}");
                }
                catch { }
            };
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }
}