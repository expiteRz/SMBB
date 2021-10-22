using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace SMBB
{
    /// <summary>
    /// App.xaml の相互作用ロジック
    /// </summary>
    public partial class App : Application
    {
        private static readonly Mutex mutex = new Mutex(false, GetMutexString());
        //SMBBが同じディレクトリで複数起動できないようにディレクトリのハッシュからmutex生成
        private static bool hasHandle = false;
        private static string GetMutexString()
        {
            byte[] data = Encoding.UTF8.GetBytes(AppDomain.CurrentDomain.BaseDirectory);
            var md5 = new MD5CryptoServiceProvider();
            byte[] dest = md5.ComputeHash(data);
            md5.Clear();
            var result = new StringBuilder();
            foreach (byte b in dest)
            {
                result.Append(b.ToString("X2"));
            }
            return "SMBB" + result.ToString();
        }
        protected override void OnStartup(StartupEventArgs e)
        {
            // Mutexの所有権を取得
            hasHandle = mutex.WaitOne(0, false);
            // 取得できなければ多重起動
            if (!hasHandle)
            {
                MessageBox.Show("すでに起動しています", "", MessageBoxButton.OK, MessageBoxImage.Information);
                this.Shutdown();
                return;
            }
            base.OnStartup(e);
        }
        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);
            if (hasHandle) mutex.ReleaseMutex();
            mutex.Close();
        }
        private void ApplicationStartup(object sender, StartupEventArgs e)
        {
            var windows = new MainWindow();
            windows.Show();
        }
    }

}
