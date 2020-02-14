﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Mapping_Tools.Classes.BeatmapHelper;
using Mapping_Tools.Classes.HitsoundStuff;
using Mapping_Tools.Classes.MathUtil;
using Mapping_Tools.Classes.SliderPathStuff;
using Mapping_Tools.Classes.SystemTools;
using Mapping_Tools.Classes.Tools;
using Mapping_Tools.Components.Dialogs;
using Mapping_Tools.Components.Graph;
using Mapping_Tools.Components.Graph.Interpolation;
using Mapping_Tools.Components.Graph.Markers;
using Mapping_Tools.Components.ObjectVisualiser;
using Mapping_Tools.Viewmodels;
using MaterialDesignThemes.Wpf;
using HitObject = Mapping_Tools.Classes.BeatmapHelper.HitObject;

namespace Mapping_Tools.Views {
    //[HiddenTool]
    public partial class SlideratorView : ISavable<SlideratorVm> {
        public static readonly string ToolName = "Sliderator";

        public static readonly string ToolDescription = "";

        private bool _ignoreAnchorsChange;

        public SlideratorView() {
            InitializeComponent();
            Width = MainWindow.AppWindow.content_views.Width;
            Height = MainWindow.AppWindow.content_views.Height;

            DataContext = new SlideratorVm();
            ViewModel.PropertyChanged += ViewModelOnPropertyChanged;

            Graph.VerticalMarkerGenerator = new DoubleMarkerGenerator(0, 1/4d);
            Graph.HorizontalMarkerGenerator = new DividedBeatMarkerGenerator(4);

            Graph.SetBrush(new SolidColorBrush(Color.FromArgb(255, 0, 255, 255)));

            Graph.MoveAnchorTo(Graph.Anchors[0], Vector2.Zero);
            Graph.MoveAnchorTo(Graph.Anchors[Graph.Anchors.Count - 1], Vector2.One);

            Graph.Anchors.CollectionChanged += AnchorsOnCollectionChanged;
            Graph.Anchors.AnchorsChanged += AnchorsOnAnchorsChanged;

            UpdateGraphModeStuff();
            UpdatePointsOfInterest();

            ProjectManager.LoadProject(this, message: false);
        }

        private SlideratorVm ViewModel => (SlideratorVm) DataContext;

