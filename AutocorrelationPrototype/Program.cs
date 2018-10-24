using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutocorrelationPrototype
{
    class Program
    {
        private static string outPath;

        static void Main(string[] args)
        {
            var baseDir = @"C:\data\acm_mirum_tempo";
            var resultFile = "results.txt";

            var labels = File.ReadAllLines(Path.Combine(baseDir, "labels.txt")).Select(l => l.Split('\t')).ToDictionary(arr => arr[0], arr => float.Parse(arr[1], CultureInfo.InvariantCulture));

            using (var resultStream = File.AppendText(Path.Combine(baseDir, resultFile)))
            {
                resultStream.WriteLine($"filename\tlabel\tresult\tentropy");
                foreach (var item in labels)
                {
                    Mp3FileReader reader = new Mp3FileReader(Path.Combine(baseDir, item.Key));

                    var sampleRate = reader.WaveFormat.SampleRate;
                    var lengthSeconds = reader.TotalTime.TotalSeconds;
                    var sampleProvider = reader.ToSampleProvider().ToMono();
                    var data = new float[(int)(sampleRate * lengthSeconds)];
                    sampleProvider.Read(data, 0, data.Length);
                    var novelty = extractNovelty(data.ToArray(), sampleRate);

                    var (autoCorrelation, bpm) = autocorrelate(novelty, sampleRate);
                    var ent = entropy(autoCorrelation);
                    var log = $"{item.Key}\t{item.Value}\t{bpm}\t{ent}";
                    Console.WriteLine(log);
                    resultStream.WriteLine(log);
                    resultStream.Flush();
                }
            }
        }

        private static float[] extractNovelty(float[] data, int sampleRate)
        {
            for (int i = 0; i < data.Length; i++)
                data[i] = Math.Abs(data[i]);

            var smoothing = new MovingAverage(sampleRate / 10);

            float prevValue = 0;


            for (int i = 2; i < data.Length; ++i)
            {

                var runningAverage = smoothing.Smooth(data[i]);
                var tmp = Math.Max(0, data[i] - prevValue - runningAverage);

                prevValue = data[i];
                data[i] = tmp;
            }

            return data;
        }

        private static (float[] values, float bpm) autocorrelate(float[] novelty, int sampleRate)
        {
            int targetRate = 700;

            novelty = aggregate(novelty, sampleRate / targetRate, (f1, f2) => Math.Max(Math.Abs(f1), f2));
            float prevValue = 0;

            for (int i = 1; i < novelty.Length; i++)
            {
                var tmp = novelty[i];
                novelty[i] -= prevValue;
                prevValue = tmp;
            }
            
            int bpmMin = 40;
            int bpmMax = 260;

            float bpmStepSize = 0.5f;

            int beatsWindowSize = 3;

            var result = new float[1 + (int)((bpmMax - bpmMin) / bpmStepSize)];


            for(int i = beatsWindowSize * targetRate * 60 / bpmMin; i < novelty.Length; i++)
            {
                for(int j = 0; j < result.Length; j++)
                {
                    int interval = (int)(targetRate * 60 / (bpmMin + j * bpmStepSize));
                    float sum = 0;

                    for(int k = 1; k < beatsWindowSize; k++)
                    {
                        sum += novelty[i] * novelty[i - interval * k];
                    }

                    result[j] += sum;
                }
            }

            int maxIndex = 0;
            float maxVal = 0;

            for(int i = 0; i < result.Length; ++i)
            {
                if(result[i] < 0)
                {
                    result[i] = 0;
                }
                else if(result[i] > maxVal)
                {
                    maxVal = result[i];
                    maxIndex = i;
                }
            }

            var bpm = bpmMin + maxIndex * bpmStepSize;

            return (result, bpm);
        }

        private static float entropy(float[] data)
        {
            for (int i = 0; i < data.Length; i++)
                data[i] = Math.Abs(data[i]);

            var sum = data.Sum();
            var result = 0d;

            foreach(var val in data)
            {
                if (val > 0)
                {
                    var p = val / sum;
                    result += p * Math.Log(p);
                }
            }

            return -(float)result;
        }

        private static T[] aggregate<T>(T[] data, int size, Func<T,T,T> aggregation)
        {
            T[] result = new T[1 + (data.Length / size)];

            for(int i = 0; i < data.Length; i++)
            {
                int targetIndex = i / size;
                result[targetIndex] = aggregation(data[i], result[targetIndex]);
            }

            return result;
        }

        private static void writeWave(string filename, float[] data, int sampleRate)
        {
            using (var writer = new WaveFileWriter(Path.Combine(outPath, filename), WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1)))
            {
                writer.WriteSamples(data, 0, data.Length);
            }
        }
    }
}
