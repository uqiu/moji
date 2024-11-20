using System.Configuration;
using System.Data;
using System.Windows;
using System.Windows.Forms;  // 添加 Windows Forms 引用
using WinFormsApplication = System.Windows.Forms.Application;
using WpfApplication = System.Windows.Application;

namespace moji
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : WpfApplication  // 明确使用 WPF 的 Application
    {
        protected override void OnExit(ExitEventArgs e)
        {
            // 修正获取 MainWindow 的方式
            if (Current.MainWindow is MainWindow mainWindow && mainWindow.notifyIcon != null)
            {
                mainWindow.notifyIcon.Dispose();
            }
            base.OnExit(e);
        }
    }

}
