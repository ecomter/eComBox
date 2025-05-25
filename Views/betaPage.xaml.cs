using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls.Primitives;

namespace eComBox.Views
{
    public sealed partial class betaPage : Page, INotifyPropertyChanged
    {
        public betaPage()
        {
            InitializeComponent();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void Set<T>(ref T storage, T value, [CallerMemberName]string propertyName = null)
        {
            if (Equals(storage, value))
            {
                return;
            }

            storage = value;
            OnPropertyChanged(propertyName);
        }

        private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private async void settings(object sender, RoutedEventArgs e)
        {

            customDialog.DialogResult += CustomDialog_DialogResult;
            customPopup.IsOpen = true;

        }
        private void CustomDialog_DialogResult(object sender, bool e)
        {
            // 根据用户选择执行相应操作
            if (e)
            {
                // 用户点击“确定”
            }
            else
            {
                // 用户点击“取消”
            }

            // 关闭 Popup
            customPopup.IsOpen = false;
            customDialog.DialogResult -= CustomDialog_DialogResult;
        }
    }
}
