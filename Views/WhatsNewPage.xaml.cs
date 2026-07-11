using eComBox.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace eComBox.Views
{
    public sealed partial class WhatsNewPage : Page
    {
        public WhatsNewPage()
        {
            InitializeComponent();
        }

        private void DoneButton_Click(object sender, RoutedEventArgs e)
        {
            if (!NavigationService.GoBack())
            {
                NavigationService.Navigate(typeof(HomePage));
            }
        }
    }
}
