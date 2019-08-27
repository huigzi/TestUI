using HPSocketCS;
using InteractiveDataDisplay.WPF;
using MathNet.Numerics.Data.Matlab;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Threading;

namespace TestUI
{
    public class FilePath : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private string _path;

        public string Path
        {

            get => _path;
            set
            {
                _path = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Path"));
            }

        }
    }

    public enum DataState
    {
        Started, Stopped
    }

    public enum DeviceState
    {
        Connected, DisConnected
    }

    public enum WorkStateEnum
    {
        DebugState, WorkState
    }


    public partial class MainWindow
    {
        private FilePath _filePath;
        private DataState _dataState;
        private DeviceState _deviceState;
        private WorkStateEnum _workState;
        private byte[] _channelInfo;
        private IntPtr _connectId;
        private int _itemcount = 1;
        private BinaryWriter _sw;
        private FileStream _filestream;
        private int _samFreq;
        private long _count;
        private long _count2;
        private string _filename;

        private readonly Algorithm _algorithm;
        private readonly List<LineGraph> _lineGraphs;
        private readonly TcpPackServer _server;

        private readonly byte[] _startCommand;
        private readonly byte[] _stopCommand;
        private readonly byte[] _paramReadyCommand;
        private readonly byte[] _alarmCommand;

        public ObservableCollection<IntPtr> ClinetList;

        private delegate void ShowMsg();
        private delegate void UpdateBytesDelegate(byte[] data);
        private delegate void UpdatePlot(byte[] xData);

        public MainWindow()
        {
            _startCommand = new byte[]
            {
                159, 25, 70, 136, 237, 238, 39, 247, 233, 118, 193, 35, 101, 218, 188, 227, 155, 9, 198, 217, 157, 175,
                171, 90, 149, 245, 251, 8
            };

            _stopCommand = new byte[]
            {
                175, 80, 237, 9, 109, 94, 188, 195, 46, 119, 108, 156, 170, 180, 66, 161, 154, 39, 28, 116, 222, 79,
                135, 52, 171, 58, 114, 157
            };

            _paramReadyCommand = new byte[]
            {
                85, 41, 149, 63, 25, 104, 210, 41, 52, 24, 197, 252, 112, 33, 62, 221, 16, 15, 66, 114, 172, 66, 178,
                142, 11, 42, 150, 130
            };


            _alarmCommand = new byte[]
            {
                231, 33, 254, 159, 25, 70, 136, 237, 238, 39, 247, 233, 118, 193, 35, 101, 218,
                188, 227, 155, 9, 198, 217, 157, 175, 171, 90, 149
            };

            _channelInfo = new byte[16];
            _dataState = DataState.Stopped;
            _deviceState = DeviceState.DisConnected;
            _workState = WorkStateEnum.WorkState;

            _algorithm = new Algorithm();
            _filePath = new FilePath();
            _server = new TcpPackServer();
            ClinetList = new ObservableCollection<IntPtr>();

            InitializeComponent();

            _lineGraphs = new List<LineGraph>
            {
                new LineGraph()
                {
                    Stroke = new SolidColorBrush(Color.FromArgb(255, 0, 0, 0)),
                    Description = $"信号通道 {1}",
                    StrokeThickness = 2
                },
                new LineGraph()
                {
                    Stroke = new SolidColorBrush(Color.FromArgb(255, 0, 0, 0)),
                    Description = $"信号通道 {1}",
                    StrokeThickness = 2
                },
                new LineGraph()
                {
                    Stroke = new SolidColorBrush(Color.FromArgb(255, 255, 0, 0)),
                    Description = $"信号通道 {2}",
                    StrokeThickness = 2
                },
                new LineGraph()
                {
                    Stroke = new SolidColorBrush(Color.FromArgb(255, 255, 0, 0)),
                    Description = $"信号通道 {2}",
                    StrokeThickness = 2
                },
                new LineGraph()
                {
                    Stroke = new SolidColorBrush(Color.FromArgb(255, 0, 255, 0)),
                    Description = $"信号通道 {3}",
                    StrokeThickness = 2
                },
                new LineGraph()
                {
                    Stroke = new SolidColorBrush(Color.FromArgb(255, 0, 255, 0)),
                    Description = $"信号通道 {3}",
                    StrokeThickness = 2
                },
                new LineGraph()
                {
                    Stroke = new SolidColorBrush(Color.FromArgb(255, 0, 0, 255)),
                    Description = $"信号通道 {4}",
                    StrokeThickness = 2
                },
                new LineGraph()
                {
                    Stroke = new SolidColorBrush(Color.FromArgb(255, 0, 0, 255)),
                    Description = $"信号通道 {4}",
                    StrokeThickness = 2
                },
            };
        }

        private void MetroWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                AddStatus("服务未启动", Color.FromRgb(255, 0, 0));

                IpAddress.Text = Properties.Settings.Default.address;
                ProtNum.Text = Properties.Settings.Default.port;
                Duration.Value = Properties.Settings.Default.duration;
                SinFreqNum.Value = Properties.Settings.Default.singfreq;
                MinNum.Value = Properties.Settings.Default.minfreq;
                MaxNum.Value = Properties.Settings.Default.maxfreq;
                Amplitude.Value = Properties.Settings.Default.amplitude;
                SampFreq.SelectedIndex = Properties.Settings.Default.sampfreq;
                SingalType.SelectedIndex = Properties.Settings.Default.singaltype;
                ChanNum.SelectedIndex = Properties.Settings.Default.channum;

                _server.OnPrepareListen += OnPrepareListen;
                _server.OnAccept += OnAccept;
                _server.OnSend += OnSend;
                _server.OnReceive += OnReceive;
                _server.OnClose += OnClose;
                _server.OnShutdown += OnShutdown;

                _channelInfo[0] = 1;

                for (int i = 0; i < 8; i = i + 2)
                {
                    Tlines.Children.Add(_lineGraphs[i]);
                    Flines.Children.Add(_lineGraphs[i + 1]);
                }

                // 设置包头标识,与对端设置保证一致性 存储方式为小端存储 例如 需要传递3个字节 若验证标识为 0xff 则网络助手应写包头为 03 00 C0 3F
                _server.PackHeaderFlag = 0xff;
                // 设置最大封包大小
                _server.MaxPackSize = 0x8FFF;

                AddMsg($"HP-Socket Version: {_server.Version}");

            }
            catch (Exception ex)
            {
                AddMsg(ex.Message);
            }
        }

        private void MetroWindow_Closed(object sender, EventArgs e)
        {
            
            Properties.Settings.Default.address = IpAddress.Text;
            Properties.Settings.Default.port = ProtNum.Text;
            Properties.Settings.Default.amplitude = Amplitude.Value;
            Properties.Settings.Default.sampfreq = SampFreq.SelectedIndex;
            Properties.Settings.Default.singaltype = SingalType.SelectedIndex;
            Properties.Settings.Default.channum = ChanNum.SelectedIndex;

            _server.Destroy();

        }

        private void AddMsg(string msg)
        {
            Dispatcher?.BeginInvoke(DispatcherPriority.Normal, (ShowMsg) delegate
            {
                //if (MsgBox.Items.Count > 50)
                //{
                //    MsgBox.Items.RemoveAt(0);
                //}

                //MsgBox.Items.Add(msg);
            });
        }

        private void AddStatus(string msg, Color color)
        {
            Dispatcher?.BeginInvoke(DispatcherPriority.Normal, (ShowMsg)delegate
            {
                WorkStateContext.Content = msg;
                WorkStateContext.Foreground = new SolidColorBrush(color);
            });
        }

        private void AddClient(string msg)
        {
            Dispatcher?.BeginInvoke(DispatcherPriority.Normal, (ShowMsg)delegate
            {
                NetMsg.Content = msg;
            });
        }

        private async void AddPlot(byte[] xData)
        {
            int j = 0;

            for (int i = 0; i < 16; i++)
            {
                if (_channelInfo[i] != 1) continue;

                await Task.Run(() =>
                {
                    _algorithm.CalculateGraph(xData, i, 512, _samFreq);
                });

                _lineGraphs[j * 2].Description = $"信号通道 {i + 1}";
                _lineGraphs[j * 2 + 1].Description = $"信号通道 {i + 1}";
                _lineGraphs[j * 2].PlotY(_algorithm.TimerGraph);
                _lineGraphs[j * 2 + 1].Plot(_algorithm.Xaxis, _algorithm.Finresult);
                j++;
            }
        }

        private void RemoveClient(int index)
        {
            Dispatcher?.BeginInvoke(DispatcherPriority.Normal, (ShowMsg) delegate { NetMsg.Content = null; });
        }

        private void SaveData(byte[] bytes)
        {
            if (string.IsNullOrEmpty(_filePath.Path)) return;
            _sw.Write(bytes);
            _sw.Flush();

        }

        private void SetDataState(DataState state)
        {
            _dataState = state;
        }

        private HandleResult OnPrepareListen(IntPtr soListen)
        {
            // 监听事件到达了,一般没什么用吧?

            return HandleResult.Ok;
        }

        private HandleResult OnAccept(IntPtr connId, IntPtr pClient)
        {
            // 客户进入了
            // 获取客户端ip和端口
            string ip = string.Empty;
            ushort port = 0;
            if (_server.GetRemoteAddress(connId, ref ip, ref port))
            {
                AddMsg($" > [{connId},OnAccept] -> PASS({ip}:{port})");
                AddClient($"{ip}:{port}");

                _deviceState = DeviceState.Connected;

                Dispatcher?.BeginInvoke(DispatcherPriority.Normal, (ShowMsg)delegate
                {
                    DebugState.IsEnabled = true;
                });

            }
            else
            {
                AddMsg($" > [{connId},OnAccept] -> Server_GetClientAddress() Error");
            }

            _connectId = connId;

            // 设置附加数据
            ClientInfo clientInfo = new ClientInfo
            {
                ConnId = connId,
                IpAddress = ip,
                Port = port
            };

            if (_server.SetExtra(connId, clientInfo) == false)
            {
                AddMsg($" > [{connId},OnAccept] -> SetConnectionExtra fail");
            }

            ClinetList.Add(connId);

            AddStatus("下位机已连接", Color.FromRgb(0, 0, 255));

            return HandleResult.Ok;
        }

        private HandleResult OnSend(IntPtr connId, byte[] bytes)
        {
            // 服务器发数据了

            AddMsg($" > [{connId},OnSend] -> ({bytes.Length} bytes)");

            return HandleResult.Ok;
        }

        private HandleResult OnReceive(IntPtr connId, byte[] bytes)
        {
            // 数据到达了
            try
            {
                // 获取附加数据
                var clientInfo = _server.GetExtra<ClientInfo>(connId);
                if (clientInfo != null)
                {
                    // clientInfo 就是accept里传入的附加数据了
                    AddMsg($" > [{clientInfo.ConnId},OnReceive] -> {clientInfo.IpAddress}:{clientInfo.Port} ({bytes.Length} bytes)");
                }
                else
                {
                    AddMsg($" > [{connId},OnReceive] -> ({bytes.Length} bytes)");
                    return HandleResult.Error;
                }

                if (_dataState == DataState.Started)
                {
                    Dispatcher?.Invoke(new UpdateBytesDelegate(SaveData), bytes);
                }

                if (bytes.Length > 32)
                {
                    Dispatcher?.Invoke(new UpdatePlot(AddPlot), bytes);
                }
                else if (bytes.Length <= 32 && bytes.Length > 20)
                {
                    if (bytes.SequenceEqual(_startCommand))
                    {
                        SetDataState(DataState.Started);
                    }

                    if (bytes.SequenceEqual(_stopCommand))
                    {
                        _sw.Dispose();
                        _filestream.Dispose();
                        SetDataState(DataState.Stopped);
                        Dispatcher?.Invoke(() =>
                        {
                            SampChange.IsEnabled = true;
                            Identy.IsEnabled = true;
                        });
                    }

                    if (bytes.SequenceEqual(_alarmCommand))
                    {
                        Dispatcher?.Invoke(() => { AddStatus("失控报警", Color.FromRgb(255, 0, 0)); });
                    }

                }
                else
                {
                    if (bytes[0] == 0x00)
                    {
                        Dispatcher?.BeginInvoke(DispatcherPriority.Normal, (ShowMsg)delegate
                        {
                            _samFreq = 1000;
                            SampleContent.Content = "1000Hz采样率";
                            AddStatus("工作模式", Color.FromRgb(0, 255, 0));
                        });
                    }
                    else if (bytes[0] == 0x01)
                    {
                        Dispatcher?.BeginInvoke(DispatcherPriority.Normal, (ShowMsg)delegate
                        {
                            _samFreq = 2000;
                            SampleContent.Content = "2000Hz采样率";
                            AddStatus("工作模式", Color.FromRgb(0, 255, 0));
                        });
                    }
                    else if (bytes[0] == 0x02)
                    {
                        Dispatcher?.BeginInvoke(DispatcherPriority.Normal, (ShowMsg)delegate
                        {
                            _samFreq = 4000;
                            SampleContent.Content = "4000Hz采样率";
                            AddStatus("工作模式", Color.FromRgb(0, 255, 0));
                        });
                    }
                    else if (bytes[0] == 0xff)
                    {
                        Dispatcher?.BeginInvoke(DispatcherPriority.Normal, (ShowMsg)delegate
                            {
                                RotateSpeed1.Content = "转速1：" + BitConverter.ToUInt16(bytes, 2).ToString() + "转/秒";
                                RotateSpeed2.Content = "转速2：" + BitConverter.ToUInt16(bytes, 4).ToString() + "转/秒";
                                RotateSpeed3.Content = "转速3：" + BitConverter.ToUInt16(bytes, 6).ToString() + "转/秒";
                                RotateSpeed4.Content = "转速4：" + BitConverter.ToUInt16(bytes, 8).ToString() + "转/秒";
                            });
                    }
                    else
                    {
                        Dispatcher?.BeginInvoke(DispatcherPriority.Normal, (ShowMsg)delegate
                        {
                            _samFreq = 8000;
                            SampleContent.Content = "8000Hz采样率";
                            AddStatus("工作模式", Color.FromRgb(0, 255, 0));
                        });
                    }

                }

                return HandleResult.Ok;
            }
            catch (Exception)
            {
                return HandleResult.Error;
            }
        }

        private HandleResult OnClose(IntPtr connId, SocketOperation enOperation, int errorCode)
        {
            AddMsg(errorCode == 0
                ? $" > [{connId},OnClose]"
                : $" > [{connId},OnError] -> OP:{enOperation},CODE:{errorCode}");

            var index = ClinetList.IndexOf(connId);
            ClinetList.RemoveAt(index);
            RemoveClient(index);

            if (_server.RemoveExtra(connId) == false)
            {
                AddMsg($" > [{connId},OnClose] -> SetConnectionExtra({connId}, null) fail");
            }

            AddStatus("下位机已断开", Color.FromRgb(255, 0, 0));

            _deviceState = DeviceState.DisConnected;

            return HandleResult.Ok;
        }

        private HandleResult OnShutdown()
        {
            // 服务关闭了
            AddStatus("服务未启动", Color.FromRgb(255, 0, 0));
            AddMsg("已停止服务");
            return HandleResult.Ok;
        }

        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(IpAddress.Text) || string.IsNullOrEmpty(ProtNum.Text))
            {
                AddMsg("请填写完整目标端地址及端口号");
                return;
            }

            string tmpIp = IpAddress.Text.Trim();
            ushort port = ushort.Parse(ProtNum.Text.Trim());

            _server.IpAddress = tmpIp;
            _server.Port = port;

            try
            {
                if (_server.Start())
                {
                    AddMsg($"服务已经正常启动 ->({tmpIp}:{port})");
                    AddStatus("下位机未连接", Color.FromRgb(255, 0, 0));
                }

                Connect.IsEnabled = false;
                UnCon.IsEnabled = true;
            }
            catch (Exception ex)
            {
                AddMsg(ex.Message);
            }

        }

        private void UnCon_Click(object sender, RoutedEventArgs e)
        {
            if (_server.Stop())
            {
                Connect.IsEnabled = true;
                UnCon.IsEnabled = false;
                Save1772Data.IsEnabled = false;
                SendParam.IsEnabled = false;
                DebugState.IsEnabled = false;
                WorkState.IsEnabled = false;
                Programing.IsEnabled = false;
                SampChange.IsEnabled = false;
                Identy.IsEnabled = false;
            }
            else
            {
                AddMsg($"停止错误 -> {_server.ErrorMessage}({_server.ErrorCode})");
            }
        }

        private void DebugState_Click(object sender, RoutedEventArgs e)
        {
            var commandBuf = new byte[16];

            commandBuf[0] = 0x01;
            commandBuf[1] = 0x01;
            commandBuf[2] = 0x04;
            commandBuf[3] = 0x01;
            _server.Send(_connectId, commandBuf, 4);

            AddStatus("调试模式", Color.FromRgb(0, 255, 0));

            _workState = WorkStateEnum.DebugState;

            Save1772Data.IsEnabled = true;
            SendParam.IsEnabled = true;
            DebugState.IsEnabled = false;
            WorkState.IsEnabled = true;
            Programing.IsEnabled = true;
            SampChange.IsEnabled = true;
            Identy.IsEnabled = true;

        }

        private void WorkState_Click(object sender, RoutedEventArgs e)
        {
            var commandBuf = new byte[16];

            commandBuf[0] = 0x01;
            commandBuf[1] = 0x01;
            commandBuf[2] = 0x04;
            commandBuf[3] = 0x00;
            _server.Send(_connectId, commandBuf, 4);

            AddStatus("工作模式", Color.FromRgb(0, 255, 0));

            _workState = WorkStateEnum.WorkState;

            Save1772Data.IsEnabled = false;
            SendParam.IsEnabled = false;
            DebugState.IsEnabled = true;
            WorkState.IsEnabled = false;
            Programing.IsEnabled = false;
            SampChange.IsEnabled = false;
            Identy.IsEnabled = false;
        }

        private void DisAlarm_Click(object sender, RoutedEventArgs e)
        {

            var commandBuf = new byte[16];

            commandBuf[0] = 0x01;
            commandBuf[1] = 0x01;
            commandBuf[2] = 0x04;
            commandBuf[3] = 0x00;
            _server.Send(_connectId, commandBuf, 4);

            AddStatus("工作模式", Color.FromRgb(0, 255, 0));

            _workState = WorkStateEnum.WorkState;

            Save1772Data.IsEnabled = false;
            SendParam.IsEnabled = false;
            DebugState.IsEnabled = true;
            WorkState.IsEnabled = false;
            Programing.IsEnabled = false;
            SampChange.IsEnabled = false;
            Identy.IsEnabled = false;
        }

        private void Identy_Click(object sender, RoutedEventArgs e)
        {
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (Duration.Value == null || MaxNum.Value == null|| MinNum.Value == null ||SinFreqNum.Value == null)
            {
                return;
            }

            double duration = (double) Duration.Value;

            if (duration > 120)
                duration = 120;

            double amplitude = Amplitude.Value;
            double minnum = (double) MinNum.Value;
            double maxnum = (double) MaxNum.Value;
            double sinfreq = (double) SinFreqNum.Value;


            if (string.IsNullOrEmpty(_filePath.Path))
            {
                var mDialog = new FolderBrowserDialog();
                DialogResult result = mDialog.ShowDialog();

                if (result == System.Windows.Forms.DialogResult.Cancel)
                {
                    return;
                }

                _filePath.Path = mDialog.SelectedPath.Trim();
            }

            SampChange.IsEnabled = false;
            Identy.IsEnabled = false;

            _count2++;

            _filename = DateTime.Now.ToString("MM-dd") + "第" + Convert.ToString(_count2) + "次辨识数据（" +
                        ChanNum.SelectedItem.ToString().Trim() +
                        "）通道";
            string path = _filePath.Path + "\\" + _filename;

            _filestream = new FileStream(path: path, mode: FileMode.Create, access: FileAccess.Write);
            _sw = new BinaryWriter(_filestream);

            switch (SingalType.SelectedIndex)
            {
                case 0:
                {
                    var commandBuf = new byte[16];

                    commandBuf[0] = 0x01;
                    commandBuf[1] = 0x01;
                    commandBuf[2] = 0x02;
                    commandBuf[3] = 0x00;
                    commandBuf[4] = (byte) duration;
                    commandBuf[5] = (byte) ChanNum.SelectedItem;
                    commandBuf[5] -= 1;
                    commandBuf[6] = (byte) (amplitude * 10);

                    _server.Send(_connectId, commandBuf, 7);
                    break;
                }

                case 1:
                {

                    if (sinfreq > _samFreq / 2)
                    {
                        sinfreq = _samFreq / 2;

                        SinFreqNum.Value = _samFreq / 2;
                    }

                    var commandBuf = new byte[16];

                    commandBuf[0] = 0x01;
                    commandBuf[1] = 0x01;
                    commandBuf[2] = 0x02;
                    commandBuf[3] = 0x01;
                    commandBuf[4] = (byte) duration;
                    commandBuf[5] = (byte) ChanNum.SelectedItem;
                    commandBuf[5] -= 1;
                    commandBuf[6] = (byte) (amplitude * 10);
                    commandBuf[7] = (byte) (sinfreq / 100);

                    _server.Send(_connectId, commandBuf, 8);
                    break;
                }

                default:
                {
                    if (maxnum > _samFreq / 2)
                    {
                        maxnum = _samFreq / 2;

                        MaxNum.Value = _samFreq / 2;
                    }

                    var commandBuf = new byte[16];

                    commandBuf[0] = 0x01;
                    commandBuf[1] = 0x01;
                    commandBuf[2] = 0x02;
                    commandBuf[3] = 0x02;
                    commandBuf[4] = (byte) duration;
                    commandBuf[5] = (byte) ChanNum.SelectedItem;
                    commandBuf[5] -= 1;
                    commandBuf[6] = (byte) (amplitude * 10);
                    commandBuf[7] = (byte) (minnum / 100);
                    commandBuf[8] = (byte) (maxnum / 100);

                    _server.Send(_connectId, commandBuf, 9);
                    break;
                }
            }

        }

        private void Tile_Click(object sender, RoutedEventArgs e)
        {
            var commandBuf = new byte[16];

            commandBuf[0] = 0x01;
            commandBuf[1] = 0x01;
            commandBuf[2] = 0x03;
            commandBuf[3] = (byte)SampFreq.SelectedIndex;

            _server.Send(_connectId, commandBuf, 4);
            _samFreq = int.Parse(SampFreq.SelectedItem.ToString().Trim());
            SampleContent.Content = SampFreq.SelectedItem + "Hz采样率";
        }

        private void Programing_Click(object sender, RoutedEventArgs e)
        {
            var commandBuf = new byte[16];
            commandBuf[0] = 0x01;
            commandBuf[1] = 0x01;
            commandBuf[2] = 0x05;
            _server.Send(_connectId, commandBuf, 3);

        }

        //private void ChannelControl_Click(object sender, RoutedEventArgs e)
        //{
        //    var commandBuf = new byte[16];
        //    if (ChannelControl.IsChecked == true)
        //    {
        //        commandBuf[0] = 0x01;
        //        commandBuf[1] = 0x01;
        //        commandBuf[2] = 0x06;
        //        commandBuf[3] = 0x01;
        //        _server.Send(_connectId, commandBuf, 4);
        //    }
        //    else
        //    {
        //        commandBuf[0] = 0x01;
        //        commandBuf[1] = 0x01;
        //        commandBuf[2] = 0x06;
        //        commandBuf[3] = 0x00;
        //        _server.Send(_connectId, commandBuf, 4);
        //    }
        //}

        private void Save1772Data_Click(object sender, RoutedEventArgs e)
        {
            var tile = (MahApps.Metro.Controls.Tile)sender;

            if (string.IsNullOrEmpty(_filePath.Path))
            {
                var mDialog = new FolderBrowserDialog();
                DialogResult result = mDialog.ShowDialog();

                if (result == System.Windows.Forms.DialogResult.Cancel)
                {
                    return;
                }
                _filePath.Path = mDialog.SelectedPath.Trim();
            }

            if (_dataState == DataState.Started)
            {
                SetDataState(DataState.Stopped);
                tile.Title = "开始保存";
                _sw.Dispose();
                _filestream.Dispose();

            }
            else
            {
                _count++;
                _filename = DateTime.Now.ToString("MM-dd") + "第" + Convert.ToString(_count) + "次非辨识数据";
                SetDataState(DataState.Started);
                tile.Title = "停止保存";
                string path = _filePath.Path + "\\" + _filename;
                _filestream = new FileStream(path: path, mode: FileMode.Create, access: FileAccess.Write);
                _sw = new BinaryWriter(_filestream);

            }
        }

        private void ChangePath_Click(object sender, RoutedEventArgs e)
        {
            var mDialog = new FolderBrowserDialog();
            DialogResult result = mDialog.ShowDialog();

            if (result == System.Windows.Forms.DialogResult.Cancel)
            {
                return;
            }
            _filePath.Path = mDialog.SelectedPath.Trim();
        }

        private void SendParam_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog()
            {
                Filter = ""
            };

            var result = openFileDialog.ShowDialog();

            if (result == false)
            {
                return;
            }

            var commandBuf = new byte[16];

            commandBuf[0] = 0x01;
            commandBuf[1] = 0x01;
            commandBuf[2] = 0x01;
            commandBuf[3] = 0x00;

            _server.Send(_connectId, commandBuf, 4);

            try
            {
                //save 语句功能 version -v7以下
                var m1 = MatlabReader.ReadAll<float>(filePath: openFileDialog.FileName);

                if (m1.ContainsKey("param"))
                {
                    if (m1["param"].ColumnCount > 16)
                    {
                        AddMsg($"读取错误 -> 参数个数错误，请检查文件");
                        return;
                    }

                    var paramBuf = new byte[512 * 4];

                    for (int i = 0; i < m1["param"].ColumnCount; i++)
                    {
                        var temp = BitConverter.GetBytes(m1["param"][0, i]);
                        paramBuf[4 * i] = temp[0];
                        paramBuf[4 * i + 1] = temp[1];
                        paramBuf[4 * i + 2] = temp[2];
                        paramBuf[4 * i + 3] = temp[3];
                    }

                    _server.Send(_connectId, paramBuf, 64);
                }

                if (m1.ContainsKey("filterCoeff"))
                {
                    if (m1["filterCoeff"].ColumnCount > 512 || m1["filterCoeff"].RowCount > 16)
                    {
                        AddMsg($"读取错误 -> 滤波器系数错误，请检查文件");
                        return;
                    }

                    for (int i = 0; i < m1["filterCoeff"].RowCount; i++)
                    {

                        var paramBuf = new byte[512 * 4];

                        for (int j = 0; j < m1["filterCoeff"].ColumnCount; j++)
                        {
                            var temp = BitConverter.GetBytes(m1["filterCoeff"][i, j]);
                            paramBuf[4 * j] = temp[0];
                            paramBuf[4 * j + 1] = temp[1];
                            paramBuf[4 * j + 2] = temp[2];
                            paramBuf[4 * j + 3] = temp[3];
                        }

                        _server.Send(_connectId, paramBuf, 512 * 4);
                    }
                }
            }
            catch
            {
                AddMsg($"读取错误 -> 更改参数错误，请联系开发商");
            }
        }

        private void ViewChange_Click(object sender, RoutedEventArgs e)
        {
            Flyout.IsOpen = true;
        }

        private void CheckBox1_Click(object sender, RoutedEventArgs e)
        {
            if (CheckBox1.IsChecked == true)
            {
                _channelInfo[0] = 1;

                _itemcount++;
                _lineGraphs[_itemcount * 2 - 1].Visibility = Visibility.Visible;
                _lineGraphs[_itemcount * 2 - 2].Visibility = Visibility.Visible;

                if (_itemcount > 3)
                {
                    for (int i = 0; i < ChannelList.Children.Count; i++)
                    {
                        var item = ChannelList.Children[i];
                        if (item is System.Windows.Controls.CheckBox checkBoxItem)
                        {
                            checkBoxItem.IsEnabled = false;
                        }
                    }
                }

                CheckBox1.IsEnabled = true;
            }

            else
            {
                _channelInfo[0] = 0;


                _lineGraphs[_itemcount * 2 - 1].Visibility = Visibility.Collapsed;
                _lineGraphs[_itemcount * 2 - 2].Visibility = Visibility.Collapsed;
                _itemcount--;


                if (_itemcount > 2)
                {
                    for (int i = 0; i < ChannelList.Children.Count; i++)
                    {
                        var item = ChannelList.Children[i];
                        if (item is System.Windows.Controls.CheckBox checkBoxItem)
                        {
                            checkBoxItem.IsEnabled = true;
                        }
                    }
                }

            }
        }

        private void CheckBox2_Click(object sender, RoutedEventArgs e)
        {
            if (CheckBox2.IsChecked == true)
            {
                _channelInfo[1] = 1;

                _itemcount++;
                _lineGraphs[_itemcount * 2 - 1].Visibility = Visibility.Visible;
                _lineGraphs[_itemcount * 2 - 2].Visibility = Visibility.Visible;

                if (_itemcount > 3)
                {
                    for (int i = 0; i < ChannelList.Children.Count; i++)
                    {
                        var item = ChannelList.Children[i];
                        if (item is System.Windows.Controls.CheckBox checkBoxItem)
                        {
                            checkBoxItem.IsEnabled = false;
                        }
                    }
                }

                CheckBox2.IsEnabled = true;

            }
            else
            {
                _channelInfo[1] = 0;

                _lineGraphs[_itemcount * 2 - 1].Visibility = Visibility.Collapsed;
                _lineGraphs[_itemcount * 2 - 2].Visibility = Visibility.Collapsed;
                _itemcount--;

                if (_itemcount > 2)
                {
                    for (int i = 0; i < ChannelList.Children.Count; i++)
                    {
                        var item = ChannelList.Children[i];
                        if (item is System.Windows.Controls.CheckBox checkBoxItem)
                        {
                            checkBoxItem.IsEnabled = true;
                        }
                    }
                }
            }
        }

        private void CheckBox3_Click(object sender, RoutedEventArgs e)
        {
            if (CheckBox3.IsChecked == true)
            {
                _channelInfo[2] = 1;

                _itemcount++;
                _lineGraphs[_itemcount * 2 - 1].Visibility = Visibility.Visible;
                _lineGraphs[_itemcount * 2 - 2].Visibility = Visibility.Visible;

                if (_itemcount > 3)
                {
                    for (int i = 0; i < ChannelList.Children.Count; i++)
                    {
                        var item = ChannelList.Children[i];
                        if (item is System.Windows.Controls.CheckBox checkBoxItem)
                        {
                            checkBoxItem.IsEnabled = false;
                        }
                    }
                }

                CheckBox3.IsEnabled = true;
            }
            else
            {
                _channelInfo[2] = 0;

                _lineGraphs[_itemcount * 2 - 1].Visibility = Visibility.Collapsed;
                _lineGraphs[_itemcount * 2 - 2].Visibility = Visibility.Collapsed;
                _itemcount--;

                if (_itemcount > 2)
                {
                    for (int i = 0; i < ChannelList.Children.Count; i++)
                    {
                        var item = ChannelList.Children[i];
                        if (item is System.Windows.Controls.CheckBox checkBoxItem)
                        {
                            checkBoxItem.IsEnabled = true;
                        }
                    }
                }
            }
        }

        private void CheckBox4_Click(object sender, RoutedEventArgs e)
        {
            if (CheckBox4.IsChecked == true)
            {
                _channelInfo[3] = 1;

                _itemcount++;
                _lineGraphs[_itemcount * 2 - 1].Visibility = Visibility.Visible;
                _lineGraphs[_itemcount * 2 - 2].Visibility = Visibility.Visible;

                if (_itemcount > 3)
                {
                    for (int i = 0; i < ChannelList.Children.Count; i++)
                    {
                        var item = ChannelList.Children[i];
                        if (item is System.Windows.Controls.CheckBox checkBoxItem)
                        {
                            checkBoxItem.IsEnabled = false;
                        }
                    }
                }

                CheckBox4.IsEnabled = true;
            }
            else
            {
                _channelInfo[3] = 0;

                _lineGraphs[_itemcount * 2 - 1].Visibility = Visibility.Collapsed;
                _lineGraphs[_itemcount * 2 - 2].Visibility = Visibility.Collapsed;
                _itemcount--;


                if (_itemcount > 2)
                {
                    for (int i = 0; i < ChannelList.Children.Count; i++)
                    {
                        var item = ChannelList.Children[i];
                        if (item is System.Windows.Controls.CheckBox checkBoxItem)
                        {
                            checkBoxItem.IsEnabled = true;
                        }
                    }
                }
            }
        }

        private void CheckBox5_Click(object sender, RoutedEventArgs e)
        {
            if (CheckBox5.IsChecked == true)
            {
                _channelInfo[4] = 1;

                _itemcount++;
                _lineGraphs[_itemcount * 2 - 1].Visibility = Visibility.Visible;
                _lineGraphs[_itemcount * 2 - 2].Visibility = Visibility.Visible;

                if (_itemcount > 3)
                {
                    for (int i = 0; i < ChannelList.Children.Count; i++)
                    {
                        var item = ChannelList.Children[i];
                        if (item is System.Windows.Controls.CheckBox checkBoxItem)
                        {
                            checkBoxItem.IsEnabled = false;
                        }
                    }
                }

                CheckBox5.IsEnabled = true;

            }
            else
            {
                _channelInfo[4] = 0;

                _lineGraphs[_itemcount * 2 - 1].Visibility = Visibility.Collapsed;
                _lineGraphs[_itemcount * 2 - 2].Visibility = Visibility.Collapsed;
                _itemcount--;

                if (_itemcount > 2)
                {
                    for (int i = 0; i < ChannelList.Children.Count; i++)
                    {
                        var item = ChannelList.Children[i];
                        if (item is System.Windows.Controls.CheckBox checkBoxItem)
                        {
                            checkBoxItem.IsEnabled = true;
                        }
                    }
                }
            }
        }

        private void CheckBox6_Click(object sender, RoutedEventArgs e)
        {
            if (CheckBox6.IsChecked == true)
            {
                _channelInfo[5] = 1;

                _itemcount++;
                _lineGraphs[_itemcount * 2 - 1].Visibility = Visibility.Visible;
                _lineGraphs[_itemcount * 2 - 2].Visibility = Visibility.Visible;

                if (_itemcount > 3)
                {
                    for (int i = 0; i < ChannelList.Children.Count; i++)
                    {
                        var item = ChannelList.Children[i];
                        if (item is System.Windows.Controls.CheckBox checkBoxItem)
                        {
                            checkBoxItem.IsEnabled = false;
                        }
                    }
                }

                CheckBox6.IsEnabled = true;
            }
            else
            {
                _channelInfo[5] = 0;

                _lineGraphs[_itemcount * 2 - 1].Visibility = Visibility.Collapsed;
                _lineGraphs[_itemcount * 2 - 2].Visibility = Visibility.Collapsed;
                _itemcount--;

                if (_itemcount > 2)
                {
                    for (int i = 0; i < ChannelList.Children.Count; i++)
                    {
                        var item = ChannelList.Children[i];
                        if (item is System.Windows.Controls.CheckBox checkBoxItem)
                        {
                            checkBoxItem.IsEnabled = true;
                        }
                    }
                }
            }
        }

        private void CheckBox7_Click(object sender, RoutedEventArgs e)
        {
            if (CheckBox7.IsChecked == true)
            {
                _channelInfo[6] = 1;

                _itemcount++;
                _lineGraphs[_itemcount * 2 - 1].Visibility = Visibility.Visible;
                _lineGraphs[_itemcount * 2 - 2].Visibility = Visibility.Visible;

                if (_itemcount > 3)
                {
                    for (int i = 0; i < ChannelList.Children.Count; i++)
                    {
                        var item = ChannelList.Children[i];
                        if (item is System.Windows.Controls.CheckBox checkBoxItem)
                        {
                            checkBoxItem.IsEnabled = false;
                        }
                    }
                }

                CheckBox7.IsEnabled = true;
            }
            else
            {
                _channelInfo[6] = 0;

                _lineGraphs[_itemcount * 2 - 1].Visibility = Visibility.Collapsed;
                _lineGraphs[_itemcount * 2 - 2].Visibility = Visibility.Collapsed;
                _itemcount--;

                if (_itemcount > 2)
                {
                    for (int i = 0; i < ChannelList.Children.Count; i++)
                    {
                        var item = ChannelList.Children[i];
                        if (item is System.Windows.Controls.CheckBox checkBoxItem)
                        {
                            checkBoxItem.IsEnabled = true;
                        }
                    }
                }
            }
        }

        private void CheckBox8_Click(object sender, RoutedEventArgs e)
        {
            if (CheckBox8.IsChecked == true)
            {
                _channelInfo[7] = 1;

                _itemcount++;
                _lineGraphs[_itemcount * 2 - 1].Visibility = Visibility.Visible;
                _lineGraphs[_itemcount * 2 - 2].Visibility = Visibility.Visible;

                if (_itemcount > 3)
                {
                    for (int i = 0; i < ChannelList.Children.Count; i++)
                    {
                        var item = ChannelList.Children[i];
                        if (item is System.Windows.Controls.CheckBox checkBoxItem)
                        {
                            checkBoxItem.IsEnabled = false;
                        }
                    }
                }

                CheckBox8.IsEnabled = true;
            }
            else
            {
                _channelInfo[7] = 0;

                _lineGraphs[_itemcount * 2 - 1].Visibility = Visibility.Collapsed;
                _lineGraphs[_itemcount * 2 - 2].Visibility = Visibility.Collapsed;
                _itemcount--;

                if (_itemcount > 2)
                {
                    for (int i = 0; i < ChannelList.Children.Count; i++)
                    {
                        var item = ChannelList.Children[i];
                        if (item is System.Windows.Controls.CheckBox checkBoxItem)
                        {
                            checkBoxItem.IsEnabled = true;
                        }
                    }
                }
            }
        }

        private void CheckBox9_Click(object sender, RoutedEventArgs e)
        {
            if (CheckBox9.IsChecked == true)
            {
                _channelInfo[8] = 1;

                _itemcount++;
                _lineGraphs[_itemcount * 2 - 1].Visibility = Visibility.Visible;
                _lineGraphs[_itemcount * 2 - 2].Visibility = Visibility.Visible;

                if (_itemcount > 3)
                {
                    for (int i = 0; i < ChannelList.Children.Count; i++)
                    {
                        var item = ChannelList.Children[i];
                        if (item is System.Windows.Controls.CheckBox checkBoxItem)
                        {
                            checkBoxItem.IsEnabled = false;
                        }
                    }
                }

                CheckBox9.IsEnabled = true;
            }
            else
            {
                _channelInfo[8] = 0;

                _lineGraphs[_itemcount * 2 - 1].Visibility = Visibility.Collapsed;
                _lineGraphs[_itemcount * 2 - 2].Visibility = Visibility.Collapsed;
                _itemcount--;

                if (_itemcount > 2)
                {
                    for (int i = 0; i < ChannelList.Children.Count; i++)
                    {
                        var item = ChannelList.Children[i];
                        if (item is System.Windows.Controls.CheckBox checkBoxItem)
                        {
                            checkBoxItem.IsEnabled = true;
                        }
                    }
                }
            }
        }

        private void CheckBox10_Click(object sender, RoutedEventArgs e)
        {
            if (CheckBox10.IsChecked == true)
            {
                _channelInfo[9] = 1;

                _itemcount++;
                _lineGraphs[_itemcount * 2 - 1].Visibility = Visibility.Visible;
                _lineGraphs[_itemcount * 2 - 2].Visibility = Visibility.Visible;

                if (_itemcount > 3)
                {
                    for (int i = 0; i < ChannelList.Children.Count; i++)
                    {
                        var item = ChannelList.Children[i];
                        if (item is System.Windows.Controls.CheckBox checkBoxItem)
                        {
                            checkBoxItem.IsEnabled = false;
                        }
                    }
                }

                CheckBox10.IsEnabled = true;
            }
            else
            {
                _channelInfo[9] = 0;

                _lineGraphs[_itemcount * 2 - 1].Visibility = Visibility.Collapsed;
                _lineGraphs[_itemcount * 2 - 2].Visibility = Visibility.Collapsed;
                _itemcount--;

                if (_itemcount > 2)
                {
                    for (int i = 0; i < ChannelList.Children.Count; i++)
                    {
                        var item = ChannelList.Children[i];
                        if (item is System.Windows.Controls.CheckBox checkBoxItem)
                        {
                            checkBoxItem.IsEnabled = true;
                        }
                    }
                }
            }
        }

        private void CheckBox11_Click(object sender, RoutedEventArgs e)
        {
            if (CheckBox11.IsChecked == true)
            {
                _channelInfo[10] = 1;

                _itemcount++;
                _lineGraphs[_itemcount * 2 - 1].Visibility = Visibility.Visible;
                _lineGraphs[_itemcount * 2 - 2].Visibility = Visibility.Visible;

                if (_itemcount > 3)
                {
                    for (int i = 0; i < ChannelList.Children.Count; i++)
                    {
                        var item = ChannelList.Children[i];
                        if (item is System.Windows.Controls.CheckBox checkBoxItem)
                        {
                            checkBoxItem.IsEnabled = false;
                        }
                    }
                }

                CheckBox11.IsEnabled = true;
            }
            else
            {
                _channelInfo[10] = 0;

                _lineGraphs[_itemcount * 2 - 1].Visibility = Visibility.Collapsed;
                _lineGraphs[_itemcount * 2 - 2].Visibility = Visibility.Collapsed;
                _itemcount--;

                if (_itemcount > 2)
                {
                    for (int i = 0; i < ChannelList.Children.Count; i++)
                    {
                        var item = ChannelList.Children[i];
                        if (item is System.Windows.Controls.CheckBox checkBoxItem)
                        {
                            checkBoxItem.IsEnabled = true;
                        }
                    }
                }
            }
        }

        private void CheckBox12_Click(object sender, RoutedEventArgs e)
        {
            if (CheckBox12.IsChecked == true)
            {
                _channelInfo[11] = 1;

                _itemcount++;
                _lineGraphs[_itemcount * 2 - 1].Visibility = Visibility.Visible;
                _lineGraphs[_itemcount * 2 - 2].Visibility = Visibility.Visible;

                if (_itemcount > 3)
                {
                    for (int i = 0; i < ChannelList.Children.Count; i++)
                    {
                        var item = ChannelList.Children[i];
                        if (item is System.Windows.Controls.CheckBox checkBoxItem)
                        {
                            checkBoxItem.IsEnabled = false;
                        }
                    }
                }

                CheckBox12.IsEnabled = true;
            }
            else
            {
                _channelInfo[11] = 0;

                _lineGraphs[_itemcount * 2 - 1].Visibility = Visibility.Collapsed;
                _lineGraphs[_itemcount * 2 - 2].Visibility = Visibility.Collapsed;
                _itemcount--;

                if (_itemcount > 2)
                {
                    for (int i = 0; i < ChannelList.Children.Count; i++)
                    {
                        var item = ChannelList.Children[i];
                        if (item is System.Windows.Controls.CheckBox checkBoxItem)
                        {
                            checkBoxItem.IsEnabled = true;
                        }
                    }
                }
            }
        }

        private void CheckBox13_Click(object sender, RoutedEventArgs e)
        {
            if (CheckBox13.IsChecked == true)
            {
                _channelInfo[12] = 1;

                _itemcount++;
                _lineGraphs[_itemcount * 2 - 1].Visibility = Visibility.Visible;
                _lineGraphs[_itemcount * 2 - 2].Visibility = Visibility.Visible;

                if (_itemcount > 3)
                {
                    for (int i = 0; i < ChannelList.Children.Count; i++)
                    {
                        var item = ChannelList.Children[i];
                        if (item is System.Windows.Controls.CheckBox checkBoxItem)
                        {
                            checkBoxItem.IsEnabled = false;
                        }
                    }
                }

                CheckBox13.IsEnabled = true;
            }
            else
            {
                _channelInfo[12] = 0;

                _lineGraphs[_itemcount * 2 - 1].Visibility = Visibility.Collapsed;
                _lineGraphs[_itemcount * 2 - 2].Visibility = Visibility.Collapsed;
                _itemcount--;

                if (_itemcount > 2)
                {
                    for (int i = 0; i < ChannelList.Children.Count; i++)
                    {
                        var item = ChannelList.Children[i];
                        if (item is System.Windows.Controls.CheckBox checkBoxItem)
                        {
                            checkBoxItem.IsEnabled = true;
                        }
                    }
                }
            }
        }

        private void CheckBox14_Click(object sender, RoutedEventArgs e)
        {
            if (CheckBox14.IsChecked == true)
            {
                _channelInfo[13] = 1;

                _itemcount++;
                _lineGraphs[_itemcount * 2 - 1].Visibility = Visibility.Visible;
                _lineGraphs[_itemcount * 2 - 2].Visibility = Visibility.Visible;

                if (_itemcount > 3)
                {
                    for (int i = 0; i < ChannelList.Children.Count; i++)
                    {
                        var item = ChannelList.Children[i];
                        if (item is System.Windows.Controls.CheckBox checkBoxItem)
                        {
                            checkBoxItem.IsEnabled = false;
                        }
                    }
                }

                CheckBox14.IsEnabled = true;
            }
            else
            {
                _channelInfo[13] = 0;

                _lineGraphs[_itemcount * 2 - 1].Visibility = Visibility.Collapsed;
                _lineGraphs[_itemcount * 2 - 2].Visibility = Visibility.Collapsed;
                _itemcount--;

                if (_itemcount > 2)
                {
                    for (int i = 0; i < ChannelList.Children.Count; i++)
                    {
                        var item = ChannelList.Children[i];
                        if (item is System.Windows.Controls.CheckBox checkBoxItem)
                        {
                            checkBoxItem.IsEnabled = true;
                        }
                    }
                }
            }
        }

        private void CheckBox15_Click(object sender, RoutedEventArgs e)
        {
            if (CheckBox15.IsChecked == true)
            {
                _channelInfo[14] = 1;

                _itemcount++;
                _lineGraphs[_itemcount * 2 - 1].Visibility = Visibility.Visible;
                _lineGraphs[_itemcount * 2 - 2].Visibility = Visibility.Visible;

                if (_itemcount > 3)
                {
                    for (int i = 0; i < ChannelList.Children.Count; i++)
                    {
                        var item = ChannelList.Children[i];
                        if (item is System.Windows.Controls.CheckBox checkBoxItem)
                        {
                            checkBoxItem.IsEnabled = false;
                        }
                    }
                }

                CheckBox15.IsEnabled = true;
            }
            else
            {
                _channelInfo[14] = 0;

                _lineGraphs[_itemcount * 2 - 1].Visibility = Visibility.Collapsed;
                _lineGraphs[_itemcount * 2 - 2].Visibility = Visibility.Collapsed;
                _itemcount--;

                if (_itemcount > 2)
                {
                    for (int i = 0; i < ChannelList.Children.Count; i++)
                    {
                        var item = ChannelList.Children[i];
                        if (item is System.Windows.Controls.CheckBox checkBoxItem)
                        {
                            checkBoxItem.IsEnabled = true;
                        }
                    }
                }
            }
        }

        private void CheckBox16_Click(object sender, RoutedEventArgs e)
        {
            if (CheckBox16.IsChecked == true)
            {
                _channelInfo[15] = 1;

                _itemcount++;
                _lineGraphs[_itemcount * 2 - 1].Visibility = Visibility.Visible;
                _lineGraphs[_itemcount * 2 - 2].Visibility = Visibility.Visible;

                if (_itemcount > 3)
                {
                    for (int i = 0; i < ChannelList.Children.Count; i++)
                    {
                        var item = ChannelList.Children[i];
                        if (item is System.Windows.Controls.CheckBox checkBoxItem)
                        {
                            checkBoxItem.IsEnabled = false;
                        }
                    }
                }

                CheckBox16.IsEnabled = true;
            }
            else
            {
                _channelInfo[15] = 0;

                _lineGraphs[_itemcount * 2 - 1].Visibility = Visibility.Collapsed;
                _lineGraphs[_itemcount * 2 - 2].Visibility = Visibility.Collapsed;
                _itemcount--;

                if (_itemcount > 2)
                {
                    for (int i = 0; i < ChannelList.Children.Count; i++)
                    {
                        var item = ChannelList.Children[i];
                        if (item is System.Windows.Controls.CheckBox checkBoxItem)
                        {
                            checkBoxItem.IsEnabled = true;
                        }
                    }
                }
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public class ClientInfo
        {
            public IntPtr ConnId { get; set; }
            public string IpAddress { get; set; }
            public ushort Port { get; set; }
        }

    }
}
