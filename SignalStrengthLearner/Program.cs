#define POWERSOLVER2

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks.Dataflow;

namespace SignalStrengthLearner
{
    class SignalData
    {
        public string Ssid;
        public int SignalStrength;
        public float NormAngle;
        public int Frequency;
        public Vector3 Position;
        public float Distance;
    }
    struct Cell
    {
        public float Score;
        public Vector3 Pos;
    };

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


        static float LinearSolver1(SignalData signalData, float m1, float b1, float m2 = 0)
        {
            return Math.Max(0, (signalData.SignalStrength * m1 + b1)
               + (signalData.NormAngle * m2));
        }


        static float PowerSolver1(SignalData signalData, float a, float b, float a2, float b2)
        {
            return (b + (signalData.NormAngle * b2)) * (float)Math.Pow(Math.Abs(signalData.SignalStrength), a + (signalData.NormAngle * a2));
        }
        static float PowerSolver2(SignalData signalData, float a, float b, float c)
        {
            return b * (float)Math.Pow(Math.Abs(signalData.SignalStrength) + (signalData.NormAngle * c), a);
        }

        static void Main(string[] args)
        {
            string signalDataPath = @"C:\Git\Work\OpenXR-SDK-VisualStudio.brycehutchings\Data\SpatialLog (25).csv";

            ILookup<string, SignalData> signalData = System.IO.File.ReadAllLines(signalDataPath)
                .Select(l =>
                {
                    var tokens = l.Split(',');
                    float x = float.Parse(tokens[1]);
                    float y = float.Parse(tokens[2]);
                    float z = float.Parse(tokens[3]);

                    return new SignalData
                    {
                        NormAngle = float.Parse(tokens[4]),
                        Ssid = tokens[5],
                        SignalStrength = int.Parse(tokens[6]),
                        Frequency = int.Parse(tokens[7]),
                        Position = new Vector3(x, y, z),
                        Distance = (float)Math.Sqrt(x * x + y * y + z * z)
                    };
                })
                .ToLookup(s => s.Ssid);

#if GUESS
            using (var result = File.CreateText("Result.csv"))
            {
                result.WriteLine($"x,y,z,offset,guessdelta,guessdelta2,count2,count3");
                var internetsSignalData = signalData["Internets2"].ToList();

                const int Size = 30;


                Cell[,,] score = new Cell[Size,Size,Size];
                for (int x1 = 0; x1 < Size; x1++)
                {
                    for (int y1 = 0; y1 < Size; y1++)
                    {
                        for (int z1 = 0; z1 < Size; z1++)
                        {
                            Vector3 voxelPos = new Vector3(x1 - (Size / 2.0f), y1 - (Size / 2.0f), z1 - (Size / 2.0f));
                            voxelPos *= 0.5f;

                            float totalDelta = 0;
                            // float mathy = 0;

                            foreach (SignalData sd in internetsSignalData)
                            {
                                // Distance from hypothetical position to reading position
                                float voxelDist = Vector3.Distance(voxelPos, sd.Position);

                                // Predicted distance from reading to SSID
                                // float predictDist = PowerSolver1(sd, 2.39f, 0.00054f, 0.049f, 0.0002f);
                                // float predictDist = LinearSolver1(sd, -0.2901f, -7.8108f);
                                // float predictDist = LinearSolver1(sd, -0.26f, -5.1f, -0.5f); // Optimal for 'Internets'
                                float predictDist = LinearSolver1(sd, -0.3f, -7.9f, -1.3f); // Optimal for 'Internets2'

                                // Difference between predicted distance and voxel distance.
                                float distDelta = Math.Abs(predictDist - voxelDist);

                                // distDelta = (float)Math.Log(distDelta + 1);
                                totalDelta += distDelta;
//                                mathy += (float)Math.Sqrt(1 + distDelta); // (float)Math.Log(distDelta);
                            }

                            score[x1, y1, z1].Pos = voxelPos;
                            score[x1, y1, z1].Score = totalDelta;
                            //result.WriteLine($"{voxelPos.X},{voxelPos.Y},{voxelPos.Z},{voxelPos.Length()},{totalDelta / internetsSignalData.Count},{mathy},{countLessThan2},{countLessThan3}");
                        }
                    }
                }

                // Find local minima
                for (int x = 1; x < Size - 1; x++)
                {
                    for (int y = 1; y < Size - 1; y++)
                    {
                        for (int z = 1; z < Size - 1; z++)
                        {
                            //9+9+8
                            float me = score[x, y, z].Score;
                            if (me < score[x-1,y,z].Score && me < score[x + 1, y, z].Score && me < score[x, y - 1, z].Score && me < score[x , y + 1, z].Score && me < score[x , y, z - 1].Score && me < score[x, y, z + 1].Score)
                            {
                                Console.WriteLine($"{score[x,y,z].Pos}, {score[x,y,z].Score}");
                            }
                        }
                    }
                }
            }
#endif

            using (var result = File.CreateText("LearnScore.csv"))
            {
#if FANCY // In theory the proper algo to use... but bad in practice.
                {
                    var offsets = signalData.Select(sd => sd.Distance - ComputeFancyDistance(sd)).ToArray();
                    float avgOffset = Math.Abs(offsets.Average());
                    float avgAbsOffset = offsets.Average(o => Math.Abs(o));

                    Console.WriteLine($"Avg={avgOffset} AbsAvg={avgAbsOffset} (3.13 Best)");
                }
#endif

#if LINEAR
                // Linear is very good except at 20-30 dB where it curves a bit
                // For this reason, a POWER estimate is better
                /*
                 * SpatialLog (25).csv
                 * 0.18,5.01                            -0.27	-6	-1.8
                 * 0.12,5.03                            -0.28	-6.6	-1.7
                 * 0.07,5.06                            -0.29	-7.1	-1.8
                 * 0.02,5.11                            -0.3	-7.6	-1.8    <----Best trade off between avg offset and r-squared
                 * 0.01,5.98                            -0.31	-9.8	1.4
                */
                for (float m = -0.35f; m < -0.20f; m += 0.01f)
                {
                    Console.WriteLine(m);
                    for (float b = -15.0f; b < -5.0f; b += 0.1f)
                    {
                        for (float m2 = -2.0f; m2 <= 2.0f; m2 += 0.1f)
                        {
                            string row = $"{m:N2},{b:N2},{m2:N2}";

                            float avgOffsetTotal = 0, rSquaredTotal = 0;
                            foreach (var grouping in signalData)
                            {
                                var offsets = grouping.Select(sd => sd.Distance - LinearSolver1(sd, m, b, m2)).ToArray();
                                var avgOffset = Math.Abs(offsets.Average());
                                var rSquared = offsets.Average(o => o*o);

                                avgOffsetTotal += avgOffset;
                                rSquaredTotal += rSquared;

                                row += $",{grouping.Key},{avgOffset:N2},{rSquared:N3}";
                            }

                            avgOffsetTotal /= signalData.Count();
                            rSquaredTotal /= signalData.Count();

                            row += $",Avg,{avgOffsetTotal:N2},{rSquaredTotal:N2}";
                            result.WriteLine(row);
                        }
                    }
                }
#endif
#if POWERSOLVER1
                // Optimal:  A=2.39, B=0.00054, A2=-0.049, B2=0.0002
                // PowerSolver1(sd, 2.39, 0.00054f, 0.049f, 0.0002f)
                // AngNorm = Ang/180
                // 0.00054f * POW(ABS(dB), 2.39f + AngNorm*-0.049f)

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
#if POWERSOLVER2
                // Optimal:  A=2.41, B=0.00054, C=-0.049, 0.0002

                // Lowest r-Squared:
                // 1.99	0.0029	-5.7	Internets	0.21	7.454	Internets2	0.01	2.574	Avg	0.11	5.01
                // 1.9	0.0042	-6.2	Internets	0.24	7.402	Internets2	0.07	2.585	Avg	0.16	4.99
                // 1.94	0.0036	-5.8	Internets	0.11	7.402	Internets2	0.12	2.586	Avg	0.11	4.99

                // Best Trade-Off?
                // 1.98	0.0031	-6.2	Internets	0.06	7.444	Internets2	0.09	2.579	Avg	0.07	5.01
                // 2.02	0.0027	-7.2	Internets	0   	7.524	Internets2	0.04	2.585	Avg	0.02	5.05 <---

                // Lowest Avg Delta:
                // 2.05	0.0024	-7.4	Internets	0.01	7.57	Internets2	0	    2.588	Avg	0	    5.08
                // 2.04	0.0025	-7.4	Internets	0	7.558	    Internets2	0.01	2.588	Avg	0.01	5.07

                // 20-30db: 0-1m
                // 30-40db: 0-4m
                // 40-50db: 0-5m
                // 50-60db: 0-6m
                // 60-70db: 0-7m
                // 70-80db: 0-8m
                // therefore...
                // maxOffset = db / 10
                // Average offset is 0.0496*dB - 0.8449


                for (float a = 1.9f; a < 2.1f; a += 0.02f)
                {
                    Console.WriteLine(a);
                    for (float b = 0.0015f; b < 0.005f; b += 0.0001f)
                    {
                        for (float c = -20.0f; c <= 0.0f; c += 0.2f)
                        {
                            string row = $"{a:N2},{b:N5},{c:N2}";

                            float avgOffsetTotal = 0, rSquaredTotal = 0;
                            foreach (var grouping in signalData)
                            {
                                var offsets = grouping.Select(sd => sd.Distance - PowerSolver2(sd, a, b, c)).ToArray();
                                var avgOffset = Math.Abs(offsets.Average());
                                var rSquared = offsets.Average(o => o * o);

                                avgOffsetTotal += avgOffset;
                                rSquaredTotal += rSquared;

                                row += $",{grouping.Key},{avgOffset:N2},{rSquared:N3}";
                            }

                            avgOffsetTotal /= signalData.Count();
                            rSquaredTotal /= signalData.Count();

                            if (rSquaredTotal > 5.5f || avgOffsetTotal > 0.3f)
                            {
                                continue;
                            }

                            row += $",Avg,{avgOffsetTotal:N2},{rSquaredTotal:N2}";
                            result.WriteLine(row);

                        }
                    }
                }
#endif
            }
        }
    }
}
