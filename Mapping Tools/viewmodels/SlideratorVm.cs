﻿using Mapping_Tools.Classes.BeatmapHelper;
using Mapping_Tools.Classes.MathUtil;
using Mapping_Tools.Classes.SystemTools;
using Mapping_Tools.Classes.Tools;
using Mapping_Tools.Components.Domain;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using Mapping_Tools.Components.Graph;
using Newtonsoft.Json;

namespace Mapping_Tools.Viewmodels {
    public class SlideratorVm : BindableBase {
        private ObservableCollection<HitObject> _loadedHitObjects;
        private HitObject _visibleHitObject;
        private int _visibleHitObjectIndex;
        private double _pixelLength;
        private double _globalSv;
        private double _graphBeats;
        private double _beatsPerMinute;
        private int _beatSnapDivisor;
        private TimeSpan _graphDuration;
        private double _svGraphMultiplier;
        private ImportMode _importMode;
        private double _exactTime;
        private Visibility _exactTimeBoxVisibility;
        private double _exportTime;
        private ExportMode _exportMode;
        private GraphMode _graphMode;
        private double _velocityLimit;
        private bool _showRedAnchors;
        private bool _showGraphAnchors;
        private bool _manualVelocity;
        private double _newVelocity;
        private double _minDendrite;
        private double _distanceTraveled;
        private bool _delegateToBpm;
        private bool _removeSliderTicks;

        #region Properties

        public ObservableCollection<HitObject> LoadedHitObjects {
            get => _loadedHitObjects;
            set => SetLoadedHitObjects(value);
        }

        [JsonIgnore]
        public HitObject VisibleHitObject {
            get => _visibleHitObject;
            set => SetCurrentHitObject(value);
        }

        public int VisibleHitObjectIndex {
            get => _visibleHitObjectIndex;
            set => SetCurrentHitObjectIndex(value);
        }
        
        [JsonIgnore]
        public double PixelLength {
            get => _pixelLength;
            set {
                if (Set(ref _pixelLength, value)) {
                    UpdateSvGraphMultiplier();
                }
            } 
        }

        public double GlobalSv {
            get => _globalSv;
            set {
                if (Set(ref _globalSv, value)) {
                    UpdateSvGraphMultiplier();
                    RaisePropertyChanged(nameof(ExpectedSegments));
                }
            } 
        }
        
        [JsonIgnore]
        public double GraphBeats {
            get => _graphBeats;
            set {
                if (Set(ref _graphBeats, value)) {
                    UpdateAnimationDuration();
                    RaisePropertyChanged(nameof(ExpectedSegments));
                }
            }
        }
        
        [JsonIgnore]
        public double BeatsPerMinute {
            get => _beatsPerMinute;
            set {
                if (Set(ref _beatsPerMinute, value)) {
                    UpdateAnimationDuration();
                }
            } 
        }

        public int BeatSnapDivisor {
            get => _beatSnapDivisor;
            set => Set(ref _beatSnapDivisor, value);
        }
        
        [JsonIgnore]
        public TimeSpan GraphDuration {
            get => _graphDuration;
            set => Set(ref _graphDuration, value);
        }
        
        [JsonIgnore]
        public double SvGraphMultiplier {
            get => _svGraphMultiplier;
            set {
                if (Set(ref _svGraphMultiplier, value)) {
                }
            }
        }

        public ImportMode ImportMode {
            get => _importMode;
            set => SetImportMode(value);
        }

        [JsonIgnore]
        public IEnumerable<ImportMode> ImportModes => Enum.GetValues(typeof(ImportMode)).Cast<ImportMode>();

        public double ExactTime {
            get => _exactTime;
            set => Set(ref _exactTime, value);
        }

        public Visibility ExactTimeBoxVisibility {
            get => _exactTimeBoxVisibility;
            set => Set(ref _exactTimeBoxVisibility, value);
        }

        public double ExportTime {
            get => _exportTime;
            set => Set(ref _exportTime, value);
        }

        public ExportMode ExportMode {
            get => _exportMode;
            set => Set(ref _exportMode, value);
        }
        
