﻿using Mapping_Tools.Classes.HitsoundStuff;
using Mapping_Tools.Classes.MathUtil;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mapping_Tools.Classes.BeatmapHelper {
    public class TimelineObject {
        public HitObject Origin { get; set; }
        public double Time { get; set; }
        public int Repeat { get; set; }

        public int ObjectType { get; set; }
        private BitArray TypeArray { get => new BitArray(new int[] { ObjectType }); }
        public bool IsCircle { get => TypeArray[0]; }
        public bool IsSlider { get => TypeArray[1]; }
        public bool IsSpinner { get => TypeArray[3]; }
        public bool IsHoldNote { get => TypeArray[7]; }
        public bool IsSliderHead { get => IsSlider && Repeat == 0; }
        public bool IsSliderRepeat { get => IsSlider && Repeat != 0 && Repeat != Origin.Repeat; }
        public bool IsSliderEnd { get => IsSlider && Repeat == Origin.Repeat; }
        public bool IsSpinnerHead { get => IsSpinner && Repeat == 0; }
        public bool IsSpinnerEnd { get => IsSpinner && Repeat == 1; }
        public bool IsHoldnoteHead { get => IsHoldNote && Repeat == 0; }
        public bool IsHoldnoteEnd { get => IsHoldNote && Repeat == 1; }

        public SampleSet SampleSet { get; set; }
        public SampleSet AdditionSet { get; set; }
        public bool Normal { get; set; }
        public bool Whistle { get; set; }
        public bool Finish { get; set; }
        public bool Clap { get; set; }

        public bool HasHitsound { get => IsCircle || IsSliderHead || IsHoldnoteHead || IsSliderEnd || IsSpinnerEnd || IsSliderRepeat; }
        public bool UsesFilename { get => Filename != null && Filename != "" && (IsCircle || IsHoldnoteHead); }
        public bool CanCustoms { get => IsCircle || IsHoldnoteHead; }

        public int CustomIndex { get; set; }
        public double SampleVolume { get; set; }
        public string Filename { get; set; }

        // Special combined with greenline
        public TimingPoint TP { get; set; }
        public TimingPoint HitsoundTP { get; set; }
        public TimingPoint Redline { get; set; }
        public SampleSet FenoSampleSet { get; set; }
        public SampleSet FenoAdditionSet { get; set; }
        public int FenoCustomIndex { get; set; }
        public double FenoSampleVolume { get; set; }

        // Special for hitsound copier
        public bool canCopy = true;

        public TimelineObject(HitObject origin, double time, int objectType, int repeat, int hitsounds, SampleSet sampleset, SampleSet additionset) {
            Origin = origin;
            Time = time;

            BitArray b = new BitArray(new int[] { hitsounds });
            Normal = b[0];
            Whistle = b[1];
            Finish = b[2];
            Clap = b[3];

            SampleSet = sampleset;
            AdditionSet = additionset;

            ObjectType = objectType;

            Repeat = repeat;

            if ( IsCircle || IsHoldnoteHead ) // Can have custom index/volume/filename
            {
                CustomIndex = origin.CustomIndex;
                SampleVolume = origin.SampleVolume;
                Filename = origin.Filename;
            }
        }

        public Hitsound GetHitsound() {
            if (Normal) {
                return Hitsound.Normal;
            }
            if (Whistle) {
                return Hitsound.Whistle;
            }
            if (Finish) {
                return Hitsound.Finish;
            }
            if (Clap) {
                return Hitsound.Clap;
            }
            return Hitsound.Normal;
        }

        public int GetHitsounds() {
            return MathHelper.GetIntFromBitArray(new BitArray(new bool[] { Normal, Whistle, Finish, Clap }));
        }

        public void SetHitsound(Hitsound hitsound) {
            Normal = false;
            Whistle = false;
            Finish = false;
            Clap = false;
            switch (hitsound) {
                case Hitsound.Normal:
                    Normal = true;
                    return;
                case Hitsound.Whistle:
                    Whistle = true;
                    return;
                case Hitsound.Finish:
                    Finish = true;
                    return;
                case Hitsound.Clap:
                    Clap = true;
                    return;
            }
        }

        public bool PlaysNormal(int mode) {
            return mode != 3 || Normal || !(Whistle || Finish || Clap);
        }

        public List<Tuple<SampleSet, Hitsound, int>> GetPlayingHitsounds(int mode = 0) {
            List<Tuple<SampleSet, Hitsound, int>> samples = new List<Tuple<SampleSet, Hitsound, int>>();
            bool normal = mode != 3 || Normal || !(Whistle || Finish || Clap);

            if (normal)
                samples.Add(new Tuple<SampleSet, Hitsound, int>(FenoSampleSet, Hitsound.Normal, FenoCustomIndex));
            if (Whistle)
                samples.Add(new Tuple<SampleSet, Hitsound, int>(FenoAdditionSet, Hitsound.Whistle, FenoCustomIndex));
            if (Finish)
                samples.Add(new Tuple<SampleSet, Hitsound, int>(FenoAdditionSet, Hitsound.Finish, FenoCustomIndex));
            if (Clap)
                samples.Add(new Tuple<SampleSet, Hitsound, int>(FenoAdditionSet, Hitsound.Clap, FenoCustomIndex));

            return samples;
        }

        public List<string> GetPlayingFilenames(int mode = 0, bool includeDefaults = true) {
            List<string> samples = new List<string>();
            bool normal = mode != 3 || Normal || !(Whistle || Finish || Clap);
            bool useFilename = Filename != null && Filename != "" && (IsCircle || IsHoldnoteHead);

            if (useFilename) {
                samples.Add(Filename);
            } else if (includeDefaults || FenoCustomIndex != 0) {
                if (normal)
                    samples.Add(GetFileName(FenoSampleSet, Hitsound.Normal, FenoCustomIndex, mode));
                if (Whistle)
                    samples.Add(GetFileName(FenoAdditionSet, Hitsound.Whistle, FenoCustomIndex, mode));
                if (Finish)
                    samples.Add(GetFileName(FenoAdditionSet, Hitsound.Finish, FenoCustomIndex, mode));
                if (Clap)
                    samples.Add(GetFileName(FenoAdditionSet, Hitsound.Clap, FenoCustomIndex, mode));
            }

            return samples;
        }

        public List<string> GetFirstPlayingFilenames(int mode, string mapDir, Dictionary<string, string> firstSamples, bool includeDefaults=true) {
            List<string> samples = new List<string>();
            bool normal = mode != 3 || Normal || !(Whistle || Finish || Clap);
            bool useFilename = Filename != null && Filename != "" && (IsCircle || IsHoldnoteHead);

            if (useFilename) {
                string samplePath = Path.Combine(mapDir, Filename);
                string fullPathExtLess = Path.Combine(Path.GetDirectoryName(samplePath), Path.GetFileNameWithoutExtension(samplePath));

                // Get the first occurence of this sound to not get duplicated
                if (firstSamples.Keys.Contains(fullPathExtLess)) {
                    samples.Add(Path.GetFileName(firstSamples[fullPathExtLess]));
                }
            } else if (includeDefaults || FenoCustomIndex != 0) {
                if (normal)
                    AddFirstIdenticalFilename(FenoSampleSet, Hitsound.Normal, FenoCustomIndex, samples, mode, useFilename, mapDir, firstSamples, includeDefaults);
                if (Whistle)
                    AddFirstIdenticalFilename(FenoAdditionSet, Hitsound.Whistle, FenoCustomIndex, samples, mode, useFilename, mapDir, firstSamples, includeDefaults);
                if (Finish)
                    AddFirstIdenticalFilename(FenoAdditionSet, Hitsound.Finish, FenoCustomIndex, samples, mode, useFilename, mapDir, firstSamples, includeDefaults);
                if (Clap)
                    AddFirstIdenticalFilename(FenoAdditionSet, Hitsound.Clap, FenoCustomIndex, samples, mode, useFilename, mapDir, firstSamples, includeDefaults);
            }

            return samples;
        }

        private void AddFirstIdenticalFilename(SampleSet sampleSet, Hitsound hitsound, int index, List<string> samples, int mode, bool useFilename, string mapDir, Dictionary<string, string> firstSamples, bool includeDefaults) {
            string filename = GetFileName(sampleSet, hitsound, index, mode);
            string samplePath = Path.Combine(mapDir, filename);
            string fullPathExtLess = Path.Combine(Path.GetDirectoryName(samplePath), Path.GetFileNameWithoutExtension(samplePath));

            // Get the first occurence of this sound to not get duplicated
            if (firstSamples.Keys.Contains(fullPathExtLess)) {
                if (!useFilename) {
                    samples.Add(Path.GetFileName(firstSamples[fullPathExtLess]));
                }
            } else {
                // Sample doesn't exist
                if (!useFilename && includeDefaults) {
                    samples.Add(GetFileName(sampleSet, hitsound, 0, mode));
                }
            }
        }

        public void HitsoundsToOrigin() {
            if (Origin.IsCircle || (Origin.IsSpinner && Repeat == 1) || (Origin.IsHoldNote && Repeat == 0)) {
                Origin.Hitsounds = GetHitsounds();
                Origin.SampleSet = SampleSet;
                Origin.AdditionSet = AdditionSet;
                Origin.CustomIndex = CustomIndex;
                Origin.SampleVolume = SampleVolume;
                Origin.Filename = Filename;
            } else if (Origin.IsSlider) {
                Origin.EdgeHitsounds[Repeat] = GetHitsounds();
                Origin.EdgeSampleSets[Repeat] = SampleSet;
                Origin.EdgeAdditionSets[Repeat] = AdditionSet;
                Origin.SliderExtras = true;
            }
        }

        public void GiveHitsoundTimingPoint(TimingPoint hstp) {
            HitsoundTP = hstp;
            if (SampleSet == 0) {
                FenoSampleSet = hstp.SampleSet;
            } else {
                FenoSampleSet = SampleSet;
            }
            if (AdditionSet == 0) {
                FenoAdditionSet = FenoSampleSet;
            } else {
                FenoAdditionSet = AdditionSet;
            }
            if (CustomIndex == 0) {
                FenoCustomIndex = hstp.SampleIndex;
            } else {
                FenoCustomIndex = CustomIndex;
            }
            if (SampleVolume == 0) {
                FenoSampleVolume = hstp.Volume;
            } else {
                FenoSampleVolume = SampleVolume;
            }
        }

        public static string GetFileName(SampleSet sampleSet, Hitsound hitsound, int index, int mode) {
            string taiko = mode == 1 ? "taiko-" : "";
            if (index == 0) {
                return string.Format("{2}{0}-hit{1}-default.wav", sampleSet.ToString().ToLower(), hitsound.ToString().ToLower(), taiko);
            }
            if (index == 1) {
                return string.Format("{2}{0}-hit{1}.wav", sampleSet.ToString().ToLower(), hitsound.ToString().ToLower(), taiko);
            }
            return string.Format("{3}{0}-hit{1}{2}.wav", sampleSet.ToString().ToLower(), hitsound.ToString().ToLower(), index, taiko);
        }
    }
}
