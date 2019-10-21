using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MathNet.Numerics;
using MathNet.Numerics.IntegralTransforms;
using MathNet.Numerics.LinearAlgebra;

namespace TestUI
{
    public class Algorithm
    {
        private readonly FourierOptions _fourierOptions = 0;
        private float[] _xTemp2 = new float[514];
        private Vector<double> _w;
        private Vector<double> _xBuf;
        private float _fs;

        public float[] Xaxis { get; } = new float[256];

        public double[] Finalresult { get; } = new double[256];

        public float[] TimerGraph { get; } = new float[512];


        public Algorithm()
        {
            _w = Vector<double>.Build.Dense(512, 0);
            _xBuf = Vector<double>.Build.Dense(512, 0);
        }

        public void SetZero()
        {
            _w = Vector<double>.Build.Dense(512, 0);
            _xBuf = Vector<double>.Build.Dense(512, 0);
        }

        private void TimerDomain(byte[] xData, int channel)
        {
            int i = 0;
            for (int j = channel * 1024; j < 1024 + channel * 1024; j = j + 2)
            {
                TimerGraph[i] = BitConverter.ToInt16(xData, j);
                i++;
            }
        }

        private void FreqDomain(IReadOnlyList<float> xTemp, int dotsNum)
        {
            int k = 0;
            float average = xTemp.Average();

            for (int i = 0; i < dotsNum; i++)
            {
                _xTemp2[i] = xTemp[i] - average;
            }

            Fourier.ForwardReal(_xTemp2, dotsNum, _fourierOptions);

            for (int j = 0; j < dotsNum; j = j + 2)
            {
                Finalresult[k] = 10 * Math.Log(Math.Sqrt(_xTemp2[j] * _xTemp2[j] + _xTemp2[j + 1] * _xTemp2[j + 1]));
                Xaxis[k] = _fs / dotsNum * k;
                k++;
            }

            Finalresult[0] = 0;

        }

        public void CalculateGraph(byte[] xData, int channel, int dotsNum, int sampFreq)
        {
            _fs = sampFreq;
            TimerDomain(xData, channel);
            FreqDomain(TimerGraph, dotsNum);
        }

        public Vector<double> Nlms(Vector<double> outBuffer, Vector<double> chanBuffer, int FilterLength, double miu)
        {

            for (int i = 0; i < outBuffer.Count; i++)
            { 
                var temp = _xBuf.SubVector(0,511);
                _xBuf[0] = outBuffer[i];
                temp.CopySubVectorTo(_xBuf, 0, 1, 511);

                _w += (chanBuffer[i] - _xBuf * _w) * miu * _xBuf.Divide(_xBuf.PointwisePower(2).Sum());
            }

            return _w;
        }
    }

    public class Nlms
    {
        private Vector<double> _w;
        private Vector<double> _xBuf;

        public Nlms()
        {
            _w = Vector<double>.Build.Dense(512, 0);
            _xBuf = Vector<double>.Build.Dense(512, 0);
        }

        public void SetZero()
        {
            _w = Vector<double>.Build.Dense(512, 0);
            _xBuf = Vector<double>.Build.Dense(512, 0);
        }

    }
}
