// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Peek.FilePreviewer.Previewers;
using Peek.FilePreviewer.Previewers.Archives;
using Peek.FilePreviewer.Previewers.Archives.Helpers;
using Peek.FilePreviewer.Previewers.Archives.Models;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using Windows.ApplicationModel.Resources;

namespace Peek.FilePreviewer.Controls
{
    public sealed partial class ArchiveControl : UserControl
    {
        private IconCache _iconCache = new();
        private int _directoryCount;
        private int _fileCount;

        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
            nameof(Source),
            typeof(string),
            typeof(ArchivePreviewer),
            new PropertyMetadata(null, new PropertyChangedCallback((d, e) => ((ArchiveControl)d).SourcePropertyChanged())));

        public static readonly DependencyProperty LoadingStateProperty = DependencyProperty.Register(
            nameof(LoadingState),
            typeof(PreviewState),
            typeof(ArchivePreviewer),
            new PropertyMetadata(PreviewState.Uninitialized));

        private ObservableCollection<ArchiveItem> _tree;

        public PreviewState? LoadingState
        {
            get { return (PreviewState)GetValue(LoadingStateProperty); }
            set { SetValue(LoadingStateProperty, value); }
        }

        public string? Source
        {
            get { return (string?)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        public ArchiveControl()
        {
            this.InitializeComponent();

            _tree = new ObservableCollection<ArchiveItem>();
        }

        private async void SourcePropertyChanged()
        {
            ArchivePreview.ItemsSource = null;
            DirectoryCount.Text = null;
            FileCount.Text = null;
            Size.Text = null;

            _iconCache = new();
            _tree.Clear();
            _directoryCount = 0;
            _fileCount = 0;

            if (Source == null)
            {
                return;
            }

            using var stream = File.OpenRead(Source);
            long extractedSize;

            if (Source.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) || Source.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase))
            {
                using var archive = ArchiveFactory.Open(stream);
                extractedSize = archive.TotalUncompressSize;
                stream.Seek(0, SeekOrigin.Begin);

                using var reader = ReaderFactory.Open(stream);
                while (reader.MoveToNextEntry())
                {
                    await AddEntryAsync(reader.Entry);
                }
            }
            else
            {
                using var archive = ArchiveFactory.Open(stream);
                extractedSize = archive.TotalUncompressSize;

                foreach (var entry in archive.Entries)
                {
                    await AddEntryAsync(entry);
                }
            }

            ArchivePreview.ItemsSource = _tree;

            var size = new FileInfo(Source).Length; // archive.TotalSize isn't accurate
            DirectoryCount.Text = string.Format(CultureInfo.CurrentCulture, ResourceLoader.GetForViewIndependentUse().GetString("Archive_Directory_Count"), _directoryCount);
            FileCount.Text = string.Format(CultureInfo.CurrentCulture, ResourceLoader.GetForViewIndependentUse().GetString("Archive_File_Count"), _fileCount);
            Size.Text = string.Format(CultureInfo.CurrentCulture, ResourceLoader.GetForViewIndependentUse().GetString("Archive_Size"), Common.Helpers.SizeHelper.GetHumanSize(size), Common.Helpers.SizeHelper.GetHumanSize(extractedSize));
        }

        private async Task AddEntryAsync(IEntry entry)
        {
            ArgumentNullException.ThrowIfNull(entry, nameof(entry));

            var levels = entry!.Key
                .Split('/', '\\')
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToArray();

            ArchiveItem? parent = null;
            for (var i = 0; i < levels.Length; i++)
            {
                var type = (!entry.IsDirectory && i == levels.Length - 1) ? ArchiveItemType.File : ArchiveItemType.Directory;

                var icon = type == ArchiveItemType.Directory
                    ? await _iconCache.GetDirectoryIconAsync()
                    : await _iconCache.GetFileExtIconAsync(entry.Key);

                var item = new ArchiveItem(levels[i], type, icon);

                if (type == ArchiveItemType.Directory)
                {
                    item.IsExpanded = parent == null; // Only the root level is expanded
                }
                else if (type == ArchiveItemType.File)
                {
                    item.Size = entry.Size;
                }

                if (parent == null)
                {
                    var existing = _tree.FirstOrDefault(e => e.Name == item.Name);
                    if (existing == null)
                    {
                        var index = GetIndex(_tree, item);
                        _tree.Insert(index, item);
                        CountItem(item);
                    }

                    parent = existing ?? _tree.First(e => e.Name == item.Name);
                }
                else
                {
                    var existing = parent.Children.FirstOrDefault(e => e.Name == item.Name);
                    if (existing == null)
                    {
                        var index = GetIndex(parent.Children, item);
                        parent.Children.Insert(index, item);
                        CountItem(item);
                    }

                    parent = existing ?? parent.Children.First(e => e.Name == item.Name);
                }
            }
        }

        private int GetIndex(ObservableCollection<ArchiveItem> collection, ArchiveItem item)
        {
            for (var i = 0; i < collection.Count; i++)
            {
                if (item.Type == collection[i].Type && string.Compare(collection[i].Name, item.Name, StringComparison.OrdinalIgnoreCase) > 0)
                {
                    return i;
                }
            }

            return item.Type switch
            {
                ArchiveItemType.Directory => collection.Count(e => e.Type == ArchiveItemType.Directory),
                ArchiveItemType.File => collection.Count,
                _ => 0,
            };
        }

        private void CountItem(ArchiveItem item)
        {
            if (item.Type == ArchiveItemType.Directory)
            {
                _directoryCount++;
            }
            else if (item.Type == ArchiveItemType.File)
            {
                _fileCount++;
            }
        }
    }
}
