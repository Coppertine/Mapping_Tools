﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mapping_Tools.Classes.BeatmapHelper;
using Mapping_Tools.Classes.HitsoundStuff;
using Mapping_Tools.Classes.SystemTools;

namespace Mapping_Tools.Classes.Tools {
    public class RhythmGuide {
        public class RhythmGuideGeneratorArgs : BindableBase {
            #region private_members

            private string[] _paths = new string[0];
            private GameMode _outputGameMode = GameMode.Standard;
            private string _outputName = "Hitsounds";
            private bool _ncEverything;
            private SelectionMode _selectionMode = SelectionMode.HitsoundEvents;

            private ExportMode _exportMode = ExportMode.NewMap;
            private string _exportPath = Path.Combine(MainWindow.ExportPath, @"rhythm_guide.osu");

            #endregion

            public string[] Paths {
                get => _paths;
                set => Set(ref _paths, value);
            }
            public GameMode OutputGameMode {
                get => _outputGameMode;
                set => Set(ref _outputGameMode, value);
            }
            public string OutputName {
                get => _outputName;
                set => Set(ref _outputName, value);
            }
            public bool NcEverything {
                get => _ncEverything;
                set => Set(ref _ncEverything, value);
            }

            public SelectionMode SelectionMode {
                get => _selectionMode;
                set => Set(ref _selectionMode, value);
            }

            public ExportMode ExportMode {
                get => _exportMode;
                set => Set(ref _exportMode, value);
            }
            public string ExportPath {
                get => _exportPath;
                set => Set(ref _exportPath, value);
            }

            public override string ToString() {
                return $@"{Paths}, {ExportPath}, {ExportMode}, {OutputGameMode}, {OutputName}, {NcEverything}";
            }
        } 

        public enum ExportMode {
            NewMap,
            AddToMap,
        }

        public enum SelectionMode {
            AllEvents,
            HitsoundEvents
        }

        public static void GenerateRhythmGuide(RhythmGuideGeneratorArgs args) {
            if (args.ExportPath == null) {
                throw new ArgumentException("Export path can not be null.");
            }
            var editorRead = EditorReaderStuff.TryGetFullEditorReader(out var reader);
            switch (args.ExportMode) {
                case ExportMode.NewMap:
                    var beatmap = MergeBeatmaps(args.Paths.Select(o => editorRead ? EditorReaderStuff.GetNewestVersion(o, reader) : new BeatmapEditor(o)).Select(o => o.Beatmap).ToArray(),
                        args);

                    var editor = new Editor {TextFile = beatmap, Path = args.ExportPath};
                    editor.SaveFile();
                    System.Diagnostics.Process.Start(Path.GetDirectoryName(args.ExportPath) ??
                                                     throw new ArgumentException("Export path must be a file."));
                    break;
                case ExportMode.AddToMap:
                    var editor2 = EditorReaderStuff.GetNewestVersion(args.ExportPath, reader);
                    PopulateBeatmap(editor2.Beatmap,
                        args.Paths.Select(o => editorRead ? EditorReaderStuff.GetNewestVersion(o, reader) : new BeatmapEditor(o)).Select(o => o.Beatmap).ToArray(),
                        args);

                    editor2.SaveFile();
                    break;
                default:
                    return;
            }
        }

        private static Beatmap MergeBeatmaps(Beatmap[] beatmaps, RhythmGuideGeneratorArgs args) {
            if (beatmaps.Length == 0) {
                throw new ArgumentException("There must be at least one beatmap.");
            }

            // Scuffed beatmap copy
            var newBeatmap = new Beatmap(beatmaps[0].GetLines());

            // Remove all greenlines
            newBeatmap.BeatmapTiming.TimingPoints.RemoveAll(o => !o.Inherited);

            // Remove all hitobjects
            newBeatmap.HitObjects.Clear();

            // Change some parameters;
            newBeatmap.General["StackLeniency"] = new TValue("0.0");
            newBeatmap.General["Mode"] = new TValue(((int)args.OutputGameMode).ToString());
            newBeatmap.Metadata["Version"] = new TValue(args.OutputName);
            newBeatmap.Difficulty["CircleSize"] = new TValue("4");

            // Add hitobjects
            PopulateBeatmap(newBeatmap, beatmaps, args);

            return newBeatmap;
        }

        private static void PopulateBeatmap(Beatmap beatmap, IEnumerable<Beatmap> beatmaps, RhythmGuideGeneratorArgs args) {
            // Get the times from all beatmaps
            var times = new HashSet<double>();
            foreach (var b in beatmaps) {
                var timeline = b.GetTimeline();
                foreach (var timelineObject in timeline.TimelineObjects) {
                    // Handle different selection modes
                    switch (args.SelectionMode) {
                        case SelectionMode.AllEvents:
                            times.Add(b.BeatmapTiming.Resnap(timelineObject.Time, 16, 12));

                            break;
                        case SelectionMode.HitsoundEvents:
                            if (timelineObject.HasHitsound) {
                                times.Add(b.BeatmapTiming.Resnap(timelineObject.Time, 16, 12));
                            }

                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }

            // Generate hitcircles at those times
            foreach (var ho in times.Select(time => new HitObject(time, 0, SampleSet.Auto, SampleSet.Auto))) {
                ho.NewCombo = args.NcEverything;
                beatmap.HitObjects.Add(ho);
            }
        }
    }
}