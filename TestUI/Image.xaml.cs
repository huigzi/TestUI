using InteractiveDataDisplay.WPF;
using MathNet.Numerics.LinearAlgebra;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace TestUI
{
    /// <summary>
    /// Image.xaml 的交互逻辑
    /// </summary>
    public partial class ImageWindow 
    {
        private readonly List<LineGraph> _lineGraphs;
        private readonly Algorithm _algorithm;
        public byte[] ChannelInfo;

        public ImageWindow()
        {
            InitializeComponent();

            _algorithm = new Algorithm();
            _lineGraphs = new List<LineGraph>
            {
                new LineGraph()
                {
                    Stroke = new SolidColorBrush(Color.FromArgb(255, 0, 0, 0)),
                    Description = $"信号通道",
                    StrokeThickness = 2
                },
                new LineGraph()
                {
                    Stroke = new SolidColorBrush(Color.FromArgb(255, 0, 0, 0)),
                    Description = $"信号通道",
                    StrokeThickness = 2
                },
                new LineGraph()
                {
                    Stroke = new SolidColorBrush(Color.FromArgb(255, 0, 0, 0)),
                    Description = $"信号通道",
                    StrokeThickness = 2
                }
            };

            ChannelInfo = new byte[16];
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {

            Tlines.Children.Add(_lineGraphs[0]);
            Flines.Children.Add(_lineGraphs[1]);
            Wlines.Children.Add(_lineGraphs[2]);
        }

        public async Task<List<Vector<double>>> AddPlot(byte[] xData, int samFreq, DataState identityState, double? miu)
        {
            List<Vector<double>> identityResult = new List<Vector<double>>(16);

            await Task.Run(() =>
            {
                //辨识计算
                if (identityState == DataState.Started)
                {
                    var outBuffer = new double[512];

                    int i = 0;

                    for (int j = 16 * 1024; j < 1024 + 16 * 1024; j = j + 2)
                    {
                        outBuffer[i] = BitConverter.ToInt16(xData, j);
                        i++;
                    }

                    i = 0;

                    for (int j = 0; j < 16; j++)
                    {
                        var chanBuffer = new double[512];
                        for (int k = j * 1024; k < 1024 + j * 1024; k = k + 2)
                        {
                            chanBuffer[i] = BitConverter.ToInt16(xData, k);
                            i++;
                        }

                        Vector<double> outData = Vector<double>.Build.DenseOfArray(outBuffer);
                        Vector<double> chanData = Vector<double>.Build.DenseOfArray(chanBuffer);

                        identityResult[j] = _algorithm.Nlms(outData, chanData, 512, (double)miu);
                    }
                }

                for (int j = 0; j < 16; j++)
                {
                    if (ChannelInfo[j] != 1) continue;

                    _algorithm.CalculateGraph(xData, j, 512, samFreq);

                    _lineGraphs[0].Description = $"信号通道 {j}";
                    _lineGraphs[1].Description = $"信号通道 {j}";
                    _lineGraphs[2].Description = $"信号通道 {j}";
                    _lineGraphs[0].PlotY(_algorithm.TimerGraph);
                    _lineGraphs[1].Plot(_algorithm.Xaxis, _algorithm.Finalresult);
                    _lineGraphs[2].PlotY(identityResult[j]);
                }

            });

            return identityResult;

        }
    }
}
