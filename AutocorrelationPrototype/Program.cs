using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutocorrelationPrototype
{
    class Program
    {
        static void Main(string[] args)
        {
            //Mp3FileReader reader = new Mp3FileReader(@"C:\data\acm_mirum_tempo\8439655.clip.mp3");
            Mp3FileReader reader = new Mp3FileReader(@"C:\tmp\100bpm_click.mp3");
            var sampleProvider = reader.ToSampleProvider().ToMono();
            var data = new float[44100 * 30];
            sampleProvider.Read(data, 0, data.Length);

            var novelty = extractNovelty(data.ToArray());

            using (var writer = new WaveFileWriter(@"C:\\tmp\\novelty.wav", WaveFormat.CreateIeeeFloatWaveFormat(44100, 1)))
            {
                writer.WriteSamples(novelty, 0, novelty.Length);
            }



            var correlation = autocorrelate(novelty);

            using (var writer = new WaveFileWriter(@"C:\\tmp\\correlation.wav", WaveFormat.CreateIeeeFloatWaveFormat(44100, 1)))
            {
                writer.WriteSamples(correlation, 0, correlation.Length);
            }

        }

        private static float[] extractNovelty(float[] data)
        {
            for (int i = 0; i < data.Length; i++)
                data[i] = Math.Abs(data[i]);

            var smoothing = new MovingAverage(4410);

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

        private static float[] autocorrelate(float[] novelty)
        {
            novelty = aggregate(novelty, 63, (f1, f2) => Math.Max(Math.Abs(f1), f2));

            float prevValue = 0;
            for (int i = 1; i < novelty.Length; i++)
            {
                var tmp = novelty[i];
                novelty[i] -= prevValue;
                prevValue = tmp;
            }

            int rate = 700;
            
            int low = rate * 60 / 40;
            int height = rate * 60 / 260;
            int beats = 3;

            var result = new float[low - height + 1];

            for (int i = low * beats; i < novelty.Length; i++)
            {
                for (int j = low; j >= height; --j)
                {
                    float sum = 0;

                    for (int k = 1; k <= beats; k++)
                    {
                        sum += novelty[i] * novelty[i - j * k];
                    }

                    result[low - j] += sum / beats;
                }
            }

            int maxIndex = 0;
            float maxVal = 0;

            for(int i = 0; i < result.Length; ++i)
            {
                if(result[i] > maxVal)
                {
                    maxVal = result[i];
                    maxIndex = i;
                }
            }

            var bpm = 220f / (low - height) * maxIndex + 40;

            Console.WriteLine(bpm);
            Console.ReadKey();


            return result;
        }

        private static T[] aggregate<T>(T[] data, int size, Func<T,T,T> aggregation)
        {
            T[] result = new T[(int)(0.5 + (double)data.Length / size)];

            for(int i = 0; i < data.Length; i++)
            {
                int targetIndex = i / size;
                result[targetIndex] = aggregation(data[i], result[targetIndex]);
            }

            return result;
        }
    }
}
