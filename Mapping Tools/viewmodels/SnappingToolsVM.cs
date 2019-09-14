﻿using Mapping_Tools.Classes.BeatmapHelper;
using Mapping_Tools.Classes.MathUtil;
using Mapping_Tools.Classes.SnappingTools;
using Mapping_Tools.Classes.SnappingTools.RelevantObjectGenerators;
using Mapping_Tools.Classes.SystemTools;
using Mapping_Tools.Classes.Tools;
using Mapping_Tools.Views.SnappingTools;
using Process.NET;
using Process.NET.Memory;
using Process.NET.Windows;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Mapping_Tools.Viewmodels {
    public class SnappingToolsVm {
        public Hotkey SnapHotkey { get; set; }

        public ObservableCollection<RelevantObjectsGenerator> Generators { get; }
        private readonly List<IRelevantObject> _relevantObjects = new List<IRelevantObject>();
        private List<HitObject> _visibleObjects;
        private int _editorTime;

        private string _filter = "";
        public string Filter { get => _filter; set => SetFilter(value); }

        public bool ListenersEnabled {
            set {
                _updateTimer.IsEnabled = value;
                _configWatcher.EnableRaisingEvents = value;

                if (!value) {
                    _state = State.Disabled;
                    _overlay.Dispose();
                }
                else {
                    _state = State.LookingForProcess;
                }
            }
        }

        private readonly DispatcherTimer _updateTimer;
        private readonly DispatcherTimer _autoSnapTimer;

        private const double PointsBias = 3;

        private readonly CoordinateConverter _coordinateConverter;
        private readonly FileSystemWatcher _configWatcher;

        private SnappingToolsOverlay _overlay;
        private ProcessSharp _processSharp;
        private IWindow _osuWindow;

        private State _state;

        private enum State {
            Disabled,
            LookingForProcess,
            LookingForEditor,
            Active
        }

        public SnappingToolsVm() {
            //initiate SnappingToolsPreferences if it's null
            if (SettingsManager.Settings.SnappingToolsPreferences == null) { SettingsManager.Settings.SnappingToolsPreferences = new SnappingToolsPreferences(); }

            // Get all the RelevantObjectGenerators
            var interfaceType = typeof(RelevantObjectsGenerator);
            Generators = new ObservableCollection<RelevantObjectsGenerator>(AppDomain.CurrentDomain.GetAssemblies()
              .SelectMany(x => x.GetTypes())
              .Where(x => interfaceType.IsAssignableFrom(x) && !x.IsInterface && !x.IsAbstract)
              .Select(Activator.CreateInstance).OfType<RelevantObjectsGenerator>());

            // Add PropertyChanged event to all generators to listen for changes
            foreach (var gen in Generators) { gen.PropertyChanged += OnGeneratorPropertyChanged; }

            // Set up groups and filters
            CollectionView view = (CollectionView) CollectionViewSource.GetDefaultView(Generators);
            PropertyGroupDescription groupDescription = new PropertyGroupDescription("GeneratorType");
            view.GroupDescriptions.Add(groupDescription);
            view.Filter = UserFilter;

            // Set up timers for responding to hotkey presses and beatmap changes
            _updateTimer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(100) };
            _updateTimer.Tick += UpdateTimerTick;
            _autoSnapTimer = new DispatcherTimer(DispatcherPriority.Send) { Interval = TimeSpan.FromMilliseconds(16) };
            _autoSnapTimer.Tick += AutoSnapTimerTick;

            // Set up a coordinate converter for converting coordinates between screen and osu!
            _coordinateConverter = new CoordinateConverter();

            // Listen for changes in the osu! user config
            _configWatcher = new FileSystemWatcher();
            SetConfigWatcherPath(SettingsManager.Settings.OsuConfigPath);
            _configWatcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.Attributes | NotifyFilters.CreationTime;
            _configWatcher.Changed += OnChangedConfigWatcher;

            // Listen for changes in osu! user config path in the settings
            SettingsManager.Settings.PropertyChanged += OnSettingsChanged;

            _state = State.LookingForProcess;
        }

        private void OnDraw(object sender, DrawingContext context) {
            foreach (var obj in _relevantObjects) {
                obj.DrawYourself(context, _coordinateConverter);
            }
        }

        private void OnSettingsChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName != "OsuConfigPath") return;
            SetConfigWatcherPath(SettingsManager.Settings.OsuConfigPath);
            _coordinateConverter.ReadConfig();
        }

        private void SetConfigWatcherPath(string path) {
            try {
                _configWatcher.Path = Path.GetDirectoryName(path);
                _configWatcher.Filter = Path.GetFileName(path);
            }
            catch (Exception ex) { Console.WriteLine(@"Can't set ConfigWatcher Path/Filter: " + ex.Message); }
        }

        private void OnChangedConfigWatcher(object sender, FileSystemEventArgs e) {
            _coordinateConverter.ReadConfig();
        }

        private void OnGeneratorPropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == "IsActive" && _state == State.Active) {
                // Reload relevant objects when a generator gets enabled/disabled
                GenerateRelevantObjects();
                _overlay.OverlayWindow.InvalidateVisual();
            }
        }

        private void UpdateTimerTick(object sender, EventArgs e)
        {
            var reader = EditorReaderStuff.GetEditorReader();
            switch (_state) {
                case State.Disabled:
                    break;
                case State.LookingForProcess:
                    _updateTimer.Interval = TimeSpan.FromSeconds(5);

                    // Set up objects/overlay
                    var process = System.Diagnostics.Process.GetProcessesByName("osu!").FirstOrDefault();
                    if (process == null) {
                        return;
                    }

                    try {
                        reader.SetProcess();
                    }
                    catch (Win32Exception) {
                        return;
                    }

                    _processSharp = new ProcessSharp(process, MemoryType.Remote);
                    _overlay = new SnappingToolsOverlay { Converter = _coordinateConverter };

                    _osuWindow = _processSharp.WindowFactory.MainWindow;
                    _overlay.Initialize(_osuWindow);
                    _overlay.Enable();

                    _overlay.OverlayWindow.Draw += OnDraw;

                    _updateTimer.Interval = TimeSpan.FromSeconds(1);
                    _state = State.LookingForEditor;
                    break;
                case State.LookingForEditor:
                    _updateTimer.Interval = TimeSpan.FromSeconds(1);
                    if (reader.ProcessNeedsReload()) {
                        _state = State.LookingForProcess;
                        _overlay.Dispose();
                        return;
                    }

                    try {
                        if (!_osuWindow.Title.EndsWith(@".osu")) {
                            return;
                        }
                    }
                    catch (ArgumentException) {
                        _state = State.LookingForProcess;
                        _overlay.Dispose();
                        return;
                    }

                    try {
                        reader.FetchEditor();
                    }
                    catch {
                        return;
                    }

                    _updateTimer.Interval = TimeSpan.FromMilliseconds(100);
                    _state = State.Active;
                    break;
                case State.Active:
                    _updateTimer.Interval = TimeSpan.FromMilliseconds(100);

                    if (reader.ProcessNeedsReload()) {
                        ClearRelevantObjects();
                        _state = State.LookingForProcess;
                        _overlay.Dispose();
                        return;
                    }
                    if (reader.EditorNeedsReload()) {
                        ClearRelevantObjects();
                        _state = State.LookingForEditor;
                        return;
                    }

                    var editorTime = reader.EditorTime();
                    if (editorTime != _editorTime) {
                        _editorTime = editorTime;
                        UpdateRelevantObjects();
                    }

                    _overlay.Update();
                    PresentationSource source =
                        PresentationSource.FromVisual(MainWindow.AppWindow);
                    double dpiX =
                        source.CompositionTarget.TransformToDevice.M11;
                    double dpiY =
                        source.CompositionTarget.TransformToDevice.M22;
                    _coordinateConverter.OsuWindowPosition = new Vector2(_osuWindow.X, _osuWindow.Y);
                    var bounds = _coordinateConverter.GetEditorGridBox();
                    Console.WriteLine(bounds);
                    Console.WriteLine(System.Windows.Forms.Cursor.Position);
                    _overlay.OverlayWindow.Left = bounds.Left / dpiX + 0.2;
                    _overlay.OverlayWindow.Top = bounds.Top / dpiY + 0.2;
                    _overlay.OverlayWindow.Width = bounds.Width / dpiX + 0.2;
                    _overlay.OverlayWindow.Height = bounds.Height / dpiY + 0.2;

                    if (!_autoSnapTimer.IsEnabled && IsHotkeyDown(SnapHotkey)) {
                        _autoSnapTimer.Start();
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void ClearRelevantObjects() {
            if (_relevantObjects.Count == 0) return;
            _relevantObjects.Clear();
            _overlay.OverlayWindow.InvalidateVisual();
        }

        private List<HitObject> GetVisibleHitObjects()
        {
            if (!EditorReaderStuff.TryGetFullEditorReader(out var reader)) return new List<HitObject>();

            var hitObjects = EditorReaderStuff.GetHitObjects(reader);

            // Get the visible hitobjects using approach rate
            var approachTime = ApproachRateToMs(reader.ApproachRate);
            var thereAreSelected = reader.numSelected > 0;
            return hitObjects.Where(o => Math.Abs(o.Time - _editorTime) < approachTime && (!thereAreSelected || o.IsSelected)).ToList();
        }
      
        private void UpdateRelevantObjects()
        {
            var visibleObjects = GetVisibleHitObjects();
            
            if (_visibleObjects != null && visibleObjects.SequenceEqual(_visibleObjects, new HitObjectComparer()))
            {
                // Visible Objects didn't change. Return to avoid redundant updates
                return;
            }
            // Set the new hitobjects
            _visibleObjects = visibleObjects;

            GenerateRelevantObjects(visibleObjects);

            _overlay.OverlayWindow.InvalidateVisual();
        }
      
        private void GenerateRelevantObjects(List<HitObject> visibleObjects=null)
        {
            if (visibleObjects == null)
            {
                visibleObjects = GetVisibleHitObjects();
                _visibleObjects = visibleObjects;
            }

            // Get all the active generators
            var activeGenerators = Generators.Where(o => o.IsActive).ToList();

            // Reset the old RelevantObjects
            _relevantObjects.Clear();

            // Generate RelevantObjects based on the visible hitobjects
            foreach (var gen in activeGenerators.OfType<IGenerateRelevantObjectsFromHitObjects>()) {
                _relevantObjects.AddRange(gen.GetRelevantObjects(visibleObjects));
            }

            // Seperate the RelevantObjects
            var relevantPoints = new List<RelevantPoint>();
            var relevantLines = new List<RelevantLine>();
            var relevantCircles = new List<RelevantCircle>();

            foreach (var ro in _relevantObjects)
            {
                switch (ro)
                {
                    case RelevantPoint rp:
                        relevantPoints.Add(rp);
                        break;
                    case RelevantLine rl:
                        relevantLines.Add(rl);
                        break;
                    case RelevantCircle rc:
                        relevantCircles.Add(rc);
                        break;
                }
            }

            // Generate more RelevantObjects
            foreach (var gen in activeGenerators.OfType<IGenerateRelevantObjectsFromRelevantPoints>()) {
                _relevantObjects.AddRange(gen.GetRelevantObjects(relevantPoints));
            }
            foreach (var gen in activeGenerators.OfType<IGenerateRelevantObjectsFromRelevantLines>()) {
                _relevantObjects.AddRange(gen.GetRelevantObjects(relevantLines));
            }
            foreach (var gen in activeGenerators.OfType<IGenerateRelevantObjectsFromRelevantCircles>()) {
                _relevantObjects.AddRange(gen.GetRelevantObjects(relevantCircles));
            }
            foreach (var gen in activeGenerators.OfType<IGenerateRelevantObjectsFromRelevantObjects>()) {
                _relevantObjects.AddRange(gen.GetRelevantObjects(_relevantObjects));
            }
        }

        private static double ApproachRateToMs(double approachRate) {
            if (approachRate < 5) {
                return 1800 - 120 * approachRate;
            }

            return 1200 - 150 * (approachRate - 5);
        }

        private void AutoSnapTimerTick(object sender, EventArgs e) {
            if (!IsHotkeyDown(SnapHotkey)) {
                _autoSnapTimer.Stop();
                return;
            }

            // Move the cursor's Position
            // System.Windows.Forms.Cursor.Position = new Point();
            var cursorPoint = System.Windows.Forms.Cursor.Position;
            // CONVERT THIS CURSOR POSITION TO EDITOR POSITION
            var cursorPos = _coordinateConverter.ScreenToEditorCoordinate(new Vector2(cursorPoint.X, cursorPoint.Y));

            if (_relevantObjects.Count == 0)
                return;

            IRelevantObject nearest = null;
            double smallestDistance = double.PositiveInfinity;
            foreach (IRelevantObject o in _relevantObjects) {
                double dist = o.DistanceTo(cursorPos);
                if (o is RelevantPoint) // Prioritize points to be able to snap to intersections
                    dist -= PointsBias;
                if (dist < smallestDistance) {
                    smallestDistance = dist;
                    nearest = o;
                }
            }

            // CONVERT THIS TO CURSOR POSITION
            if (nearest == null) return;
            var nearestPoint = _coordinateConverter.EditorToScreenCoordinate(nearest.NearestPoint(cursorPos));
            System.Windows.Forms.Cursor.Position = new System.Drawing.Point((int) Math.Round(nearestPoint.X), (int) Math.Round(nearestPoint.Y));
        }

        private static bool IsHotkeyDown(Hotkey hotkey) {
            if (hotkey == null)
                return false;
            if (!Keyboard.IsKeyDown(hotkey.Key))
                return false;
            if (hotkey.Modifiers.HasFlag(ModifierKeys.Alt) && !(Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt)))
                return false;
            if (hotkey.Modifiers.HasFlag(ModifierKeys.Control) && !(Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)))
                return false;
            if (hotkey.Modifiers.HasFlag(ModifierKeys.Shift) && !(Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)))
                return false;
            if (hotkey.Modifiers.HasFlag(ModifierKeys.Windows) && !(Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin)))
                return false;

            return true;
        }

        private bool UserFilter(object item) {
            if (string.IsNullOrEmpty(Filter))
                return true;
            return ((RelevantObjectsGenerator) item).Name.IndexOf(Filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void SetFilter(string value) {
            _filter = value;
            CollectionViewSource.GetDefaultView(Generators).Refresh();
        }
    }
}
