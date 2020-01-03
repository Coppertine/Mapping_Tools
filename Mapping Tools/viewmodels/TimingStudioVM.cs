using Mapping_Tools.Classes.BeatmapHelper;
using Mapping_Tools.Classes.TimingStudio;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Mapping_Tools.Viewmodels
{
    public class TimingStudioVM : INotifyPropertyChanged
    {
        private int _currentTime;
        private string _baseBeatmap;
        private ObservableCollection<StudioTimingPoint> _timingPoints;
        /// <summary>
        /// The temporary view that is used for osu! view and export.
        /// </summary>
        private ObservableCollection<StudioTimingPoint> _osuViewTimingPoints = null;

        public string baseBeatmap
        {
            get => _baseBeatmap; set
            {
                if (_baseBeatmap != value)
                {
                    _baseBeatmap = value;
                    NotifyPropertyChanged("baseBeatmap");
                }
            }
        }

        /// <summary>
        /// The saved timing points used for the DAW view.
        /// </summary>
        public ObservableCollection<StudioTimingPoint> timingPoints
        {
            get => _timingPoints;
            set
            {
                if (_timingPoints != value)
                {
                    _timingPoints = value;
                    NotifyPropertyChanged("timingPoints");
                }
            }
        }

        public int currentTime
        {
            get => _currentTime; set
            {
                if (_currentTime != value)
                {
                    _currentTime = value;
                    NotifyPropertyChanged("currentTime");
                }
            }
        }


        public event PropertyChangedEventHandler PropertyChanged;

        public void NotifyPropertyChanged(string propName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        /// <summary>
        /// Imports all uninherited timing points (redline) into the DAW view, 
        /// they are not snapped to the concurrent beats on the timeline.
        /// </summary>
        /// <param name="importPath"></param>
        public void ImportTimingPointsFromBeatmap(string importPath)
        {
            var editor = new BeatmapEditor(importPath);
            var beatmap = editor.Beatmap;
            var UninheritedPoints = beatmap.BeatmapTiming.GetAllRedlines();

        }

        /// <summary>
        /// Snaps all uninherited timing points to the nearest snaping divisor in DAW View.
        /// </summary>
        public void SnapAllTimingPointsToBeat()
        {

        }

        /// <summary>
        /// Uses the beatmap string to export all osu! view timing points 
        /// </summary>
        /// <param name="exportPath"></param>
        public void ExportTimingPointsToBeatmap(string exportPath)
        {
            _osuViewTimingPoints is null ? _osuViewTimingPoints = GetOsuViewTimingPoints();
        }

        /// <summary>
        /// Convert all DAW View Timingpoints to osu! View Timingpoints
        /// </summary>
        /// <returns></returns>
        public ObservableCollection<StudioTimingPoint> GetOsuViewTimingPoints()
        {
            throw new NotImplementedException();
        }
    }
}
