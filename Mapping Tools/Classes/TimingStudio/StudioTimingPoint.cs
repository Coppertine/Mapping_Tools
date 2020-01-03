using Editor_Reader;
using Mapping_Tools.Classes.BeatmapHelper;
using Mapping_Tools.Classes.HitsoundStuff;

namespace Mapping_Tools.Classes.TimingStudio
{

    public class StudioTimingPoint : TimingPoint
    {

        
        public BeatTime Beat { get; set; }

        public StudioTimingPoint(ControlPoint cp) 
            : base(cp)
        {
            
            Beat = new BeatTime();
        }

        public StudioTimingPoint(string line)
            : base(line)
        {
            Beat = new BeatTime();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="mpb"></param>
        /// <param name="meter"></param>
        /// <param name="sampleSet"></param>
        /// <param name="sampleIndex"></param>
        /// <param name="volume"></param>
        /// <param name="uninherited"></param>
        /// <param name="kiai"></param>
        /// <param name="omitFirstBarLine"></param>
        public StudioTimingPoint(double offset, double mpb, int meter, SampleSet sampleSet, int sampleIndex, double volume, bool uninherited, bool kiai, bool omitFirstBarLine) 
            : base(offset, mpb, meter, sampleSet, sampleIndex, volume, uninherited, kiai, omitFirstBarLine)
        {
            
            Beat = new BeatTime();
        }

        public StudioTimingPoint(double offset, double mpb, int meter, SampleSet sampleSet, int sampleIndex, double volume, bool uninherited, bool kiai, bool omitFirstBarLine, double numeratorMeter, BeatTime beat) 
            : base(offset, mpb, meter, sampleSet, sampleIndex, volume, uninherited, kiai, omitFirstBarLine)
        {
            
            
            Beat = beat;
        }

        public StudioTimingPoint() : base()
        {
            
            Beat = new BeatTime();
        }

        public StudioTimingPoint(TimingPoint timingPoint)
        {
            Beat = new BeatTime();
            Kiai = timingPoint.Kiai;
            Meter = timingPoint.Meter;
            MpB = timingPoint.MpB;
            Offset = timingPoint.Offset;
            OmitFirstBarLine = timingPoint.OmitFirstBarLine;
            SampleIndex = timingPoint.SampleIndex;
            SampleSet = timingPoint.SampleSet;
            Volume = timingPoint.Volume;
        }

    }
}