        private void AnchorsOnAnchorsChanged(object sender, DependencyPropertyChangedEventArgs e) {
            if (_ignoreAnchorsChange) return;

            var anchor = (Anchor) sender;

            // Correct the anchor change if it resulted in a speed limit violation
            if (PrevOverSpeedLimit(anchor) || NextOverSpeedLimit(anchor)) {
                _ignoreAnchorsChange = true;
                Graph.IgnoreAnchorUpdates = true;

                // Use binary search to find the closest value to the limit
                const double d = 0.001;

                switch (e.NewValue) {
                    case double newDouble:
                        var oldDouble = (double) e.OldValue;

                        // Test if the old value is also a illegal speed violation
                        anchor.SetValue(e.Property, oldDouble);
                        if (PrevOverSpeedLimit(anchor) || NextOverSpeedLimit(anchor)) {
                            anchor.SetValue(e.Property, newDouble);
                            break;
                        }

                        anchor.SetValue(e.Property, BinarySearchUtil.DoubleBinarySearch(
                            oldDouble, newDouble, d,
                            mid => {
                                anchor.SetValue(e.Property, mid);
                                return !PrevOverSpeedLimit(anchor) && !NextOverSpeedLimit(anchor);
                            }));
                        break;
                    case Vector2 newVector2:
                        if (ViewModel.GraphMode == GraphMode.Position &&
                            (anchor.PreviousAnchor != null || anchor.NextAnchor != null)) {
                            // List of bounds. X represents the minimum Y value and Y represents the maximum Y value
                            // I use Vector2 here because it has usefull math methods
                            var bounds = new List<Vector2>();

                            if (anchor.PreviousAnchor != null) {
                                var maxSpeed = InterpolatorHelper.GetBiggestDerivative(anchor.Interpolator);

                                if (Math.Abs(newVector2.X - anchor.PreviousAnchor.Pos.X) < Precision.DOUBLE_EPSILON)
                                    bounds.Add(new Vector2(anchor.PreviousAnchor.Pos.Y));
                                else
                                    bounds.Add(new Vector2(anchor.PreviousAnchor.Pos.Y) +
                                               new Vector2(Precision.DOUBLE_EPSILON).PerpendicularRight +
                                               new Vector2(ViewModel.VelocityLimit * ViewModel.SvGraphMultiplier *
                                                           (newVector2.X - anchor.PreviousAnchor.Pos.X) / maxSpeed)
                                                   .PerpendicularLeft);
                            }

                            if (anchor.NextAnchor != null) {
                                var maxSpeed = InterpolatorHelper.GetBiggestDerivative(anchor.NextAnchor.Interpolator);

                                if (Math.Abs(newVector2.X - anchor.NextAnchor.Pos.X) < Precision.DOUBLE_EPSILON)
                                    bounds.Add(new Vector2(anchor.NextAnchor.Pos.Y));
                                else
                                    bounds.Add(new Vector2(anchor.NextAnchor.Pos.Y) +
                                               new Vector2(Precision.DOUBLE_EPSILON).PerpendicularRight +
                                               new Vector2(ViewModel.VelocityLimit * ViewModel.SvGraphMultiplier *
                                                           (newVector2.X - anchor.NextAnchor.Pos.X) / maxSpeed)
                                                   .PerpendicularRight);
                            }

                            // Clamp the new Y value between all the bounds
                            var newY = bounds.Aggregate(newVector2.Y,
                                (current, bound) => MathHelper.Clamp(current, bound.X, bound.Y));

                            // Break if the resulting value is not inside all the bounds
                            if (!bounds.All(b => newY >= b.X && newY <= b.Y)) break;

                            anchor.SetValue(e.Property, new Vector2(newVector2.X, newY));
                        }

                        break;
                }

                _ignoreAnchorsChange = false;
                Graph.IgnoreAnchorUpdates = false;
            }

            AnimateProgress(GraphHitObjectElement);
            UpdatePointsOfInterest();
        }

        private bool NextOverSpeedLimit(Anchor anchor) {
            if (anchor.NextAnchor == null) return false;

            var diff = anchor.NextAnchor.Pos - anchor.Pos;

            if (ViewModel.GraphMode == GraphMode.Position)
                return Math.Abs(InterpolatorHelper.GetBiggestDerivative(anchor.NextAnchor.Interpolator) * diff.Y /
                                diff.X)
                       / ViewModel.SvGraphMultiplier > ViewModel.VelocityLimit;
            return Math.Abs(InterpolatorHelper.GetBiggestValue(anchor.NextAnchor.Interpolator)) >
                   ViewModel.VelocityLimit;
        }

        private bool PrevOverSpeedLimit(Anchor anchor) {
            if (anchor.PreviousAnchor == null) return false;

            var diff = anchor.Pos - anchor.PreviousAnchor.Pos;

            if (ViewModel.GraphMode == GraphMode.Position)
                return Math.Abs(InterpolatorHelper.GetBiggestDerivative(anchor.Interpolator) * diff.Y / diff.X)
                       / ViewModel.SvGraphMultiplier > ViewModel.VelocityLimit;
            return Math.Abs(InterpolatorHelper.GetBiggestValue(anchor.Interpolator)) > ViewModel.VelocityLimit;
        }

        private void AnchorsOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            AnimateProgress(GraphHitObjectElement);
            UpdatePointsOfInterest();
        }

        private void ViewModelOnPropertyChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case nameof(ViewModel.ShowGraphAnchors):
                case nameof(ViewModel.ShowRedAnchors):
                    UpdatePointsOfInterest();
                    break;
                case nameof(ViewModel.VisibleHitObject):
                    AnimateProgress(GraphHitObjectElement);
                    UpdatePointsOfInterest();
                    break;
                case nameof(ViewModel.SvGraphMultiplier):
                case nameof(ViewModel.GraphDuration):
                    AnimateProgress(GraphHitObjectElement);
                    UpdatePointsOfInterest();
                    break;
                case nameof(ViewModel.BeatSnapDivisor):
                    Graph.HorizontalMarkerGenerator = new DividedBeatMarkerGenerator(ViewModel.BeatSnapDivisor);
                    break;
                case nameof(ViewModel.VelocityLimit):
                    if (ViewModel.GraphMode == GraphMode.Velocity) {
                        Graph.MinY = -ViewModel.VelocityLimit;
                        Graph.MaxY = ViewModel.VelocityLimit;
                    }

