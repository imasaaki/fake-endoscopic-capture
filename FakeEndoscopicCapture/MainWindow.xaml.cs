using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Threading;

using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace FakeEndoscopicCapture
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        private bool isExit = false;
        private volatile bool doFreezeOnTime = false;
        private Timer freezeTimer;
        private Object lockObject = new Object();

        public MainWindow()
        {
            InitializeComponent();

            this.WindowStyle = WindowStyle.None;
            this.WindowState = WindowState.Maximized;

            this.freezeTimer = new Timer(this.FreezeTimerCallback, null, Timeout.Infinite, Timeout.Infinite);
        }

        private void Button_Close_Click(object sender, RoutedEventArgs e)
        {
            this.ShutdownApplication();
        }

        private void ShutdownApplication()
        {
            this.freezeTimer.Dispose();

            lock (this.lockObject)
            {
                this.isExit = true;
            }          
        }

        private void Capture()
        {
            var camera = new VideoCapture(Properties.Settings.Default.cameraNumber)
            {
                FrameWidth = Properties.Settings.Default.captureWidth,
                FrameHeight = Properties.Settings.Default.captureHeight,
                Fps = Properties.Settings.Default.captureFps
            };

            using (var img = new Mat())
            using (camera)
            {
                while (true)
                {
                    camera.Read(img);

                    if (img.Empty())
                    {
                        throw new Exception("no camera found");
                    }

                    if (this.doFreezeOnTime)
                    {
                        continue;
                    }

                    lock (this.lockObject)
                    {
                        if (this.isExit)
                        {
                            break;
                        }

                        this.Dispatcher.Invoke(new Action(() =>
                        {
                            this.Image_Main.Source = img.ToWriteableBitmap();
                        }), null);
                    }
                }
            }
        }

        private void Image_Main_MouseDown(object sender, MouseButtonEventArgs e)
        {
            this.doFreezeOnTime = true;
            this.freezeTimer.Change(1500, 1500);
        }

        private void FreezeTimerCallback(object sender)
        {
            this.doFreezeOnTime = false;
            this.freezeTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private async void Button_Start_Click(object sender, RoutedEventArgs e)
        {
            this.Button_Start.IsEnabled = false;

            double gridWidth = this.Grid_Image.ActualWidth;
            double gridHeight = this.Grid_Image.ActualHeight;

            double displayImageWidth = Properties.Settings.Default.displayImageWidth;
            double displayImageHeight = Properties.Settings.Default.displayImageHeight;
            double displayImageCenterX = Properties.Settings.Default.displayImageCenterX;
            double displayImageCenterY = Properties.Settings.Default.displayImageCenterY;

            double topMargin = Math.Max(0, displayImageCenterY - (displayImageHeight / 2));
            double bottomMargin = Math.Max(0, gridHeight - displayImageHeight - topMargin);

            double leftMargin = Math.Max(0, displayImageCenterX - (displayImageWidth / 2));
            double rightMargin = Math.Max(0, gridWidth - displayImageWidth - leftMargin);

            this.Grid_Image.ColumnDefinitions[0].Width = new GridLength(leftMargin);
            this.Grid_Image.ColumnDefinitions[2].Width = new GridLength(rightMargin);

            this.Grid_Image.RowDefinitions[0].Height = new GridLength(topMargin);
            this.Grid_Image.RowDefinitions[2].Height = new GridLength(bottomMargin);

            await Task.Run(this.Capture);

            this.Image_Main.Source = null;
            this.Close();
        }
    }
}
