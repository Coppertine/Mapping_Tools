using Mapping_Tools.Classes.BeatmapHelper;
using Mapping_Tools.Classes.SystemTools;
using Mapping_Tools.Classes.SystemTools.QuickRun;
using Mapping_Tools.Classes.Tools;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;

namespace Mapping_Tools.Views {
    /// <summary>
    /// Interaktionslogik für UserControl1.xaml
    /// </summary>
    [SmartQuickRunUsage(SmartQuickRunTargets.AnySelection)]
    public partial class TumourGeneratorView : IQuickRun {
        public event EventHandler RunFinished;

        public static readonly string ToolName = "Tumour Generator";

        public static readonly string ToolDescription = $@"Change the length and duration of marked sliders and this tool will automatically handle the SV for you.";

        public TumourGeneratorView() {
            InitializeComponent();
            Width = MainWindow.AppWindow.content_views.Width;
            Height = MainWindow.AppWindow.content_views.Height;
        }

        protected override void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e) {
            var bgw = sender as BackgroundWorker;
            e.Result = Tumour_Sliders((Arguments) e.Argument, bgw, e);
        }

       
        private void Start_Click(object sender, RoutedEventArgs e) {
            RunTool(MainWindow.AppWindow.GetCurrentMaps(), quick: false);
        }

        public void QuickRun() {
            RunTool(new[] { IOHelper.GetCurrentBeatmap() }, quick: true);
        }

        private void RunTool(string[] paths, bool quick = false) {
            if (!CanRun) return;

            IOHelper.SaveMapBackup(paths);

            BackgroundWorker.RunWorkerAsync(new Arguments(
                paths,
                (TumourType) Enum.Parse(typeof(TumourType), 
                    TumourModeBox.SelectedItem.ToString()
                ),
                TumourDistanceBox.GetDouble(),
                (TumourSide) Enum.Parse(typeof(TumourSide),
                    TumourSideBox.SelectedItem.ToString()
                ),
                TumourWidthBox.GetDouble(),
                TumourLengthBox.GetDouble(),
                SelectionModeBox.SelectedIndex, 
                quick));
            CanRun = false;
        }

        private struct Arguments {
            public string[] Paths;
            public TumourType TumourType;
            public double TumourDistance;
            public TumourSide TumourSide;
            public double TumourWidth;
            public double TumourLength;

            public int SelectionMode;
            public bool Quick;

            public Arguments(string[] paths, TumourType TumourType, double TumourDistance, TumourSide TumourSide, double TumourWidth, double TumourLength, int selectionMode, bool quick)
            {
                Paths = paths;
                TumourType = TumourType;
                TumourDistance = TumourDistance;
                TumourSide = TumourSide;
                TumourWidth = TumourWidth;
                TumourLength = TumourLength;
                SelectionMode = selectionMode;
                Quick = quick;
            }
        }

        private string Tumour_Sliders(Arguments arg, BackgroundWorker worker, DoWorkEventArgs _) {
            int slidersCompleted = 0;

            bool editorRead = EditorReaderStuff.TryGetFullEditorReader(out var reader);

            foreach (string path in arg.Paths) {
                var selected = new List<HitObject>();
                BeatmapEditor editor = editorRead ? EditorReaderStuff.GetNewestVersion(path, out selected, reader) : new BeatmapEditor(path);
                Beatmap beatmap = editor.Beatmap;
                Timing timing = beatmap.BeatmapTiming;
                List<HitObject> markedObjects = arg.SelectionMode == 0 ? selected :
                                                arg.SelectionMode == 1 ? beatmap.GetBookmarkedObjects() :
                                                                         beatmap.HitObjects;

                //for (int i = 0; i < markedObjects.Count; i++) {
                //    HitObject ho = markedObjects[i];
                //    if (ho.IsSlider) {
                //        double oldSpatialLength = ho.PixelLength;
                //        double newSpatialLength = arg.SpatialLength != -1 ? ho.GetSliderPath(fullLength: true).Distance * arg.SpatialLength : oldSpatialLength;
                //        double oldTemporalLength = timing.CalculateSliderTemporalLength(ho.Time, ho.PixelLength);
                //        double newTemporalLength = arg.TemporalLength != -1 ? timing.GetMpBAtTime(ho.Time) * arg.TemporalLength : oldTemporalLength;
                //        double oldSV = timing.GetSVAtTime(ho.Time);
                //        double newSV = oldSV / ((newSpatialLength / oldSpatialLength) / (newTemporalLength / oldTemporalLength));
                //        ho.SV = newSV;
                //        ho.PixelLength = newSpatialLength;
                //        slidersCompleted++;
                //    }
                //    if (worker != null && worker.WorkerReportsProgress) {
                //        worker.ReportProgress(i / markedObjects.Count);
                //    }
                //}

                //// Reconstruct SV
                //List<TimingPointsChange> timingPointsChanges = new List<TimingPointsChange>();
                //// Add Hitobject stuff
                //foreach (HitObject ho in beatmap.HitObjects) {
                //    if (ho.IsSlider) // SV changes
                //    {
                //        TimingPoint tp = ho.TP.Copy();
                //        tp.Offset = ho.Time;
                //        tp.MpB = ho.SV;
                //        timingPointsChanges.Add(new TimingPointsChange(tp, mpb: true));
                //    }
                //}

                //// Add the new SV changes
                //TimingPointsChange.ApplyChanges(timing, timingPointsChanges);



                // Save the file
                editor.SaveFile();
            }

            // Complete progressbar
            if (worker != null && worker.WorkerReportsProgress)
            {
                worker.ReportProgress(100);
            }

            // Do stuff
            if (arg.Quick)
                RunFinished?.Invoke(this, new RunToolCompletedEventArgs(true, editorRead));

            // Make an accurate message
            string message = "";
            if (Math.Abs(slidersCompleted) == 1)
            {
                message += "Successfully completed " + slidersCompleted + " slider!";
            }
            else
            {
                message += "Successfully completed " + slidersCompleted + " sliders!";
            }
            return arg.Quick ? "" : message;
        }
    }

    public enum TumourType
    {
        Default,
        Bookmark,
        SVLine
    }

    public enum TumourSide
    {
        Left,
        Right,
        Double,
        Alternate,
        Random
    }
}
