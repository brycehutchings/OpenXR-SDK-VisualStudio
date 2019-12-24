using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks.Dataflow;

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
        static float ComputeFancyDistance(SignalData signalData, float logBase = 10.0f, float coefficient = 20.0f, float unitAdjust = 27.55f)
        {
            double logFreq = (coefficient * Math.Log(signalData.Frequency, logBase));
            double exp = (unitAdjust - logFreq + Math.Abs(signalData.SignalStrength));
            double exp20 = exp / coefficient;
            double p = Math.Pow(logBase, exp20);
            return (float)p;
        }


        static float LinearSolver1(SignalData signalData, float m1, float b1, float m2)
        {
            return Math.Max(0, (signalData.SignalStrength * m1 + b1)
                + (signalData.Angle * m2));
        }


        static float PowerSolver1(SignalData signalData, float a, float b, float a2, float b2)
        {
            return (b + (signalData.Angle * b2)) * (float)Math.Pow(Math.Abs(signalData.SignalStrength), a + (signalData.Angle * a2));
        }
        static float PowerSolver2(SignalData signalData, float a, float b, float c)
        {
            return b * (float)Math.Pow(Math.Abs(signalData.SignalStrength) + (signalData.Angle * c), a);
        }

        static void Main(string[] args)
        {
            string signalDataPath = @"C:\Users\bryceh\Downloads\SpatialLog2 (12).csv";

            ILookup<string, SignalData> signalData = System.IO.File.ReadAllLines(signalDataPath)
                .Select(l =>
                {
                    var tokens = l.Split(',');
                    return new SignalData
                    {
                        Angle = int.Parse(tokens[1]) / 180.0f,
                        Ssid = tokens[2],
                        SignalStrength = int.Parse(tokens[3]),
                        Frequency = int.Parse(tokens[4]),
                        Distance = float.Parse(tokens[6])
                    };
                })
                .ToLookup(s => s.Ssid);

            /*{
                var offsets = signalData.Select(sd => sd.Distance - ComputeFancyDistance(sd)).ToArray();
                float avgOffset = Math.Abs(offsets.Average());
                float avgAbsOffset = offsets.Average(o => Math.Abs(o));

                Console.WriteLine($"Avg={avgOffset} AbsAvg={avgAbsOffset} (3.13 Best)");
            }*/

            using (var result = File.CreateText("Result.tsv"))
            {
#if LINEAR
                for (float m = -0.45f; m < -0.25f; m += 0.01f)
                {
                    for (float b = -20.0f; b < -6.0f; b += 0.1f)
                    {
                        for (float m2 = -10.0f; m2 <= 10.0f; m2 += 0.2f)
                        {
                            string row = $"{m:N2}\t{b:N2}\t{m2:N2}";

                            bool outOfRange = false;
                            float avgOffsetTotal = 0, rSquaredTotal = 0;
                            foreach (var grouping in signalData)
                            {
                                var offsets = grouping.Select(sd => sd.Distance - LinearSolver1(sd, m, b, m2)).ToArray();
                                var avgOffset = Math.Abs(offsets.Average());
                                var rSquared = offsets.Average(o => o*o);

                                if (avgOffset > 0.3f || rSquared > 8.0f)
                                {
                                    outOfRange = true;
                                }

                                avgOffsetTotal += avgOffset;
                                rSquaredTotal += rSquared;

                                row += $"\t{grouping.Key}\t{avgOffset:N2}\t{rSquared:N3}";
                            }

                            avgOffsetTotal /= signalData.Count();
                            rSquaredTotal /= signalData.Count();

                            if (outOfRange || avgOffsetTotal > 0.25f || rSquaredTotal > 10)
                            {
                                continue;
                            }

                            row += $"\tAvg\t{avgOffsetTotal:N2}\t{rSquaredTotal:N2}";
                            result.WriteLine(row);
                        }
                    }
                }
#endif
#if POWERSOLVER1
                for (float a = 2.3f; a < 2.5f; a += 0.01f)
                {
                    Console.WriteLine(a);
                    for (float b = 0.0005f; b < 0.0007f; b += 0.00001f)
                    {
                        for (float a2 = -0.2f; a2 <= 0f; a2 += 0.005f)
                        {
                            for (float b2 = 0; b2 <= 0.0005f; b2 += 0.00005f)
                            {
                                string row = $"{a:N2}\t{b:N5}\t{a2:N5}\t{b2:N5}";

                                bool outOfRange = false;
                                float avgOffsetTotal = 0, rSquaredTotal = 0;
                                foreach (var grouping in signalData)
                                {
                                    var offsets = grouping.Select(sd => sd.Distance - PowerSolver1(sd, a, b, a2, b2)).ToArray();
                                    var avgOffset = Math.Abs(offsets.Average());
                                    var rSquared = offsets.Average(o => o * o);

                                    if (avgOffset > 0.3f || rSquared > 8.0f)
                                    {
                                        outOfRange = true;
                                        break;
                                    }

                                    avgOffsetTotal += avgOffset;
                                    rSquaredTotal += rSquared;

                                    row += $"\t{grouping.Key}\t{avgOffset:N2}\t{rSquared:N3}";
                                }

                                avgOffsetTotal /= signalData.Count();
                                rSquaredTotal /= signalData.Count();

                                if (outOfRange || avgOffsetTotal > 0.25f || rSquaredTotal > 5)
                                {
                                    continue;
                                }

                                row += $"\tAvg\t{avgOffsetTotal:N2}\t{rSquaredTotal:N2}";
                                result.WriteLine(row);
                            }
                        }
                    }
                }
#endif
                for (float a = 2.3f; a < 2.5f; a += 0.01f)
                {
                    Console.WriteLine(a);
                    for (float b = 0.0005f; b < 0.0007f; b += 0.00001f)
                    {
                        for (float c = -20.0f; c <= 20.0f; c += 0.1f)
                        {
                            string row = $"{a:N2}\t{b:N5}\t{c:N2}";

                            bool outOfRange = false;
                            float avgOffsetTotal = 0, rSquaredTotal = 0;
                            foreach (var grouping in signalData)
                            {
                                var offsets = grouping.Select(sd => sd.Distance - PowerSolver2(sd, a, b, c)).ToArray();
                                var avgOffset = Math.Abs(offsets.Average());
                                var rSquared = offsets.Average(o => o * o);

                                if (avgOffset > 0.3f || rSquared > 8.0f)
                                {
                                    outOfRange = true;
                                    break;
                                }

                                avgOffsetTotal += avgOffset;
                                rSquaredTotal += rSquared;

                                row += $"\t{grouping.Key}\t{avgOffset:N2}\t{rSquared:N3}";
                            }

                            avgOffsetTotal /= signalData.Count();
                            rSquaredTotal /= signalData.Count();

                            if (outOfRange || avgOffsetTotal > 0.25f || rSquaredTotal > 5)
                            {
                                continue;
                            }

                            row += $"\tAvg\t{avgOffsetTotal:N2}\t{rSquaredTotal:N2}";
                            result.WriteLine(row);

                        }
                    }
                }
            }
        }
    }
}
