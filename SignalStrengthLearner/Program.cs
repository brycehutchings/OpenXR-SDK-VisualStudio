using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SignalStrengthLearner
{
    struct SignalData
    {
        public string Ssid;
        public int SignalStrength;
        public float Angle;
        public int Frequency;
        public float Distance;
    }

    class Program
    {
        static float ComputeDistance(SignalData signalData, float logBase = 10.0f, float coefficient = 20.0f, float unitAdjust = 27.55f)
        {
            double logFreq = (coefficient * Math.Log(signalData.Frequency, logBase));
            double exp = (unitAdjust - logFreq + Math.Abs(signalData.SignalStrength));
            double exp20 = exp / coefficient;
            double p = Math.Pow(logBase, exp20);
            return (float)p;
        }

        static void Main(string[] args)
        {
            string signalDataPath = @"C:\Users\bryceh\Downloads\SpatialLog2 (12).csv";

            var a = Math.Log(100, 10);
            var b = Math.Log10(100);

            IReadOnlyCollection<SignalData> signalData = System.IO.File.ReadAllLines(signalDataPath)
                .Select(l =>
                {
                    var tokens = l.Split(',');
                    return new SignalData {
                        Angle = int.Parse(tokens[1]) / 180.0f,
                        Ssid = tokens[2],
                        SignalStrength = int.Parse(tokens[3]),
                        Frequency = int.Parse(tokens[4]),
                        Distance = float.Parse(tokens[6])
                    };
                })
                .ToArray();

            using (var result = File.CreateText("Result.tsv"))
            {
                for (float logBase = 7.0f; logBase < 13.0f; logBase += 0.25f)
                {
                    for (float coefficient = 15; coefficient < 25; coefficient += 0.5f)
                    {
                        for (float offset = 25; offset < 30; offset += 0.25f)
                        {
                            var offsets = signalData.Select(sd => sd.Distance - ComputeDistance(sd, logBase, coefficient, offset)).ToArray();
                            float avgOffset = Math.Abs(offsets.Average());
                            float avgAbsOffset = offsets.Average(o => Math.Abs(o));

                            // Console.WriteLine($"LogBase={logBase} Coef={coefficient} Offset={offset} Avg={avgOffset} AbsAvg={avgAbsOffset}");
                            // Console.WriteLine($"{logBase},{coefficient},{offset},{avgOffset},{avgAbsOffset}");
                            result.WriteLine($"{logBase}\t{coefficient}\t{offset}\t{avgOffset}\t{avgAbsOffset}");
                        }
                    }
                }
            }
        }
    }
}
