﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Mapping_Tools.Classes.HitsoundStuff;
using Mapping_Tools.Classes.SystemTools;
using Mapping_Tools.Viewmodels;
using NAudio.Wave;
using NAudio.Vorbis;
using System.Text;
using System.Globalization;

namespace Mapping_Tools.Views {
    /// <summary>
    /// Interactielogica voor HitsoundCopierView.xaml
    /// </summary>
    public partial class HitsoundStudioView : UserControl {
        private BackgroundWorker backgroundWorker;
        private HitsoundStudioVM Settings;

        private bool suppressEvents = false;

        private List<HitsoundLayer> selectedLayers;
        private HitsoundLayer selectedLayer;

        public HitsoundStudioView() {
            InitializeComponent();
            Width = MainWindow.AppWindow.content_views.Width;
            Height = MainWindow.AppWindow.content_views.Height;
            backgroundWorker = (BackgroundWorker) FindResource("backgroundWorker");
            Settings = new HitsoundStudioVM();
            DataContext = Settings;
            LayersList.SelectedIndex = 0;
            Num_Layers_Changed();
            GetSelectedLayers();
        }

        public HitsoundStudioVM GetSettings() {
            return Settings;
        }

        public void SetSettings(HitsoundStudioVM settings) {
            suppressEvents = true;

            Settings = settings;
            DataContext = Settings;

            suppressEvents = false;

            LayersList.SelectedIndex = 0;
            Num_Layers_Changed();
        }

        private void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e) {
            var bgw = sender as BackgroundWorker;
            Make_Hitsounds((Arguments) e.Argument, bgw, e);
        }

