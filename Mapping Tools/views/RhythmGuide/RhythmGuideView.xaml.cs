﻿using Mapping_Tools.Classes.SystemTools;
using Mapping_Tools.Viewmodels;
using System;
using System.ComponentModel;
using System.Windows;

namespace Mapping_Tools.Views.RhythmGuide {

    /// <summary>
    /// Interactielogica voor RhythmGuideView.xaml
    /// </summary>
    public partial class RhythmGuideView {
        private readonly BackgroundWorker backgroundWorker;
        private readonly RhythmGuideVm settings;

        public RhythmGuideView() {
            InitializeComponent();
            backgroundWorker = (BackgroundWorker) FindResource("BackgroundWorker");
            DataContext = settings = new RhythmGuideVm();
        }

        private void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e) {
            var bgw = sender as BackgroundWorker;
            e.Result = GenerateRhythmGuide((Classes.Tools.RhythmGuide.RhythmGuideGeneratorArgs) e.Argument, bgw, e);
        }

        private void BackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
            if( e.Error != null ) {
                MessageBox.Show($"{e.Error.Message}{Environment.NewLine}{e.Error.StackTrace}", "Error");
            }
            else {
                if (e.Result.ToString() != "")
                    MessageBox.Show(e.Result.ToString());
                Progress.Value = 0;
            }
            Start.IsEnabled = true;
        }

        private void BackgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e) {
            Progress.Value = e.ProgressPercentage;
        }

        private void Start_Click(object sender, RoutedEventArgs e) {
            foreach (var fileToCopy in settings.GuideGeneratorArgs.Paths) {
                IOHelper.SaveMapBackup(fileToCopy);
            }

            backgroundWorker.RunWorkerAsync(settings.GuideGeneratorArgs);
            Start.IsEnabled = false;
        }

        private static string GenerateRhythmGuide(Classes.Tools.RhythmGuide.RhythmGuideGeneratorArgs args, BackgroundWorker worker, DoWorkEventArgs _) {
            Console.WriteLine(args);
            Classes.Tools.RhythmGuide.GenerateRhythmGuide(args);

            // Complete progress bar
            if (worker != null && worker.WorkerReportsProgress) {
                worker.ReportProgress(100);
            }
            return args.ExportMode == Classes.Tools.RhythmGuide.ExportMode.NewMap ? "" : "Done!";
        }
    }
}