// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using Peek.Common.Extensions;
using Peek.Common.Helpers;
using Peek.Common.Models;
using Peek.FilePreviewer.Models;
using Peek.FilePreviewer.Previewers.Interfaces;

namespace Peek.FilePreviewer.Previewers.Archives
{
    public partial class ArchivePreviewer : ObservableObject, IArchivePreviewer
    {
        [ObservableProperty]
        private PreviewState state;

        [ObservableProperty]
        private string sourcePath;

        private IFileSystemItem Item { get; }

        private DispatcherQueue Dispatcher { get; }

        public ArchivePreviewer(IFileSystemItem file)
        {
            Item = file;
            SourcePath = Item.Path;
            Dispatcher = DispatcherQueue.GetForCurrentThread();
        }

        public async Task CopyAsync()
        {
            await Dispatcher.RunOnUiThread(async () =>
            {
                var storageItem = await Item.GetStorageItemAsync();
                ClipboardHelper.SaveToClipboard(storageItem);
            });
        }

        public Task<PreviewSize> GetPreviewSizeAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new PreviewSize { MonitorSize = null });
        }

        public async Task LoadPreviewAsync(CancellationToken cancellationToken)
        {
            State = PreviewState.Loaded;
            await Task.CompletedTask;
        }

        public static bool IsFileTypeSupported(string fileExt)
        {
            return _supportedFileTypes.Contains(fileExt);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        private static readonly HashSet<string> _supportedFileTypes = new()
        {
            ".zip", ".rar", ".7z", ".tar", ".nupkg", ".jar", ".gz", ".tar", ".tar.gz", ".tgz",
        };
    }
}