                    break;
                case nameof(ViewModel.GraphMode):
                    UpdateGraphModeStuff();
                    UpdatePointsOfInterest();
                    break;
            }
        }

        private void UpdateEverything() {
            UpdateGraphModeStuff();
            AnimateProgress(GraphHitObjectElement);
            UpdatePointsOfInterest();
            Graph.HorizontalMarkerGenerator = new DividedBeatMarkerGenerator(ViewModel.BeatSnapDivisor);
            Graph.Anchors.CollectionChanged += AnchorsOnCollectionChanged;
            Graph.Anchors.AnchorsChanged += AnchorsOnAnchorsChanged;
        }

        private void UpdatePointsOfInterest() {
            if ((ViewModel.ShowRedAnchors || ViewModel.ShowGraphAnchors) && ViewModel.VisibleHitObject != null && ViewModel.VisibleHitObject.IsSlider) {
                var sliderPath = ViewModel.VisibleHitObject.GetSliderPath();
                var maxCompletion = GetMaxCompletion();
                var hitObjectMarkers = new ObservableCollection<HitObjectElementMarker>();

                if (ViewModel.ShowRedAnchors) {
                    var redAnchorCompletions = SliderPathUtil.GetRedAnchorCompletions(sliderPath).ToArray();

                    // Add red anhors to hit object preview
                    foreach (var completion in redAnchorCompletions) {
                        hitObjectMarkers.Add(new HitObjectElementMarker(completion / maxCompletion, 0.2, Brushes.Red));
                    }

                    // Add red anchors to graph
                    if (ViewModel.GraphMode == GraphMode.Position) {
                        var markers = new ObservableCollection<GraphMarker>();

                        foreach (var completion in redAnchorCompletions) {
                            markers.Add(new GraphMarker {Orientation = Orientation.Horizontal, Value = completion,
                                CustomLineBrush = Brushes.Red, Text = null
                            });
                        }

                        Graph.ExtraMarkers = markers;
                    } else {
                        Graph.ExtraMarkers.Clear();
                    }
                }
                if (ViewModel.ShowGraphAnchors) {
                    // Add graph anchors to hit objects preview
                    var graphAnchorCompletions = ViewModel.GraphMode == GraphMode.Velocity
                        ? Graph.Anchors.Select(a => Graph.Anchors.GetIntegral(0, a.Pos.X) * ViewModel.SvGraphMultiplier)
                        : Graph.Anchors.Select(a => a.Pos.Y);

                    foreach (var completion in graphAnchorCompletions) {
                        hitObjectMarkers.Add(new HitObjectElementMarker(completion / maxCompletion, 0.2, Brushes.DodgerBlue));
                    }
                }

                GraphHitObjectElement.ExtraMarkers = hitObjectMarkers;

            } else {
                GraphHitObjectElement.ExtraMarkers.Clear();
                Graph.ExtraMarkers.Clear();
            }
        }

        private void AnimateProgress(HitObjectElement element) {
            if (ViewModel.VisibleHitObject == null) return;

            // Set the pixel length to the pixel length of the graph
            var maxCompletion = GetMaxCompletion();
            element.CustomPixelLength = maxCompletion * ViewModel.PixelLength;

            var graphDuration = ViewModel.GraphDuration;
            var extraDuration = graphDuration.Add(TimeSpan.FromSeconds(1));

            DoubleAnimationBase animation;
            if (ViewModel.GraphMode == GraphMode.Velocity)
                animation = new GraphIntegralDoubleAnimation {
                    GraphState = Graph.GetGraphState(), From = Graph.MinX, To = Graph.MaxX,
                    Duration = graphDuration,
                    BeginTime = TimeSpan.Zero,
                    // Here we use SvGraphMultiplier to get an accurate conversion from SV to slider completion per beat
                    // Completion = (100 * SliderMultiplier / PixelLength) * SV * Beats
                    Multiplier = ViewModel.SvGraphMultiplier / maxCompletion
                };
            else
                animation = new GraphDoubleAnimation {
                    GraphState = Graph.GetGraphState(), From = Graph.MinX, To = Graph.MaxX,
                    Duration = graphDuration,
                    BeginTime = TimeSpan.Zero,
                    Multiplier = 1 / maxCompletion
                };
            var animation2 = new DoubleAnimation(-1, -1, TimeSpan.FromSeconds(1)) {BeginTime = graphDuration};

            Storyboard.SetTarget(animation, element);
            Storyboard.SetTarget(animation2, element);
            Storyboard.SetTargetProperty(animation, new PropertyPath(HitObjectElement.ProgressProperty));
            Storyboard.SetTargetProperty(animation2, new PropertyPath(HitObjectElement.ProgressProperty));

            var timeline = new ParallelTimeline {RepeatBehavior = RepeatBehavior.Forever, Duration = extraDuration};
            timeline.Children.Add(animation);
            timeline.Children.Add(animation2);

            var storyboard = new Storyboard();
            storyboard.Children.Add(timeline);

            element.BeginStoryboard(storyboard);
        }

        private double GetMaxCompletion() {
            return GetMaxCompletion(ViewModel, Graph.Anchors);
        }

        private static double GetMaxCompletion(SlideratorVm viewModel, IReadOnlyList<IGraphAnchor> anchors) {
            double maxValue;
            if (viewModel.GraphMode == GraphMode.Velocity) // Integrate the graph to get the end value
                // Here we use SvGraphMultiplier to get an accurate conversion from SV to slider completion per beat
                // Completion = (100 * SliderMultiplier / PixelLength) * SV * Beats
                maxValue = AnchorCollection.GetMaxIntegral(anchors) * viewModel.SvGraphMultiplier;
            else
                maxValue = AnchorCollection.GetMaxValue(anchors);

            return maxValue;
        }

        private double GetMinCompletion() {
            return GetMinCompletion(ViewModel, Graph.Anchors);
        }

        private static double GetMinCompletion(SlideratorVm viewModel, IReadOnlyList<Anchor> anchors) {
            double minValue;
            if (viewModel.GraphMode == GraphMode.Velocity) // Integrate the graph to get the end value
                // Here we use SvGraphMultiplier to get an accurate conversion from SV to slider completion per beat
                // Completion = (100 * SliderMultiplier / PixelLength) * SV * Beats
                minValue = AnchorCollection.GetMinIntegral(anchors) * viewModel.SvGraphMultiplier;
            else
                minValue = AnchorCollection.GetMinValue(anchors);

            return minValue;
        }

        // Gets max velocity in SV
        private static double GetMaxVelocity(SlideratorVm viewModel, IReadOnlyList<IGraphAnchor> anchors) {
            double maxValue;
            if (viewModel.GraphMode == GraphMode.Velocity) // Integrate the graph to get the end value
                // Here we use SvGraphMultiplier to get an accurate conversion from SV to slider completion per beat
                // Completion = (100 * SliderMultiplier / PixelLength) * SV * Beats
                maxValue = Math.Max(AnchorCollection.GetMaxValue(anchors), -AnchorCollection.GetMinValue(anchors));
            else
                maxValue = Math.Max(AnchorCollection.GetMaxDerivative(anchors), -AnchorCollection.GetMinDerivative(anchors)) / viewModel.SvGraphMultiplier;

            return maxValue;
        }

        private async void ScaleCompleteButton_OnClick(object sender, RoutedEventArgs e) {
            var dialog = new TypeValueDialog(1);

            var result = await DialogHost.Show(dialog, "RootDialog");

            if (!(bool) result) return;
            if (!TypeConverters.TryParseDouble(dialog.ValueBox.Text, out var value)) return;

            var maxValue = GetMaxCompletion();
            if (Math.Abs(maxValue) < Precision.DOUBLE_EPSILON) return;
            Graph.ScaleAnchors(new Size(1, value / maxValue));
        }

        private void ClearButton_OnClick(object sender, RoutedEventArgs e) {
            var messageBoxResult = MessageBox.Show("Clear the graph?", "Confirm deletion", MessageBoxButton.YesNo);
            if (messageBoxResult != MessageBoxResult.Yes) return;

            Graph.Clear();
        }

        public void UpdateGraphModeStuff() {
            switch (ViewModel.GraphMode) {
                case GraphMode.Position:
                    GraphToggleContentTextBlock.Text = "X";
                    Graph.HorizontalAxisVisible = false;
                    Graph.VerticalAxisVisible = false;

                    // Make sure the start point is locked at y = 0
                    Graph.StartPointLockedY = true;
                    var firstAnchor = Graph.Anchors.FirstOrDefault();
                    if (firstAnchor != null) firstAnchor.Pos = new Vector2(firstAnchor.Pos.X, 0);

                    Graph.MinY = 0;
                    Graph.MaxY = 1;
                    Graph.VerticalMarkerGenerator = new DoubleMarkerGenerator(0, 1/4d);
                    break;
                case GraphMode.Velocity:
                    GraphToggleContentTextBlock.Text = "V";
                    Graph.HorizontalAxisVisible = true;
                    Graph.VerticalAxisVisible = false;
                    Graph.StartPointLockedY = false;

                    Graph.MinY = -ViewModel.VelocityLimit;
                    Graph.MaxY = ViewModel.VelocityLimit;
                    Graph.VerticalMarkerGenerator = new DoubleMarkerGenerator(0, 1/4d, "x");
                    break;
                default:
                    GraphToggleContentTextBlock.Text = "";
                    break;
            }

            AnimateProgress(GraphHitObjectElement);
        }

        private bool ValidateToolInput(out string message) {
            if (GetMinCompletion() < -1E-4) {
                message = "Negative position is illegal.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private async void Start_Click(object sender, RoutedEventArgs e) {
            if (!ValidateToolInput(out var message)) {
                var dialog = new MessageDialog(message);
                await DialogHost.Show(dialog, "RootDialog");
                return;
            }

            RunTool(MainWindow.AppWindow.GetCurrentMaps()[0]);
        }

        private void RunTool(string path, bool quick = false) {
            if (!CanRun) return;

            IOHelper.SaveMapBackup(path);

            ViewModel.Path = path;
            ViewModel.GraphState = Graph.GetGraphState();
            if (ViewModel.GraphState.CanFreeze) ViewModel.GraphState.Freeze();

            BackgroundWorker.RunWorkerAsync(ViewModel);
            CanRun = false;
        }

        protected override void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e) {
            var bgw = sender as BackgroundWorker;
            e.Result = Sliderate((SlideratorVm) e.Argument, bgw);
        }

        private string Sliderate(SlideratorVm arg, BackgroundWorker worker) {
            // Get slider path like from the hit object preview
            var sliderPath = new SliderPath(arg.VisibleHitObject.SliderType,
                arg.VisibleHitObject.GetAllCurvePoints().ToArray(), GetMaxCompletion(arg, arg.GraphState.Anchors) * arg.PixelLength);
            var path = new List<Vector2>();
            sliderPath.GetPathToProgress(path, 0, 1);

            // Update progressbar
            if (worker != null && worker.WorkerReportsProgress) worker.ReportProgress(10);

            // Get the highest velocity occuring in the graph
            double velocity = GetMaxVelocity(arg, arg.GraphState.Anchors); // Velocity is in SV
            // Do bad stuff to the velocity to make sure its the same SV as after writing it to .osu code
            velocity = -100 / double.Parse((-100 / velocity).ToInvariant(), CultureInfo.InvariantCulture);
            // Other velocity is in px / ms
            var otherVelocity = velocity * arg.SvGraphMultiplier * arg.PixelLength * arg.BeatsPerMinute / 60000;

            // make a position function for Sliderator
            Sliderator.PositionFunctionDelegate positionFunction;
            // We convert the graph GetValue function to a function that works like ms -> px
            // d is a value representing the number of milliseconds into the slider
            if (arg.GraphMode == GraphMode.Velocity)
                // Here we use SvGraphMultiplier to get an accurate conversion from SV to slider completion per beat
                // Completion = (100 * SliderMultiplier / PixelLength) * SV * Beats
                positionFunction = d =>
                    arg.GraphState.GetIntegral(0, d * arg.BeatsPerMinute / 60000) * arg.SvGraphMultiplier *
                    arg.PixelLength;
            else
                positionFunction = d => arg.GraphState.GetValue(d * arg.BeatsPerMinute / 60000) * arg.PixelLength;

            // Update progressbar
            if (worker != null && worker.WorkerReportsProgress) worker.ReportProgress(20);

            // Do Sliderator
            var sliderator = new Sliderator {
                PositionFunction = positionFunction, MaxT = arg.GraphBeats / arg.BeatsPerMinute * 60000,
                Velocity = otherVelocity
            };
            sliderator.SetPath(path);

            var slideration = sliderator.Sliderate();

            // Update progressbar
            if (worker != null && worker.WorkerReportsProgress) worker.ReportProgress(60);

            // Exporting stuff
            var editor = new BeatmapEditor(arg.Path);
            var beatmap = editor.Beatmap;
            var timing = beatmap.BeatmapTiming;

            // Get hit object that might be present at the export time or make a new one
            var hitObjectHere = beatmap.HitObjects.FirstOrDefault(o => Math.Abs(arg.ExportTime - o.Time) < 5) ??
                                new HitObject(arg.ExportTime, 0, SampleSet.Auto, SampleSet.Auto);

            // Clone the hit object to not affect the already existing hit object instance with changes
            var clone = new HitObject(hitObjectHere.GetLine()) {
                IsCircle = false, IsSpinner = false, IsHoldNote = false, IsSlider = true
            };

            // Give the new hit object the sliderated anchors
            clone.SetSliderPath(new SliderPath(PathType.Bezier, slideration.ToArray()));
            clone.PixelLength = sliderator.MaxS;

            // Update progressbar
            if (worker != null && worker.WorkerReportsProgress) worker.ReportProgress(70);
            
            // Add hit object
            if (arg.ExportMode == ExportMode.Add) {
                beatmap.HitObjects.Add(clone);
            } else {
                beatmap.HitObjects.Remove(hitObjectHere);
                beatmap.HitObjects.Add(clone);
            }

            // Add SV
            clone.SliderVelocity = velocity;
            var timingPointsChanges = beatmap.HitObjects.Select(ho => {
                    var sv = ho.SliderVelocity;
                    var tp = timing.GetTimingPointAtTime(ho.Time).Copy();
                    tp.MpB = -100 / sv;
                    tp.Offset = ho.Time;
                    return new TimingPointsChange(tp, mpb: true);
                }).ToList();
            TimingPointsChange.ApplyChanges(timing, timingPointsChanges);

            // Update progressbar
            if (worker != null && worker.WorkerReportsProgress) worker.ReportProgress(80);

            beatmap.SortHitObjects();

            editor.SaveFile();

            // Complete progressbar
            if (worker != null && worker.WorkerReportsProgress) worker.ReportProgress(100);

            return "Done!";
        }

        public SlideratorVm GetSaveData() {
            ViewModel.GraphState = Graph.GetGraphState();
            if (ViewModel.GraphState.CanFreeze) ViewModel.GraphState.Freeze();

            return ViewModel;
        }

        public void SetSaveData(SlideratorVm saveData) {
            DataContext = saveData;
            Graph.SetGraphState(saveData.GraphState);
            UpdateEverything();
            ViewModel.PropertyChanged += ViewModelOnPropertyChanged;
        }
        
        public string AutoSavePath => Path.Combine(MainWindow.AppDataPath, "slideratorproject.json");

        public string DefaultSaveFolder => Path.Combine(MainWindow.AppDataPath, "Sliderator Projects");
    }
}