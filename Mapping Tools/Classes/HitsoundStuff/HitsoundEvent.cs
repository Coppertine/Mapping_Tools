﻿using Mapping_Tools.Classes.MathUtil;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mapping_Tools.Classes.HitsoundStuff {
    /// <summary>
    /// 
    /// </summary>
    public class HitsoundEvent {
        /// <summary>
        /// 
        /// </summary>
        public double Time;
        /// <summary>
        /// 
        /// </summary>
        public double Volume;
        public SampleSet SampleSet;
        public SampleSet Additions;
        public int CustomIndex;
        public bool Whistle;
        public bool Finish;
        public bool Clap;

        public HitsoundEvent(double time, double volume, SampleSet sampleSet, SampleSet additions, int customIndex, bool whistle, bool finish, bool clap) {
            Time = time;
            Volume = volume;
            SampleSet = sampleSet;
            Additions = additions;
            CustomIndex = customIndex;
            Whistle = whistle;
            Finish = finish;
            Clap = clap;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public int GetHitsounds() {
            return MathHelper.GetIntFromBitArray(new BitArray(new bool[] { false, Whistle, Finish, Clap }));
        }
    }
}
