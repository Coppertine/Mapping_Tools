﻿using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Mapping_Tools.Classes.HitsoundStuff {
    /// <summary>
    /// 
    /// </summary>
    public class Sample : INotifyPropertyChanged {
        private SampleGeneratingArgs _sampleArgs;
        private int _priority;
        private SampleSet _sampleSet;
        private Hitsound _hitsound;

        /// <summary>
        /// 
        /// </summary>
        public SampleGeneratingArgs SampleArgs {
            get { return _sampleArgs; }
            set {
                if (_sampleArgs == value) return;
                _sampleArgs = value;
                OnPropertyChanged();
            }
        }

        public int Priority {
            get { return _priority; }
            set {
                if (_priority == value) return;
                _priority = value;
                OnPropertyChanged();
            }
        }

        public SampleSet SampleSet {
            get { return _sampleSet; }
            set {
                if (_sampleSet == value) return;
                _sampleSet = value;
                OnPropertyChanged();
            }
        }

        public Hitsound Hitsound {
            get { return _hitsound; }
            set {
                if (_hitsound == value) return;
                _hitsound = value;
                OnPropertyChanged();
            }
        }

        public Sample() {
            _sampleArgs = new SampleGeneratingArgs();
            _priority = 0;
            _sampleSet = SampleSet.Normal;
            _hitsound = Hitsound.Normal;
        }

        public Sample(SampleSet sampleSet, Hitsound hitsound, SampleGeneratingArgs sampleArgs, int priority) {
            _sampleArgs = sampleArgs;
            _priority = priority;
            _sampleSet = sampleSet;
            _hitsound = hitsound;
        }

        public Sample(HitsoundLayer hl) {
            _sampleArgs = hl.SampleArgs.Copy();
            _priority = hl.Priority;
            _sampleSet = hl.SampleSet;
            _hitsound = hl.Hitsound;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public Sample Copy() {
            return new Sample(SampleSet, Hitsound, SampleArgs.Copy(), Priority);
        }

        /// <summary>Returns a string that represents the current object.</summary>
        /// <returns>A string that represents the current object.</returns>
        public override string ToString() {
            return $"{SampleArgs}, priority: {Priority}, sampleset: {SampleSet}, hitsound: {Hitsound}";
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
