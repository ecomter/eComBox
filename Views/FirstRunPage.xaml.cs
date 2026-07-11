using eComBox.Helpers;
using eComBox.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace eComBox.Views
{
    public sealed partial class FirstRunPage : Page
    {
        private int _step;

        public FirstRunPage()
        {
            InitializeComponent();
        }

        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            if (_step == 0)
            {
                ShowReadyStep();
                return;
            }

            if (!NavigationService.GoBack())
            {
                NavigationService.Navigate(typeof(HomePage));
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e) => ShowWelcomeStep();

        private void ShowReadyStep()
        {
            _step = 1;
            WelcomeStep.Visibility = Visibility.Collapsed;
            ReadyStep.Visibility = Visibility.Visible;
            BackButton.Visibility = Visibility.Visible;
            ContinueButton.Content = "Oobe_Start".GetLocalized();
            SwapStepIndicator();
        }

        private void ShowWelcomeStep()
        {
            _step = 0;
            ReadyStep.Visibility = Visibility.Collapsed;
            WelcomeStep.Visibility = Visibility.Visible;
            BackButton.Visibility = Visibility.Collapsed;
            ContinueButton.Content = "Oobe_Continue".GetLocalized();
            SwapStepIndicator();
        }

        private void SwapStepIndicator()
        {
            var width = StepOneDot.Width;
            StepOneDot.Width = StepTwoDot.Width;
            StepTwoDot.Width = width;
            var fill = StepOneDot.Fill;
            StepOneDot.Fill = StepTwoDot.Fill;
            StepTwoDot.Fill = fill;
        }
    }
}