        [JsonIgnore]
        public IEnumerable<ExportMode> ExportModes => Enum.GetValues(typeof(ExportMode)).Cast<ExportMode>();

        public GraphMode GraphMode {
            get => _graphMode;
            set => Set(ref _graphMode, value);
        }

        public double VelocityLimit {
            get => _velocityLimit;
            set => Set(ref _velocityLimit, value);
        }

        public bool ShowRedAnchors {
            get => _showRedAnchors;
            set => Set(ref _showRedAnchors, value);
        }

        public bool ShowGraphAnchors {
            get => _showGraphAnchors;
            set => Set(ref _showGraphAnchors, value);
        }

        public bool ManualVelocity {
            get => _manualVelocity;
            set => Set(ref _manualVelocity, value);
        }

        public double NewVelocity {
            get => _newVelocity;
            set  {
                if (Set(ref _newVelocity, value)) {
                    RaisePropertyChanged(nameof(ExpectedSegments));
                }
            }
        }

        public double MinDendrite {
            get => _minDendrite;
            set {
                if (Set(ref _minDendrite, value)) {
                    RaisePropertyChanged(nameof(ExpectedSegments));
                }
            }
        }

        public double DistanceTraveled {
            get => _distanceTraveled;
            set {
                if (Set(ref _distanceTraveled, value)) {
                    RaisePropertyChanged(nameof(ExpectedSegments));
                }
            }
        }

        public int ExpectedSegments {
            get {
                var newLength = NewVelocity * 100 * GlobalSv * GraphBeats;
                return (int)((newLength - DistanceTraveled) / MinDendrite + DistanceTraveled / 10);
            }
        }

        public bool DelegateToBpm {
            get => _delegateToBpm;
            set => Set(ref _delegateToBpm, value);
        }

        public bool RemoveSliderTicks {
            get => _removeSliderTicks;
            set => Set(ref _removeSliderTicks, value);
        }

        [JsonIgnore]
        public CommandImplementation ImportCommand { get; }
        [JsonIgnore]
        public CommandImplementation MoveLeftCommand { get; }
        [JsonIgnore]
        public CommandImplementation MoveRightCommand { get; }
        [JsonIgnore]
        public CommandImplementation GraphToggleCommand { get; }

        public GraphState GraphState { get; set; }

        [JsonIgnore]
        public string Path { get; set; }

        #endregion

        public SlideratorVm() {
            LoadedHitObjects = new ObservableCollection<HitObject>();
            PixelLength = 100;
            BeatsPerMinute = 180;
            GlobalSv = 1.4;
            GraphBeats = 3;
            BeatSnapDivisor = 4;
            ImportMode = ImportMode.Selected;
            ExactTimeBoxVisibility = Visibility.Collapsed;
            VelocityLimit = 10;
            GraphMode = GraphMode.Position;
            ShowRedAnchors = false;
            ShowGraphAnchors = false;
            ManualVelocity = false;
            NewVelocity = 1;
            MinDendrite = 2;
            DistanceTraveled = 0;
            DelegateToBpm = false;
            RemoveSliderTicks = false;

            ImportCommand = new CommandImplementation(Import);
            MoveLeftCommand = new CommandImplementation(_ => {
                VisibleHitObjectIndex = MathHelper.Clamp(VisibleHitObjectIndex - 1, 0, LoadedHitObjects.Count - 1);
            });
            MoveRightCommand = new CommandImplementation(_ => {
                VisibleHitObjectIndex = MathHelper.Clamp(VisibleHitObjectIndex + 1, 0, LoadedHitObjects.Count - 1);
            });
            GraphToggleCommand = new CommandImplementation(ToggleGraphMode);
        }

