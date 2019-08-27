using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MathNet.Numerics.IntegralTransforms;

namespace TestUI
{
    public class Algorithm
    {
        private readonly FourierOptions _fourierOptions = 0;
        private float[] _xTemp2 = new float[514];
        private float _fs;
        private byte[] _temp = new byte[2];

        public float[] Xaxis { get; } = new float[256];

        public double[] Finresult { get; } = new double[256];

        public float[] TimerGraph { get; } = new float[512];

        private void TimerDomain(IReadOnlyList<byte> xData, int channel)
        {
            int i = 0;
            for (int j = channel * 1024; j < 1024 + channel * 1024; j = j + 2)
            {
                _temp[0] = xData[j];
                _temp[1] = xData[j + 1];

                TimerGraph[i] = BitConverter.ToInt16(_temp, 0);
                i++;
            }
        }

        private void FreqDomain(IReadOnlyList<float> xTemp, int dotsNum)
        {
            int k = 0;
            float average = xTemp.Average();
            double temp = 0;

            for (int i = 0; i < dotsNum; i++)
            {
                _xTemp2[i] = xTemp[i] - average;
            }

            Fourier.ForwardReal(_xTemp2, dotsNum, _fourierOptions);

            for (int j = 0; j < dotsNum; j = j + 2)
            {
                Finresult[k] = Math.Sqrt(_xTemp2[j] * _xTemp2[j] + _xTemp2[j + 1] * _xTemp2[j + 1]);
                //temp = Math.Sqrt(_xTemp2[j] * _xTemp2[j] + _xTemp2[j + 1] * _xTemp2[j + 1]);
                //Finresult[k] = 10 * Math.Log(Math.Sqrt(_xTemp2[j] * _xTemp2[j] + _xTemp2[j + 1] * _xTemp2[j + 1]));
                Xaxis[k] = _fs / dotsNum * k;
                k++;
            }
        }

        public void CalculateGraph(byte[] xData, int channel, int dotsNum, int sampFreq)
        {
            _fs = sampFreq;
            TimerDomain(xData, channel);
            FreqDomain(TimerGraph, dotsNum);
        }
    }
}
