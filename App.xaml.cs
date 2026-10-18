using System.Windows;

namespace PesPluginLoaderInstaller
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            EmbeddedResourceManager.Install();
        }
    }
}
