using Caliburn.Micro;
using CsvHelper;
using MahApps.Metro.Controls;
using SuperSocket.SocketBase;
using SuperSocket.SocketBase.Protocol;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows;
using System.Windows.Media;

namespace QRCodeScanner
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        public MainWindow()
        {
            InitializeComponent();

            var model = new MainWindowViewModel();
            model.Owner = this;
            DataContext = model;
        }
    }

    public class BaseCode
    {
        /// <summary>
        /// 记录时间
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// 设备名
        /// </summary>
        public string DeviceName { get; set; }

        /// <summary>
        /// 字符码
        /// </summary>
        public string Code { get; set; }

        public BaseCode()
        {
            StartTime = DateTime.Now;
        }

        public BaseCode(string deviceName, string code) : this()
        {
            DeviceName = deviceName;
            Code = code;
        }

    }


    /// <summary>
    /// 主窗口显示模型
    /// </summary>
    public class MainWindowViewModel : Screen
    {
        public Window Owner { get; set; }

        //服务器实例
        private AppServer appServer = new AppServer();

        public MainWindowViewModel()
        {
            //启动服务器
            if (!appServer.Setup(777))
            {
                Console.WriteLine("启动失败!");
                return;
            }

            if (!appServer.Start())
            {
                Console.WriteLine("启动失败");
                return;
            }
            Console.WriteLine("开始监听");

            //注册任务
            appServer.NewSessionConnected += AppServer_NewSessionConnected;
            appServer.SessionClosed += AppServer_SessionClosed;
            appServer.NewRequestReceived += AppServer_NewRequestReceived;

            Host = "本机IP地址: ";
            var addr = Dns.GetHostAddresses(Dns.GetHostName());
            foreach (var item in addr)
            {
                if (!item.ToString().Contains("%"))
                {
                    Host += item.ToString() + "  ";
                }
            }

        }

        private string _host;

        /// <summary>
        /// 本机地址
        /// </summary>
        public string Host
        {
            get { return _host; }
            set { _host = value; NotifyOfPropertyChange(() => Host); }
        }


        private ObservableCollection<BaseCode> _scannedCode = new ObservableCollection<BaseCode>();

        public ObservableCollection<BaseCode> ScannedCode
        {
            get { return _scannedCode; }
            set { _scannedCode = value; NotifyOfPropertyChange(() => ScannedCode); }
        }

        private string _deviceName = "未连接";

        /// <summary>
        /// 设备名
        /// </summary>
        public string DeviceName
        {
            get { return _deviceName; }
            set { _deviceName = value; NotifyOfPropertyChange(() => DeviceName); }
        }

        private SolidColorBrush _deviceStateColor = Brushes.DarkGray;

        /// <summary>
        /// 设备状态颜色
        /// </summary>
        public SolidColorBrush DeviceStateColor
        {
            get { return _deviceStateColor; }
            set { _deviceStateColor = value; NotifyOfPropertyChange(() => DeviceStateColor); }
        }

        private string _filePath = @"C:\Users\Public\Documents\decode.csv";

        /// <summary>
        /// 文件路径
        /// </summary>
        public string FilePath
        {
            get { return _filePath; }
            set { _filePath = value; NotifyOfPropertyChange(() => FilePath); }
        }

        public void SelectSaveFilePath()
        {
            //创建一个保存文件式的对话框  
            var sfd = new Microsoft.Win32.SaveFileDialog();

            //设置保存的文件的类型，注意过滤器的语法  
            sfd.Filter = "csv file|*.csv";
            sfd.FileName = "decode";

            //调用ShowDialog()方法显示该对话框，该方法的返回值代表用户是否点击了确定按钮  
            if (sfd.ShowDialog() == true)
            {
                //此处做你想做的事 ...=sfd.FileName; 
                FilePath = sfd.FileName;
            }
        }

        private void AppServer_SessionClosed(AppSession session, CloseReason value)
        {
            DeviceStateColor = Brushes.DarkGray;
            DeviceName = "未连接";
            Console.WriteLine($"{session.RemoteEndPoint.ToString()} 已断开(关闭原因:{value})");
        }

        private void AppServer_NewSessionConnected(AppSession session)
        {
            DeviceStateColor = Brushes.Green;
            DeviceName = session.RemoteEndPoint.ToString();
            Console.WriteLine($"{session.RemoteEndPoint.ToString()} 已连接");

        }

        private void AppServer_NewRequestReceived(AppSession session, StringRequestInfo requestInfo)
        {
            //DeviceName = requestInfo.Key;
            var code = new BaseCode(requestInfo.Key, requestInfo.Body);

            new Thread(delegate ()
            {
                ThreadPool.QueueUserWorkItem(delegate
                {
                    System.Threading.SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(System.Windows.Application.Current.Dispatcher));
                    System.Threading.SynchronizationContext.Current.Send(pl =>
                    {
                        //执行相关任务.....
                        ScannedCode.Insert(0, code);

                        var fileInfo = new FileInfo(FilePath);
                        if (!fileInfo.Directory.Exists)
                        {
                            try
                            {
                                fileInfo.Directory.Create();
                            }
                            catch (DirectoryNotFoundException)
                            {
                                MessageBox.Show(Owner, "路径无效,无法创建相关文件!");
                                return;
                            }
                        }

                        //写入本地
                        try
                        {
                            using (var writer = new StreamWriter(FilePath))
                            {
                                using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                                {
                                    csv.WriteRecords(ScannedCode);
                                }
                            }
                        }
                        catch (UnauthorizedAccessException)
                        {
                            MessageBox.Show(Owner, "无权限写入文件,请以管理员权限运行!");
                        }
                        catch (System.IO.IOException)
                        {
                            MessageBox.Show(Owner, "文件被占用,无法写入!");
                        }

                    }, null);
                });
            }).Start();

            session.Send("OK\r\n");
            Console.WriteLine($"收到{session.RemoteEndPoint.ToString()}: {requestInfo.Key} {requestInfo.Body}");
        }

    }
}