        private void Import(object _) {
            try {
                bool editorRead = EditorReaderStuff.TryGetFullEditorReader(out var reader);
                string path = MainWindow.AppWindow.GetCurrentMaps()[0];
                BeatmapEditor editor = null;
                List<HitObject> markedObjects = null;

                switch (ImportMode) {
                    case ImportMode.Selected:
                        if (!editorRead) {
                            MessageBox.Show(EditorReaderStuff.SelectedObjectsReadFailText);
                        };

                        editor = EditorReaderStuff.GetBeatmapEditor(out var selected, reader);

                        if (editor == null) {
                            MessageBox.Show(EditorReaderStuff.SelectedObjectsReadFailText);
                        }

                        markedObjects = selected;
                        break;
                    case ImportMode.Bookmarked:
                        editor = new BeatmapEditor(path);
                        markedObjects = editor.Beatmap.GetBookmarkedObjects();
                        break;
                    case ImportMode.Time:
                        editor = new BeatmapEditor(path);
                        markedObjects =
                            new List<HitObject>(editor.Beatmap.GetHitObjectsWithRangeInRange(
                                ExactTime - 5,
                                ExactTime + 5));
                        break;
                }

                if (markedObjects == null || markedObjects.Count(o => o.IsSlider) == 0) return;

                LoadedHitObjects = new ObservableCollection<HitObject>(markedObjects.Where(s => s.IsSlider));

                if (editor != null) {
                    GlobalSv = editor.Beatmap.Difficulty["SliderMultiplier"].GetDouble();
                }
            } catch (Exception ex) {
                MessageBox.Show(ex.Message + ex.StackTrace, "Error");
            }
        }

        private void ToggleGraphMode(object _) {
            switch (GraphMode) {
                case GraphMode.Position:
                    GraphMode = GraphMode.Velocity;
                    break;
                case GraphMode.Velocity:
                    GraphMode = GraphMode.Position;
                    break;
                default:
                    GraphMode = GraphMode.Position;
                    break;
            }
        }

        private void SetLoadedHitObjects(ObservableCollection<HitObject> value) {
            if (!Set(ref _loadedHitObjects, value, nameof(LoadedHitObjects))) return;
            LoadedHitObjects.CollectionChanged += LoadedHitObjectsOnCollectionChanged;
            if (LoadedHitObjects.Count == 0) return;
            VisibleHitObject = LoadedHitObjects[0];
            VisibleHitObjectIndex = 0;
        }

        private void LoadedHitObjectsOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            if (VisibleHitObject == null && LoadedHitObjects.Count > 0) {
                VisibleHitObject = LoadedHitObjects[0];
                VisibleHitObjectIndex = 0;
            }
        }

        private void SetCurrentHitObject(HitObject value) {
            if (!Set(ref _visibleHitObject, value, nameof(VisibleHitObject))) return;
            if (VisibleHitObject.UnInheritedTimingPoint == null) return;
            BeatsPerMinute = VisibleHitObject.UnInheritedTimingPoint.GetBPM();
            GraphBeats = VisibleHitObject.TemporalLength * BeatsPerMinute / 60000;
            ExportTime = VisibleHitObject.Time;
            PixelLength = VisibleHitObject.PixelLength;
        }

        private void SetCurrentHitObjectIndex(int value) {
            if (!Set(ref _visibleHitObjectIndex, value, nameof(VisibleHitObjectIndex))) return;
            if (VisibleHitObjectIndex < 0 || VisibleHitObjectIndex >= LoadedHitObjects.Count) return;
            VisibleHitObject = LoadedHitObjects[VisibleHitObjectIndex];
        }

        private void UpdateAnimationDuration() {
            if (BeatsPerMinute < 1) return;
            GraphDuration = TimeSpan.FromMinutes(GraphBeats / BeatsPerMinute);
        }

        private void UpdateSvGraphMultiplier() {
            SvGraphMultiplier = 100 * GlobalSv / PixelLength;
        }

        private void SetImportMode(ImportMode value) {
            if (!Set(ref _importMode, value, nameof(ImportMode))) return;
            ExactTimeBoxVisibility = ImportMode == ImportMode.Time ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    public enum ImportMode {
        Selected,
        Bookmarked,
        Time
    }

    public enum ExportMode {
        Add,
        Override
    }

    public enum GraphMode {
        Position,
        Velocity
    }
}