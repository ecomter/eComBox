using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

//https://go.microsoft.com/fwlink/?LinkId=234236 上介绍了“用户控件”项模板

namespace eComBox.Views
{
    public sealed partial class CustomDialog : UserControl
    {
        public event EventHandler<bool> DialogResult;

        public CustomDialog()
        {
            this.InitializeComponent();
        }

        private void PrimaryButton_Click(object sender, RoutedEventArgs e)
        {
            // 用户点击“确定”，传递结果并关闭对话框
            DialogResult?.Invoke(this, true);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // 用户点击“取消”，传递结果并关闭对话框
            DialogResult?.Invoke(this, false);
        }
    }
}
