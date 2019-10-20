using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MathNet.Numerics.Data.Matlab;
using MathNet.Numerics.LinearAlgebra;

namespace TestUI.Tests
{
    [TestClass()]
    public class AlgorithmTests
    {
        [TestMethod()]
        public void CalculateGraphTest()
        {
            var testData = MatlabReader.ReadAll<double>("data.mat")["Data"];
            var testResult = MatlabReader.ReadAll<double>("testResult.mat")["SecPathKJ"];

            Algorithm algorithm = new Algorithm();

            Vector<double> result = Vector<double>.Build.Dense(512, 0);

            //for (int i = 0; i < testData.RowCount; i = i + 512)
            //{
            for (int j = 0; j < 1; j++)
            {
                //var chanData = testData.Column(j, i, 512);
                //var outData = testData.Column(16, i, 512);

                var chanData = testData.Column(j);
                var outData = testData.Column(16);

                result = algorithm.Nlms(outData, chanData, 512, 0.1);
            }
            //}

            for (int i = 0; i < testResult.RowCount; i++)
            {
                if (Math.Abs(testResult[i, 0] - result[i]) > 1e-10)
                {
                    Assert.Fail();
                }
            }

        }
    }
}