        private void BackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
            if( e.Error != null ) {
                MessageBox.Show(String.Format("{0}:{1}{2}", e.Error.Message, Environment.NewLine, e.Error.StackTrace), "Error");
            }
            else {
                progress.Value = 0;
            }
            start.IsEnabled = true;
            startish.IsEnabled = true;
        }

        private void BackgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e) {
            progress.Value = e.ProgressPercentage;
        }

        private struct Arguments {
            public string ExportFolder;
            public string BaseBeatmap;
            public Sample DefaultSample;
            public List<HitsoundLayer> HitsoundLayers;
            public bool Debug;
            public Arguments(string exportFolder, string baseBeatmap, Sample defaultSample, List<HitsoundLayer> hitsoundLayers, bool debug) {
                ExportFolder = exportFolder;
                BaseBeatmap = baseBeatmap;
                DefaultSample = defaultSample;
                HitsoundLayers = hitsoundLayers;
                Debug = debug;
            }
        }

        private void Make_Hitsounds(Arguments arg, BackgroundWorker worker, DoWorkEventArgs e) {
            if (arg.Debug) {
                // Convert the multiple layers into packages that have the samples from all the layers at one specific time
                List<SamplePackage> samplePackages = HitsoundConverter.ZipLayers(arg.HitsoundLayers, arg.DefaultSample);
                UpdateProgressBar(worker, 20);

                // Convert the packages to hitsounds that fit on an osu standard map
                CompleteHitsounds completeHitsounds = HitsoundConverter.GetCompleteHitsounds(samplePackages);
                UpdateProgressBar(worker, 40);

                int samples = 0;
                foreach (CustomIndex ci in completeHitsounds.CustomIndices) {
                    foreach (HashSet<SampleGeneratingArgs> h in ci.Samples.Values) {
                        if (h.Any(o => SampleImporter.ValidateSampleArgs(o))) {
                            samples++;
                        }
                    }
                }
                UpdateProgressBar(worker, 60);

                int greenlines = 0;
                int lastIndex = -1;
                foreach (Hitsound hit in completeHitsounds.Hitsounds) {
                    if (hit.CustomIndex != lastIndex) {
                        lastIndex = hit.CustomIndex;
                        greenlines++;
                    }
                }
                UpdateProgressBar(worker, 100);

                MessageBox.Show(String.Format("Number of sample indices: {0}, Number of samples: {1}, Number of greenlines: {2}", completeHitsounds.CustomIndices.Count, samples, greenlines));
            } 
            else {
                var loadedSamples = SampleImporter.ImportSamples(arg.HitsoundLayers.Select(o => o.SampleArgs));
                UpdateProgressBar(worker, 20);

                // Convert the multiple layers into packages that have the samples from all the layers at one specific time
                List<SamplePackage> samplePackages = HitsoundConverter.ZipLayers(arg.HitsoundLayers, arg.DefaultSample);
                UpdateProgressBar(worker, 40);

                // Convert the packages to hitsounds that fit on an osu standard map
                CompleteHitsounds completeHitsounds = HitsoundConverter.GetCompleteHitsounds(samplePackages, loadedSamples);
                UpdateProgressBar(worker, 60);

                // Delete all files in the export folder before filling it again
                DirectoryInfo di = new DirectoryInfo(arg.ExportFolder);
                foreach (FileInfo file in di.GetFiles()) {
                    file.Delete();
                }
                UpdateProgressBar(worker, 80);

                // Export the hitsound .osu and sound samples
                HitsoundExporter.ExportCompleteHitsounds(arg.ExportFolder, arg.BaseBeatmap, completeHitsounds, loadedSamples);
                UpdateProgressBar(worker, 99);

                // Open export folder
                Process.Start(arg.ExportFolder);
            }

            UpdateProgressBar(worker, 100);
        }

        private void UpdateProgressBar(BackgroundWorker worker, int progress) {
            if (worker != null && worker.WorkerReportsProgress) {
                worker.ReportProgress(progress);
            }
        }

        private void Startish_Click(object sender, RoutedEventArgs e) {
            backgroundWorker.RunWorkerAsync(new Arguments(MainWindow.AppWindow.ExportPath, Settings.BaseBeatmap, Settings.DefaultSample, Settings.HitsoundLayers.ToList(), true));
            startish.IsEnabled = false;
        }

        private void Start_Click(object sender, RoutedEventArgs e) {
            if (Settings.BaseBeatmap == null || Settings.DefaultSample == null) {
                MessageBox.Show("Please import a base beatmap and default hitsound first.");
                return;
            }
            backgroundWorker.RunWorkerAsync(new Arguments(MainWindow.AppWindow.ExportPath, Settings.BaseBeatmap, Settings.DefaultSample, Settings.HitsoundLayers.ToList(), false));
            start.IsEnabled = false;
        }

        private void GetSelectedLayers() {
            selectedLayers = new List<HitsoundLayer>();

            if (LayersList.SelectedItems.Count == 0) {
                selectedLayer = null;
                return;
            }

            foreach (HitsoundLayer hsl in LayersList.SelectedItems) {
                selectedLayers.Add(hsl);
            }

            selectedLayer = selectedLayers[0];
        }

        private void SelectedSamplePathBrowse_Click(object sender, RoutedEventArgs e) {
            try {
                string path = IOHelper.SampleFileDialog();
                if (path != "") {
                    SelectedSamplePathBox.Text = path;
                }
            } catch (Exception) { }
        }

        private void SelectedSourcePathBrowse_Click(object sender, RoutedEventArgs e) {
            try {
                string path = IOHelper.BeatmapFileDialog();
                if (path != "") {
                    SelectedSourcePathBox.Text = path;
                    }
            } catch (Exception) { }
        }

        private void SelectedSourcePathLoad_Click(object sender, RoutedEventArgs e) {
            try {
                string path = IOHelper.CurrentBeatmap();
                if (path != "") {
                    SelectedSourcePathBox.Text = path;
                }
            } catch (Exception) { }
        }

        private void DefaultSampleBrowse_Click(object sender, RoutedEventArgs e) {
            try {
                string path = IOHelper.SampleFileDialog();
                if (path != "") {
                    Settings.DefaultSample.SampleArgs.Path = path;
                    DefaultSamplePathBox.Text = path;
                    }
            } catch (Exception) { }
        }

        private void BaseBeatmapBrowse_Click(object sender, RoutedEventArgs e) {
            try {
                string path = IOHelper.BeatmapFileDialog();
                if (path != "") {
                    Settings.BaseBeatmap = path;
                    }
            } catch (Exception) { }
        }

        private void BaseBeatmapLoad_Click(object sender, RoutedEventArgs e) {
            try {
                string path = IOHelper.CurrentBeatmap();
                if (path != "") {
                    Settings.BaseBeatmap = path;
                    }
            } catch (Exception) { }
        }

        private void ReloadFromSource_Click(object sender, RoutedEventArgs e) {
            try {
                HashSet<string> paths = new HashSet<string>(selectedLayers.Select(o => o.ImportArgs.Path));
                List<HitsoundLayer> layers = new List<HitsoundLayer>();

                if (selectedLayers.Any(o => o.ImportArgs.ImportType == ImportType.Hitsounds)) {
                    foreach (string path in paths) {
                        layers.AddRange(HitsoundImporter.LayersFromHitsounds(path));
                    }
                }
                if (selectedLayers.Any(o => o.ImportArgs.ImportType == ImportType.MIDI)) {
                    foreach (string path in paths) {
                        layers.AddRange(HitsoundImporter.ImportMIDI(path));
                    }
                }

                foreach (HitsoundLayer hl in selectedLayers) {
                    try {
                        hl.Import(layers);
                    } catch (Exception) { }
                }
            } catch (Exception ex) { Console.WriteLine(ex.Message); Console.WriteLine(ex.StackTrace); }
        }

        private void LayersList_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (suppressEvents) return;

            GetSelectedLayers();
            UpdateEditingField();
        }

        private void UpdateEditingField() {
            if (selectedLayers.Count == 0) { return; }

            suppressEvents = true;

            // Populate the editing fields
            SelectedNameBox.Text = selectedLayers.AllToStringOrDefault(o => o.Name);
            SelectedSamplePathBox.Text = selectedLayers.AllToStringOrDefault(o => o.SampleArgs.Path);
            SelectedSampleSetBox.Text = selectedLayers.AllToStringOrDefault(o => o.SampleSetString);
            SelectedHitsoundBox.Text = selectedLayers.AllToStringOrDefault(o => o.HitsoundString);
            ImportTypeBox.Text = selectedLayers.AllToStringOrDefault(o => o.ImportArgs.ImportType);
            SelectedSourcePathBox.Text = selectedLayers.AllToStringOrDefault(o => o.ImportArgs.Path);
            SelectedXCoordBox.Text = selectedLayers.AllToStringOrDefault(o => o.ImportArgs.X, CultureInfo.InvariantCulture);
            SelectedYCoordBox.Text = selectedLayers.AllToStringOrDefault(o => o.ImportArgs.Y, CultureInfo.InvariantCulture);
            SelectedBankBox.Text = selectedLayers.AllToStringOrDefault(o => o.SampleArgs.Bank);
            SelectedPatchBox.Text = selectedLayers.AllToStringOrDefault(o => o.SampleArgs.Patch);
            SelectedInstrumentBox.Text = selectedLayers.AllToStringOrDefault(o => o.SampleArgs.Instrument);
            SelectedKeyBox.Text = selectedLayers.AllToStringOrDefault(o => o.SampleArgs.Key);
            SelectedLengthBox.Text = selectedLayers.AllToStringOrDefault(o => o.SampleArgs.Length, CultureInfo.InvariantCulture);
            SelectedVelocityBox.Text = selectedLayers.AllToStringOrDefault(o => o.SampleArgs.Velocity);
            TimesBox.Text = selectedLayers.AllToStringOrDefault(o => o.Times, HitsoundLayerExtension.DoubleListToStringConverter);

            // Update visibility
            if (selectedLayers.Any(o => o.ImportArgs.ImportType == ImportType.Stack)) {
                SelectedCoordinatePanel.Visibility = Visibility.Visible;
            } else {
                SelectedCoordinatePanel.Visibility = Visibility.Collapsed;
            }
            if (selectedLayers.Any(o => o.ImportArgs.ImportType == ImportType.MIDI)) {
                SelectedMIDIPanel.Visibility = Visibility.Visible;
            } else {
                SelectedMIDIPanel.Visibility = Visibility.Collapsed;
            }

            suppressEvents = false;
        }

        void HitsoundLayer_MouseDoubleClick(object sender, MouseButtonEventArgs e) {
            try {
                SampleGeneratingArgs args = selectedLayer.SampleArgs;
                var mainOutputStream = SampleImporter.ImportSample(args);

                WaveOutEvent player = new WaveOutEvent();

                player.Init(mainOutputStream.GetSampleProvider());
                player.PlaybackStopped += PlayerStopped;

                player.Play();
            } catch (Exception ex) { Console.WriteLine(ex.Message); Console.WriteLine(ex.StackTrace); }
        }

        void PlayerStopped(object sender, StoppedEventArgs e) {
            ((WaveOutEvent)sender).Dispose();
            GC.Collect();
        }

        private void Num_Layers_Changed() {
            if (Settings.HitsoundLayers.Count == 0) {
                FirstGrid.ColumnDefinitions[0].Width = new GridLength(0);
                EditPanel.IsEnabled = false;
            } else if (FirstGrid.ColumnDefinitions[0].Width.Value < 100) {
                FirstGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
                FirstGrid.ColumnDefinitions[2].Width = new GridLength(2, GridUnitType.Star);
                EditPanel.IsEnabled = true;
            }
        }

        private void Add_Click(object sender, RoutedEventArgs e) {
            try {
                HitsoundLayerImportWindow importWindow = new HitsoundLayerImportWindow(Settings.HitsoundLayers.Count);
                importWindow.ShowDialog();

                LayersList.SelectedItems.Clear();
                foreach (HitsoundLayer layer in importWindow.HitsoundLayers) {
                    if (layer != null) {
                        Settings.HitsoundLayers.Add(layer);
                        LayersList.SelectedItems.Add(layer);
                    }
                }
                
                RecalculatePriorities();
                Num_Layers_Changed();
                GetSelectedLayers();
            } catch (Exception ex) {
                MessageBox.Show(ex.Message);
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e) {
            try {
                // Ask for confirmation
                MessageBoxResult messageBoxResult = MessageBox.Show("Are you sure?", "Delete Confirmation", MessageBoxButton.YesNo);
                if (messageBoxResult != MessageBoxResult.Yes) { return; }

                if (selectedLayers.Count == 0 || selectedLayers == null) { return; }

                suppressEvents = true;

                int index = Settings.HitsoundLayers.IndexOf(selectedLayer);

                foreach (HitsoundLayer hsl in selectedLayers) {
                    Settings.HitsoundLayers.Remove(hsl);
                }
                suppressEvents = false;

                LayersList.SelectedIndex = Math.Max(Math.Min(index - 1, Settings.HitsoundLayers.Count - 1), 0);

                RecalculatePriorities();
                Num_Layers_Changed();

            } catch (Exception ex) { Console.WriteLine(ex.Message); Console.WriteLine(ex.StackTrace); }
        }

        private void Raise_Click(object sender, RoutedEventArgs e) {
            try {
                suppressEvents = true;

                int selectedIndex = Settings.HitsoundLayers.IndexOf(selectedLayer);
                List<HitsoundLayer> moveList = new List<HitsoundLayer>();
                foreach (HitsoundLayer hsl in selectedLayers) {
                    moveList.Add(hsl);
                }

                foreach (HitsoundLayer hsl in Settings.HitsoundLayers) {
                    if (moveList.Contains(hsl)) {
                        moveList.Remove(hsl);
                    }
                    else
                        break;
                }

                foreach (HitsoundLayer hsl in moveList) {
                    int index = Settings.HitsoundLayers.IndexOf(hsl);

                    //Dont move left if it is the first item in the list or it is not in the list
                    if (index <= 0)
                        continue;

                    //Swap with this item with the one to its left
                    Settings.HitsoundLayers.Remove(hsl);
                    Settings.HitsoundLayers.Insert(index - 1, hsl);
                }

                LayersList.SelectedItems.Clear();
                foreach (HitsoundLayer hsl in selectedLayers) {
                    LayersList.SelectedItems.Add(hsl);
                }

                suppressEvents = false;

                RecalculatePriorities();
                GetSelectedLayers();
            } catch (Exception ex) { Console.WriteLine(ex.Message); Console.WriteLine(ex.StackTrace); }
        }

        private void Lower_Click(object sender, RoutedEventArgs e) {
            try {
                suppressEvents = true;

                int selectedIndex = Settings.HitsoundLayers.IndexOf(selectedLayer);
                List<HitsoundLayer> moveList = new List<HitsoundLayer>();
                foreach (HitsoundLayer hsl in selectedLayers) {
                    moveList.Add(hsl);
                }

                for (int i = Settings.HitsoundLayers.Count - 1; i >= 0; i--) {
                    HitsoundLayer hsl = Settings.HitsoundLayers[i];
                    if (moveList.Contains(hsl)) {
                        moveList.Remove(hsl);
                    }
                    else
                        break;
                }

                for (int i = moveList.Count - 1; i >= 0; i--) {
                    HitsoundLayer hsl = moveList[i];
                    int index = Settings.HitsoundLayers.IndexOf(hsl);

                    //Dont move left if it is the first item in the list or it is not in the list
                    if (index >= Settings.HitsoundLayers.Count - 1)
                        continue;

                    //Swap with this item with the one to its left
                    Settings.HitsoundLayers.Remove(hsl);
                    Settings.HitsoundLayers.Insert(index + 1, hsl);
                }

                LayersList.SelectedItems.Clear();
                foreach (HitsoundLayer hsl in selectedLayers) {
                    LayersList.SelectedItems.Add(hsl);
                }

                suppressEvents = false;

                RecalculatePriorities();
                GetSelectedLayers();
            } catch (Exception ex) { Console.WriteLine(ex.Message); Console.WriteLine(ex.StackTrace); }
        }

        private void RecalculatePriorities() {
            for (int i = 0; i < Settings.HitsoundLayers.Count; i++) {
                Settings.HitsoundLayers[i].SetPriority(i);
            }
        }

        private void SelectedNameBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (suppressEvents) return;

            string t = (sender as TextBox).Text;
            foreach (HitsoundLayer hitsoundLayer in selectedLayers) {
                hitsoundLayer.Name = t;
            }
        }

        private void SelectedSamplePathBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (suppressEvents) return;

            string t = (sender as TextBox).Text;
            foreach (HitsoundLayer hitsoundLayer in selectedLayers) {
                hitsoundLayer.SampleArgs.Path = t;
            }
        }

        private void SelectedSampleSetBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (suppressEvents) return;

            string t = ((sender as ComboBox).SelectedItem as ComboBoxItem).Content.ToString();
            foreach (HitsoundLayer hitsoundLayer in selectedLayers) {
                hitsoundLayer.SampleSetString = t;
            }
        }

        private void SelectedHitsoundBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (suppressEvents) return;

            string t = ((sender as ComboBox).SelectedItem as ComboBoxItem).Content.ToString();
            foreach (HitsoundLayer hitsoundLayer in selectedLayers) {
                hitsoundLayer.HitsoundString = t;
            }
        }

        private void ImportTypeBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (suppressEvents) return;

            string t = ((sender as ComboBox).SelectedItem as ComboBoxItem).Content.ToString();
            var converter = new Components.Domain.ImportTypeToStringConverter();
            ImportType type = (ImportType)converter.ConvertBack(t, null, null, null);
            foreach (HitsoundLayer hitsoundLayer in selectedLayers) {
                hitsoundLayer.ImportArgs.ImportType = type;
            }
            UpdateEditingField();
        }

        private void SelectedSourcePathBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (suppressEvents) return;

            string t = (sender as TextBox).Text;
            foreach (HitsoundLayer hitsoundLayer in selectedLayers) {
                hitsoundLayer.ImportArgs.Path = t;
            }
        }

        private void SelectedXCoordBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (suppressEvents) return;

            double t = (sender as TextBox).GetDouble(-1);
            foreach (HitsoundLayer hitsoundLayer in selectedLayers) {
                hitsoundLayer.ImportArgs.X = t;
            }
        }

        private void SelectedYCoordBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (suppressEvents) return;

            double t = (sender as TextBox).GetDouble(-1);
            foreach (HitsoundLayer hitsoundLayer in selectedLayers) {
                hitsoundLayer.ImportArgs.Y = t;
            }
        }

        private void TimesBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (suppressEvents) return;
            if ((sender as TextBox).GetBindingExpression(TextBox.TextProperty).HasValidationError) return;

            try {
                List<double> t = (sender as TextBox).Text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(o => double.Parse(o)).OrderBy(o => o).ToList();

                foreach (HitsoundLayer hitsoundLayer in selectedLayers) {
                    hitsoundLayer.Times = t;
                }
            } catch (Exception ex) { Console.WriteLine(ex.Message); Console.WriteLine(ex.StackTrace); }
        }

        private void SelectedBankBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (suppressEvents) return;

            int t = (sender as TextBox).GetInt(-1);
            foreach (HitsoundLayer hitsoundLayer in selectedLayers) {
                hitsoundLayer.SampleArgs.Bank = t;
            }
        }

        private void SelectedPatchBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (suppressEvents) return;

            int t = (sender as TextBox).GetInt(-1);
            foreach (HitsoundLayer hitsoundLayer in selectedLayers) {
                hitsoundLayer.SampleArgs.Patch = t;
            }
        }

        private void SelectedInstrumentBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (suppressEvents) return;

            int t = (sender as TextBox).GetInt(-1);
            foreach (HitsoundLayer hitsoundLayer in selectedLayers) {
                hitsoundLayer.SampleArgs.Instrument = t;
            }
        }

        private void SelectedKeyBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (suppressEvents) return;

            int t = (sender as TextBox).GetInt(-1);
            foreach (HitsoundLayer hitsoundLayer in selectedLayers) {
                hitsoundLayer.SampleArgs.Key = t;
            }
        }

        private void SelectedLengthBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (suppressEvents) return;

            int t = (sender as TextBox).GetInt(-1);
            foreach (HitsoundLayer hitsoundLayer in selectedLayers) {
                hitsoundLayer.SampleArgs.Length = t;
            }
        }

        private void SelectedVelocityBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (suppressEvents) return;

            int t = (sender as TextBox).GetInt(-1);
            foreach (HitsoundLayer hitsoundLayer in selectedLayers) {
                hitsoundLayer.SampleArgs.Velocity = t;
            }
        }
    }
}