using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutocorrelationPrototype
{
    public class MovingAverage
    {
        private readonly float[] data;
        private float average;
        private int index;

        public MovingAverage(int size)
        {
            data = new float[size];
            this.index = size;
        }


        public float Smooth(float value)
        {
            value /= data.Length;

            average += value;
            average -= data[index % data.Length];

            data[index++ % data.Length] = value;

            return average;
        }


    }
}
