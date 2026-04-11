using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.Services.Storage;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.SystemUtilities.StorageMaster;

// Tree node ViewModel for the folder hierarchy view

/// <summary>
/// Wraps a StorageItem directory node for display in the tree view.
/// Uses lazy-loading: children are populated only when the node is expanded.
/// </summary>
public class StorageTreeNodeViewModel : ViewModelBase
{
    private bool _isExpanded;
    private bool _isLoaded;

    public StorageItem Item { get; }
    public ObservableCollection<StorageTreeNodeViewModel> Children { get; } = [];

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetProperty(ref _isExpanded, value) && value && !_isLoaded)
                LoadChildren();
        }
    }

    public string DisplayText => $"{Item.Name}  ({Item.DisplaySize})";
    public double PercentOfParent { get; init; }

    public StorageTreeNodeViewModel(StorageItem item, double percentOfParent = 0)
    {
        Item            = item;
        PercentOfParent = percentOfParent;
        if (item.IsDirectory && item.Children.Count > 0)
            Children.Add(new StorageTreeNodeViewModel(
                new StorageItem { Name = "Loading...", IsDirectory = false }));
    }

    private void LoadChildren()
    {
        _isLoaded = true;
        Children.Clear();
        double parentSize = Item.TotalSizeBytes > 0 ? Item.TotalSizeBytes : 1;
        foreach (var child in Item.Children
            .Where(c => c.IsDirectory)
            .OrderByDescending(c => c.TotalSizeBytes))
        {
            double pct = child.TotalSizeBytes / parentSize * 100.0;
            Children.Add(new StorageTreeNodeViewModel(child, pct));
        }
    }
}

/// <summary>Wraps a DuplicateGroup for display in the duplicates view.</summary>
public class DuplicateGroupViewModel : ViewModelBase
{
    public DuplicateGroup Group { get; }
    public string Header =>
        $"{Group.Files.Count} copies  .  {Group.FileSizeFormatted} each  .  {Group.WastedFormatted} wasted";
    public string ConfidenceText => Group.Confidence switch
    {
        DuplicateConfidence.FullHash => "Verified full hash match",
        DuplicateConfidence.QuickHash => "Quick hash match",
        DuplicateConfidence.SizeOnly => "Size-only match",
        _ => "Unknown confidence",
    };
    public string PreviewSummary
    {
        get
        {
            var folders = Group.Files
                .Select(f => System.IO.Path.GetDirectoryName(f.FullPath) ?? "(unknown)")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(3);

            return $"Locations: {string.Join(" | ", folders)}";
        }
    }
    public string AgeSpreadSummary
    {
        get
        {
            if (Group.Files.Count == 0)
            {
                return "No file age information available.";
            }

            var oldest = Group.Files.Min(f => f.CreatedAt);
            var newest = Group.Files.Max(f => f.CreatedAt);
            return $"Oldest: {oldest:yyyy-MM-dd}  Newest: {newest:yyyy-MM-dd}";
        }
    }
    public ObservableCollection<DuplicateFileEntryViewModel> Files { get; }

    public DuplicateGroupViewModel(DuplicateGroup group)
    {
        Group = group;
        Files = new ObservableCollection<DuplicateFileEntryViewModel>(
            group.Files.Select(f => new DuplicateFileEntryViewModel(f, f == group.Original)));
    }
}

/// <summary>Wraps a single file in a duplicate group for display.</summary>
public class DuplicateFileEntryViewModel : ViewModelBase
{
    private bool _isMarkedForDeletion;
    public StorageItem File       { get; }
    public bool        IsOriginal { get; }
    public string Badge => IsOriginal ? "Original" : "Duplicate";
    public string Name  => File.Name;
    public string Path  => File.FullPath;
    public string Size  => File.DisplaySize;
    public string CreatedAt => File.CreatedAt.ToString("yyyy-MM-dd HH:mm");
    public bool IsMarkedForDeletion
    {
        get => _isMarkedForDeletion;
        set => SetProperty(ref _isMarkedForDeletion, value);
    }
    public DuplicateFileEntryViewModel(StorageItem file, bool isOriginal)
    {
        File                = file;
        IsOriginal          = isOriginal;
        _isMarkedForDeletion = !isOriginal;
    }
}

/// <summary>Wraps a CleanupRecommendation for display with selection state.</summary>
public class CleanupItemViewModel : ViewModelBase
{
    private bool _isSelected;
    public CleanupRecommendation Recommendation { get; }
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
    public string CategoryIcon => Recommendation.Category switch
    {
        CleanupCategory.TemporaryFiles  => "Temp",
        CleanupCategory.LargeStaleFiles => "Large",
        CleanupCategory.DuplicateFiles  => "Dupe",
        CleanupCategory.EmptyFolders    => "Empty",
        CleanupCategory.CacheLikeFiles  => "Cache",
        _                               => "Other"
    };
    public string RiskColor => Recommendation.Risk switch
    {
        CleanupRisk.Low    => "#4CAF50",
        CleanupRisk.Medium => "#FF9800",
        CleanupRisk.High   => "#F44336",
        _                  => "#9E9E9E"
    };
    public string Name      => Recommendation.Item.Name;
    public string ItemPath  => Recommendation.Item.FullPath;
    public string Size      => Recommendation.PotentialSavingsFormatted;
    public string Rationale => Recommendation.Rationale;
    public CleanupItemViewModel(CleanupRecommendation rec)
    {
        Recommendation = rec;
        _isSelected    = rec.IsSelected;
    }
}

/// <summary>Represents one row in the extension breakdown summary.</summary>
public class ExtensionSummaryItem
{
    public string Extension  { get; init; } = string.Empty;
    public int    FileCount  { get; init; }
    public long   TotalBytes { get; init; }
    public string TotalSize  { get; init; } = string.Empty;
    public double Percentage { get; init; }
    public string PercentFormatted => $"{Percentage:F1}%";
}

/// <summary>
/// Main ViewModel for the Storage Master tool.
/// Tab layout: 0=Overview, 1=Tree, 2=Files, 3=Duplicates, 4=Cleanup, 5=Reports, 6=Snapshots
/// </summary>
public class StorageMasterViewModel : ViewModelBase
{
    private const int MaxDisplayedFilteredFiles = 2000;

    private readonly IScanEngine                   _scanEngine;
    private readonly IDuplicateDetectionService    _duplicateService;
    private readonly ICleanupRecommendationService _cleanupService;
    private readonly ICleanupAutomationPolicyService _cleanupPolicyService;
    private readonly ISnapshotService              _snapshotService;
    private readonly IReportService                _reportService;
    private readonly IElevationService             _elevationService;
    private readonly IDriveAnalysisService         _driveService;
    private readonly IFolderPickerService          _folderPicker;
    private readonly IUserDialogService            _dialogService;
    private readonly IClipboardService             _clipboardService;
    private readonly IBackgroundTaskService        _backgroundTaskService;

    private Guid?                    _scanTaskId;
    private StorageItem?             _scanRoot;

    private bool   _isScanning;
    private bool   _hasScanResult;
    private int    _scanProgress;
    private string _scanStatusText  = "Select a drive or folder, then click Scan.";
    private string _scanCurrentPath = string.Empty;
    private string _scanSummary     = string.Empty;
    private bool   _isDuplicateScanRunning;
    private bool   _isCleanupAnalysing;

    private string           _searchText      = string.Empty;
    private long             _minSizeFilterMb = 0;
    private string           _extensionFilter = string.Empty;
    private bool             _showHiddenFiles = false;
    private bool             _showSystemFiles = false;
    private bool             _sortDescending  = true;
    private StorageSortField _sortField       = StorageSortField.Size;

    private string _cleanupSortColumn    = "Savings";
    private bool   _cleanupSortDescending = true;
    private bool   _isFilteredResultsTruncated;
    private int    _totalFilteredCount;
    private CleanupAutomationPolicyMode _selectedCleanupPolicyMode = CleanupAutomationPolicyMode.Balanced;
    private bool _includeMediumRiskInPolicy;
    private bool _includeHighRiskInPolicy;
    private bool _includeDuplicatesInPolicy = true;
    private string _policyMinimumSavingsMb = "1";
    private string _policyPreviewSummary = "No cleanup policy preview yet.";

    // Live-update interim counters (updated from progress callback during scan)
    private long _interimBytes;
    private int  _interimFiles;
    private int  _interimDirs;

    private int _selectedTabIndex = 0;

    private StorageSnapshot? _selectedSnapshot;
    private StorageSnapshot? _comparisonBaseline;
    private string           _snapshotLabel = string.Empty;
    private string           _selectedScanPath = string.Empty;

    public ObservableCollection<DriveInfoExtended>         Drives           { get; } = [];
    public ObservableCollection<StorageTreeNodeViewModel>  TreeRoots        { get; } = [];
    public ObservableCollection<StorageItem>               AllFiles         { get; } = [];
    public ObservableCollection<StorageItem>               FilteredFiles    { get; } = [];
    public ObservableCollection<DuplicateGroupViewModel>   DuplicateGroups  { get; } = [];
    public ObservableCollection<CleanupItemViewModel>      CleanupItems     { get; } = [];
    public ObservableCollection<StorageSnapshot>           Snapshots        { get; } = [];
    public ObservableCollection<ExtensionSummaryItem>      ExtensionSummary { get; } = [];

    public bool IsScanning
    {
        get => _isScanning;
        private set
        {
            SetProperty(ref _isScanning, value);
            OnPropertyChanged(nameof(IsNotScanning));
            OnPropertyChanged(nameof(CanScan));
        }
    }
    public bool IsNotScanning => !_isScanning;
    public bool CanScan       => !_isScanning && !string.IsNullOrEmpty(SelectedScanPath);
    public bool HasScanResult
    {
        get => _hasScanResult;
        private set => SetProperty(ref _hasScanResult, value);
    }
    public int ScanProgressValue
    {
        get => _scanProgress;
        private set => SetProperty(ref _scanProgress, value);
    }
    public string ScanStatusText
    {
        get => _scanStatusText;
        private set => SetProperty(ref _scanStatusText, value);
    }
    public string ScanCurrentPath
    {
        get => _scanCurrentPath;
        private set => SetProperty(ref _scanCurrentPath, value);
    }
    public string ScanSummary
    {
        get => _scanSummary;
        private set => SetProperty(ref _scanSummary, value);
    }
    public bool IsDuplicateScanRunning
    {
        get => _isDuplicateScanRunning;
        private set => SetProperty(ref _isDuplicateScanRunning, value);
    }
    public bool IsCleanupAnalysing
    {
        get => _isCleanupAnalysing;
        private set => SetProperty(ref _isCleanupAnalysing, value);
    }
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, value);
    }
    public string SelectedScanPath
    {
        get => _selectedScanPath;
        set
        {
            if (SetProperty(ref _selectedScanPath, value))
                OnPropertyChanged(nameof(CanScan));
        }
    }
    public string SearchText
    {
        get => _searchText;
        set { if (SetProperty(ref _searchText, value)) ApplyFilters(); }
    }
    public long MinSizeFilterMb
    {
        get => _minSizeFilterMb;
        set { if (SetProperty(ref _minSizeFilterMb, value)) ApplyFilters(); }
    }
    public string ExtensionFilter
    {
        get => _extensionFilter;
        set { if (SetProperty(ref _extensionFilter, value)) ApplyFilters(); }
    }
    public bool ShowHiddenFiles
    {
        get => _showHiddenFiles;
        set { if (SetProperty(ref _showHiddenFiles, value)) ApplyFilters(); }
    }
    public bool ShowSystemFiles
    {
        get => _showSystemFiles;
        set { if (SetProperty(ref _showSystemFiles, value)) ApplyFilters(); }
    }
    public bool   IsElevated           => _elevationService.IsElevated;
    public string ElevationStatusText  => _elevationService.IsElevated
        ? "Running as Administrator"
        : "Standard user - some locations may be inaccessible";
    public string ElevationStatusColor => _elevationService.IsElevated ? "#4CAF50" : "#FF9800";
    public StorageSnapshot? SelectedSnapshot
    {
        get => _selectedSnapshot;
        set => SetProperty(ref _selectedSnapshot, value);
    }
    public StorageSnapshot? ComparisonBaseline
    {
        get => _comparisonBaseline;
        set => SetProperty(ref _comparisonBaseline, value);
    }
    public string SnapshotLabel
    {
        get => _snapshotLabel;
        set => SetProperty(ref _snapshotLabel, value);
    }
    public string TotalScanSize      => _scanRoot != null
        ? _scanRoot.DisplaySize
        : (_isScanning && _interimBytes > 0 ? StorageItem.FormatBytes(_interimBytes) : "-");
    public long   TotalScanSizeBytes => _scanRoot?.TotalSizeBytes ?? _interimBytes;
    public int    TotalFileCount     => _scanRoot?.FileCount      ?? _interimFiles;
    public int    TotalDirCount      => _scanRoot?.DirectoryCount ?? _interimDirs;
    public long   TotalDuplicateWasted         => DuplicateGroups.Sum(g => g.Group.WastedBytes);
    public long   TotalCleanupSavings          => CleanupItems.Where(i => i.IsSelected).Sum(i => i.Recommendation.PotentialSavingsBytes);
    public string TotalDuplicateWastedFormatted => StorageItem.FormatBytes(TotalDuplicateWasted);
    public string TotalCleanupSavingsFormatted  => StorageItem.FormatBytes(TotalCleanupSavings);
    public bool IsFilteredResultsTruncated
    {
        get => _isFilteredResultsTruncated;
        private set => SetProperty(ref _isFilteredResultsTruncated, value);
    }
    public int TotalFilteredCount
    {
        get => _totalFilteredCount;
        private set => SetProperty(ref _totalFilteredCount, value);
    }
    public string FilteredResultsSummary =>
        BuildFilteredResultsSummary(FilteredFiles.Count, TotalFilteredCount);

    public IReadOnlyList<CleanupAutomationPolicyMode> CleanupPolicyModes { get; } = Enum.GetValues<CleanupAutomationPolicyMode>();

    public CleanupAutomationPolicyMode SelectedCleanupPolicyMode
    {
        get => _selectedCleanupPolicyMode;
        set => SetProperty(ref _selectedCleanupPolicyMode, value);
    }

    public bool IncludeMediumRiskInPolicy
    {
        get => _includeMediumRiskInPolicy;
        set => SetProperty(ref _includeMediumRiskInPolicy, value);
    }

    public bool IncludeHighRiskInPolicy
    {
        get => _includeHighRiskInPolicy;
        set => SetProperty(ref _includeHighRiskInPolicy, value);
    }

    public bool IncludeDuplicatesInPolicy
    {
        get => _includeDuplicatesInPolicy;
        set => SetProperty(ref _includeDuplicatesInPolicy, value);
    }

    public string PolicyMinimumSavingsMb
    {
        get => _policyMinimumSavingsMb;
        set => SetProperty(ref _policyMinimumSavingsMb, value);
    }

    public string PolicyPreviewSummary
    {
        get => _policyPreviewSummary;
        set => SetProperty(ref _policyPreviewSummary, value);
    }

    internal static string BuildFilteredResultsSummary(int displayedCount, int totalCount)
    {
        if (totalCount > displayedCount)
        {
            return $"Showing {displayedCount:N0} of {totalCount:N0} files. Refine filters to narrow results.";
        }

        return $"{displayedCount:N0} files";
    }

    public AsyncRelayCommand ScanCommand                  { get; }
    public RelayCommand      CancelScanCommand            { get; }
    public RelayCommand      BrowseFolderCommand          { get; }
    public AsyncRelayCommand ScanDuplicatesCommand        { get; }
    public AsyncRelayCommand AnalyseCleanupCommand        { get; }
    public AsyncRelayCommand SaveSnapshotCommand          { get; }
    public AsyncRelayCommand LoadSnapshotsCommand         { get; }
    public AsyncRelayCommand DeleteSnapshotCommand        { get; }
    public AsyncRelayCommand ExportFileCsvCommand         { get; }
    public AsyncRelayCommand ExportDuplicateCsvCommand    { get; }
    public AsyncRelayCommand ExportSummaryCommand         { get; }
    public AsyncRelayCommand ElevateCommand               { get; }
    public RelayCommand      CopyPathCommand              { get; }
    public RelayCommand      OpenInExplorerCommand        { get; }
    public AsyncRelayCommand DeleteSelectedCleanupCommand  { get; }
    public AsyncRelayCommand RecycleSelectedCleanupCommand { get; }
    public RelayCommand      SelectAllCleanupCommand      { get; }
    public RelayCommand      DeselectAllCleanupCommand    { get; }
    public RelayCommand      RefreshDrivesCommand         { get; }
    public AsyncRelayCommand CompareSnapshotsCommand      { get; }
    public RelayCommand      PreviewCleanupPolicyCommand  { get; }
    public RelayCommand      ApplyCleanupPolicyCommand    { get; }
    public AsyncRelayCommand ExecuteCleanupPolicyRecycleCommand { get; }

    public StorageMasterViewModel(
        IScanEngine                   scanEngine,
        IDuplicateDetectionService    duplicateService,
        ICleanupRecommendationService cleanupService,
        ICleanupAutomationPolicyService cleanupPolicyService,
        ISnapshotService              snapshotService,
        IReportService                reportService,
        IElevationService             elevationService,
        IDriveAnalysisService         driveService,
        IFolderPickerService          folderPicker,
        IUserDialogService            dialogService,
        IClipboardService             clipboardService,
        IBackgroundTaskService        backgroundTaskService)
    {
        _scanEngine       = scanEngine;
        _duplicateService = duplicateService;
        _cleanupService   = cleanupService;
        _cleanupPolicyService = cleanupPolicyService;
        _snapshotService  = snapshotService;
        _reportService    = reportService;
        _elevationService = elevationService;
        _driveService     = driveService;
        _folderPicker     = folderPicker;
        _dialogService    = dialogService;
        _clipboardService = clipboardService;
        _backgroundTaskService = backgroundTaskService;

        ScanCommand                   = new AsyncRelayCommand(_ => StartScanAsync(),             _ => CanScan);
        CancelScanCommand             = new RelayCommand(_ => CancelScan(),                       _ => _isScanning);
        BrowseFolderCommand           = new RelayCommand(_ => BrowseFolder());
        ScanDuplicatesCommand         = new AsyncRelayCommand(_ => ScanDuplicatesAsync(),        _ => HasScanResult && !IsDuplicateScanRunning);
        AnalyseCleanupCommand         = new AsyncRelayCommand(_ => AnalyseCleanupAsync(),        _ => HasScanResult && !IsCleanupAnalysing);
        SaveSnapshotCommand           = new AsyncRelayCommand(_ => SaveSnapshotAsync(),          _ => HasScanResult);
        LoadSnapshotsCommand          = new AsyncRelayCommand(_ => LoadSnapshotsAsync());
        DeleteSnapshotCommand         = new AsyncRelayCommand(_ => DeleteSnapshotAsync(),        _ => SelectedSnapshot != null);
        ExportFileCsvCommand          = new AsyncRelayCommand(_ => ExportFilesCsvAsync(),        _ => FilteredFiles.Count > 0);
        ExportDuplicateCsvCommand     = new AsyncRelayCommand(_ => ExportDuplicatesCsvAsync(),   _ => DuplicateGroups.Count > 0);
        ExportSummaryCommand          = new AsyncRelayCommand(_ => ExportSummaryAsync(),         _ => HasScanResult);
        ElevateCommand                = new AsyncRelayCommand(_ => ElevateAsync(),               _ => !IsElevated);
        CopyPathCommand               = new RelayCommand(CopyPath);
        OpenInExplorerCommand         = new RelayCommand(OpenInExplorer);
        DeleteSelectedCleanupCommand  = new AsyncRelayCommand(_ => DeleteCleanupItemsAsync(permanent: true),  _ => CleanupItems.Any(i => i.IsSelected));
        RecycleSelectedCleanupCommand = new AsyncRelayCommand(_ => DeleteCleanupItemsAsync(permanent: false), _ => CleanupItems.Any(i => i.IsSelected));
        SelectAllCleanupCommand       = new RelayCommand(_ => SetAllCleanupSelection(true));
        DeselectAllCleanupCommand     = new RelayCommand(_ => SetAllCleanupSelection(false));
        RefreshDrivesCommand          = new RelayCommand(_ => RefreshDrives());
        CompareSnapshotsCommand       = new AsyncRelayCommand(_ => CompareSnapshotsAsync(),      _ => SelectedSnapshot != null && ComparisonBaseline != null);
        PreviewCleanupPolicyCommand   = new RelayCommand(_ => PreviewCleanupPolicy(),             _ => CleanupItems.Count > 0);
        ApplyCleanupPolicyCommand     = new RelayCommand(_ => ApplyCleanupPolicySelection(),      _ => CleanupItems.Count > 0);
        ExecuteCleanupPolicyRecycleCommand = new AsyncRelayCommand(_ => ExecuteCleanupPolicyRecycleAsync(), _ => CleanupItems.Count > 0);

        RefreshDrives();
        _ = LoadSnapshotsAsync();
    }

    private async Task StartScanAsync()
    {
        if (string.IsNullOrEmpty(SelectedScanPath)) return;
        ClearScanResults();
        IsScanning        = true;
        HasScanResult     = false;
        ScanProgressValue = 0;
        ScanStatusText    = $"Scanning {SelectedScanPath}...";

        if (_scanTaskId is Guid previousTask)
        {
            _backgroundTaskService.CancelTask(previousTask, "Superseded by a new scan request.");
        }

        var taskId = _backgroundTaskService.BeginTask("Storage scan");
        _scanTaskId = taskId;
        var cancellationToken = _backgroundTaskService.GetCancellationToken(taskId);

        var options = new ScanOptions
        {
            IncludeHidden = ShowHiddenFiles || IsElevated,
            IncludeSystem = ShowSystemFiles || IsElevated,
        };
        var progress = new Progress<ScanProgress>(p =>
        {
            ScanStatusText    = $"Found {p.FilesFound:N0} files, {p.DirsFound:N0} folders - {p.BytesFormatted}";
            ScanCurrentPath   = TruncatePath(p.CurrentPath, 60);
            ScanProgressValue = (ScanProgressValue + 2) % 98 + 1;

            _backgroundTaskService.ReportProgress(taskId, new BackgroundTaskProgress
            {
                Percent = ScanProgressValue,
                Message = "Scanning storage",
                Detail = p.CurrentPath,
            });

            // Update interim counters so Overview stat cards reflect live progress
            _interimBytes = p.BytesCounted;
            _interimFiles = p.FilesFound;
            _interimDirs  = p.DirsFound;
            NotifyScanMetrics();
        });
        try
        {
            _scanRoot = await _scanEngine.ScanAsync(SelectedScanPath, options, progress, cancellationToken);
            PopulateScanResults(_scanRoot);
            HasScanResult     = true;
            ScanStatusText    = $"Scan complete - {_scanRoot.DisplaySize} in {_scanRoot.FileCount:N0} files, {_scanRoot.DirectoryCount:N0} folders";
            ScanProgressValue = 100;
            SelectedTabIndex  = 1;
            _backgroundTaskService.CompleteTask(taskId, "Storage scan completed.");
        }
        catch (OperationCanceledException)
        {
            ScanStatusText    = "Scan cancelled.";
            ScanProgressValue = 0;
            _backgroundTaskService.CancelTask(taskId, "Storage scan cancelled.");
        }
        catch (Exception ex)
        {
            ScanStatusText = $"Scan failed: {ex.Message}";
            _backgroundTaskService.FailTask(taskId, ex, "Storage scan failed.");
        }
        finally
        {
            IsScanning = false;
            _scanTaskId = null;
        }
    }

    private void CancelScan()
    {
        if (_scanTaskId is Guid taskId)
        {
            _backgroundTaskService.CancelTask(taskId, "Cancellation requested by user.");
        }
    }

    private void ClearScanResults()
    {
        TreeRoots.Clear();
        AllFiles.Clear();
        FilteredFiles.Clear();
        DuplicateGroups.Clear();
        CleanupItems.Clear();
        ExtensionSummary.Clear();
        ScanSummary     = string.Empty;
        ScanCurrentPath = string.Empty;
        IsFilteredResultsTruncated = false;
        TotalFilteredCount = 0;
        _interimBytes   = 0;
        _interimFiles   = 0;
        _interimDirs    = 0;
        NotifyScanMetrics();
    }

    private void PopulateScanResults(StorageItem root)
    {
        TreeRoots.Clear();
        var rootNode = new StorageTreeNodeViewModel(root, 100.0) { IsExpanded = true };
        TreeRoots.Add(rootNode);

        AllFiles.Clear();
        var fileList = new List<StorageItem>(Math.Max(root.FileCount, 1));
        CollectFiles(root, fileList);
        fileList.Sort((a, b) => b.SizeBytes.CompareTo(a.SizeBytes));
        foreach (var f in fileList) AllFiles.Add(f);

        BuildExtensionSummary(fileList, root.TotalSizeBytes);
        ApplyFilters();
        ScanSummary = _reportService.GenerateSummaryText(root);
        NotifyScanMetrics();
    }

    private void ApplyFilters()
    {
        if (AllFiles.Count == 0 && !_hasScanResult) return;
        FilteredFiles.Clear();
        IEnumerable<StorageItem> source = AllFiles;
        if (!string.IsNullOrWhiteSpace(_searchText))
            source = source.Where(f => f.FullPath.Contains(_searchText, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(_extensionFilter))
        {
            var ext = _extensionFilter.StartsWith('.') ? _extensionFilter : "." + _extensionFilter;
            source  = source.Where(f => f.Extension.Equals(ext, StringComparison.OrdinalIgnoreCase));
        }
        if (_minSizeFilterMb > 0)
            source = source.Where(f => f.SizeBytes >= _minSizeFilterMb * 1024 * 1024);
        if (!_showHiddenFiles) source = source.Where(f => !f.IsHidden);
        if (!_showSystemFiles) source = source.Where(f => !f.IsSystem);
        source = _sortField switch
        {
            StorageSortField.Name         => _sortDescending ? source.OrderByDescending(f => f.Name)         : source.OrderBy(f => f.Name),
            StorageSortField.LastModified => _sortDescending ? source.OrderByDescending(f => f.LastModified)  : source.OrderBy(f => f.LastModified),
            StorageSortField.Extension    => _sortDescending ? source.OrderByDescending(f => f.Extension)    : source.OrderBy(f => f.Extension),
            _                             => _sortDescending ? source.OrderByDescending(f => f.SizeBytes)    : source.OrderBy(f => f.SizeBytes),
        };
        var filtered = source.ToList();
        TotalFilteredCount = filtered.Count;
        IsFilteredResultsTruncated = TotalFilteredCount > MaxDisplayedFilteredFiles;
        foreach (var item in filtered.Take(MaxDisplayedFilteredFiles)) FilteredFiles.Add(item);
        OnPropertyChanged(nameof(FilteredResultsSummary));
    }

    /// <summary>
    /// Sorts the cleanup list by the given column header text.
    /// Clicking the same column again toggles ascending/descending.
    /// </summary>
    internal void SortCleanupByColumn(string columnHeader)
    {
        if (_cleanupSortColumn == columnHeader)
            _cleanupSortDescending = !_cleanupSortDescending;
        else
        {
            _cleanupSortColumn     = columnHeader;
            _cleanupSortDescending = columnHeader == "Savings"; // default descending for size
        }

        var sorted = SortCleanupItems(CleanupItems, _cleanupSortColumn, _cleanupSortDescending).ToList();
        CleanupItems.Clear();
        foreach (var item in sorted) CleanupItems.Add(item);
        NotifyScanMetrics();
    }

    private static IEnumerable<CleanupItemViewModel> SortCleanupItems(
        IEnumerable<CleanupItemViewModel> source, string column, bool desc) => column switch
    {
        "Cat"     => desc ? source.OrderByDescending(i => i.CategoryIcon) : source.OrderBy(i => i.CategoryIcon),
        "Name"    => desc ? source.OrderByDescending(i => i.Name)         : source.OrderBy(i => i.Name),
        "Reason"  => desc ? source.OrderByDescending(i => i.Rationale)    : source.OrderBy(i => i.Rationale),
        "Path"    => desc ? source.OrderByDescending(i => i.ItemPath)     : source.OrderBy(i => i.ItemPath),
        _         => desc ? source.OrderByDescending(i => i.Recommendation.PotentialSavingsBytes)
                          : source.OrderBy(i => i.Recommendation.PotentialSavingsBytes),
    };

    private async Task ScanDuplicatesAsync()
    {
        if (_scanRoot == null) return;
        IsDuplicateScanRunning = true;
        DuplicateGroups.Clear();
        ScanStatusText = "Scanning for duplicate files...";
        try
        {
           var progress = new Progress<int>(pct => ScanStatusText = $"Scanning for duplicates... {pct}%");
           var groups   = await _duplicateService.FindDuplicatesAsync(_scanRoot!.FullPath, progress, CancellationToken.None);
            foreach (var g in groups) DuplicateGroups.Add(new DuplicateGroupViewModel(g));
            ScanStatusText   = $"Duplicate scan complete - {groups.Count} groups, {StorageItem.FormatBytes(groups.Sum(g => g.WastedBytes))} wasted";
            NotifyScanMetrics();
            SelectedTabIndex = 3;
        }
        catch (Exception ex) { ScanStatusText = $"Duplicate scan failed: {ex.Message}"; }
        finally { IsDuplicateScanRunning = false; }
    }

    private async Task AnalyseCleanupAsync()
    {
        if (_scanRoot == null) return;
        IsCleanupAnalysing = true;
        CleanupItems.Clear();
        ScanStatusText = "Analysing cleanup opportunities...";
        try
        {
            IReadOnlyList<DuplicateGroup>? dupes = DuplicateGroups.Count > 0
                ? DuplicateGroups.Select(vm => vm.Group).ToList()
                : null;
            var recs = await _cleanupService.AnalyseAsync(_scanRoot, dupes, CancellationToken.None);
            foreach (var rec in recs) CleanupItems.Add(new CleanupItemViewModel(rec));
            ScanStatusText   = $"Cleanup analysis complete - {recs.Count} recommendations, up to {StorageItem.FormatBytes(recs.Sum(r => r.PotentialSavingsBytes))} recoverable";
            PreviewCleanupPolicy();
            NotifyScanMetrics();
            SelectedTabIndex = 4;
        }
        catch (Exception ex) { ScanStatusText = $"Cleanup analysis failed: {ex.Message}"; }
        finally { IsCleanupAnalysing = false; }
    }

    private async Task SaveSnapshotAsync()
    {
        if (_scanRoot == null) return;
        try
        {
            var snap = await _snapshotService.SaveSnapshotAsync(_scanRoot, SnapshotLabel);
            Snapshots.Insert(0, snap);
            ScanStatusText = $"Snapshot saved: {snap.DisplayLabel}";
            SnapshotLabel  = string.Empty;
        }
        catch (Exception ex) { ScanStatusText = $"Failed to save snapshot: {ex.Message}"; }
    }

    private async Task LoadSnapshotsAsync()
    {
        try
        {
            var snaps = await _snapshotService.LoadAllSnapshotsAsync();
            Snapshots.Clear();
            foreach (var s in snaps) Snapshots.Add(s);
        }
        catch { /* optional - silent */ }
    }

    private async Task DeleteSnapshotAsync()
    {
        if (SelectedSnapshot == null) return;
        if (!_dialogService.Confirm("Delete Snapshot", $"Delete snapshot '{SelectedSnapshot.DisplayLabel}'?")) return;
        try
        {
            await _snapshotService.DeleteSnapshotAsync(SelectedSnapshot.Id);
            Snapshots.Remove(SelectedSnapshot);
            SelectedSnapshot = null;
        }
        catch (Exception ex) { ScanStatusText = $"Failed to delete snapshot: {ex.Message}"; }
    }

    private async Task CompareSnapshotsAsync()
    {
        if (SelectedSnapshot == null || ComparisonBaseline == null) return;
        try
        {
            var cmp      = _snapshotService.Compare(ComparisonBaseline, SelectedSnapshot);
            ScanSummary  = BuildComparisonReport(cmp);
            SelectedTabIndex = 5;
        }
        catch (Exception ex) { ScanStatusText = $"Comparison failed: {ex.Message}"; }
        await Task.CompletedTask;
    }

    private async Task ExportFilesCsvAsync()
    {
        var path = GetSavePath("Storage_Files.csv", "CSV files (*.csv)|*.csv");
        if (path == null) return;
        await _reportService.SaveToCsvAsync(_reportService.ExportFilesToCsv(FilteredFiles), path);
        ScanStatusText = $"Exported {FilteredFiles.Count} files to {path}";
    }

    private async Task ExportDuplicatesCsvAsync()
    {
        var path = GetSavePath("Storage_Duplicates.csv", "CSV files (*.csv)|*.csv");
        if (path == null) return;
        await _reportService.SaveToCsvAsync(_reportService.ExportDuplicatesToCsv(DuplicateGroups.Select(vm => vm.Group)), path);
        ScanStatusText = $"Exported duplicates report to {path}";
    }

    private async Task ExportSummaryAsync()
    {
        if (_scanRoot == null) return;
        var path = GetSavePath("Storage_Summary.txt", "Text files (*.txt)|*.txt");
        if (path == null) return;
        IReadOnlyList<DuplicateGroup>? dupes = DuplicateGroups.Count > 0 ? DuplicateGroups.Select(vm => vm.Group).ToList() : null;
        await _reportService.SaveToTextAsync(_reportService.GenerateSummaryText(_scanRoot, dupes), path);
        ScanStatusText = $"Summary report saved to {path}";
    }

    private async Task DeleteCleanupItemsAsync(bool permanent)
    {
        var selected = CleanupItems.Where(i => i.IsSelected).ToList();
        if (!selected.Any()) return;
        var savings = StorageItem.FormatBytes(selected.Sum(i => i.Recommendation.PotentialSavingsBytes));
        var verb    = permanent ? "permanently delete" : "send to Recycle Bin";
        if (!_dialogService.Confirm(
            permanent ? "Confirm Permanent Deletion" : "Confirm Recycle",
            $"Are you sure you want to {verb} {selected.Count} items?\n"
          + $"Estimated freed space: {savings}\n\n"
          + $"{(permanent ? "WARNING: PERMANENT DELETION - cannot be undone!" : "Items will be sent to the Recycle Bin.")}"))
            return;
        int success = 0, failed = 0;
        var deletedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await Task.Run(() =>
        {
            foreach (var item in selected)
            {
                try
                {
                    ShellFileOperations.Delete(
                        item.Recommendation.Item.FullPath, recycle: !permanent);
                    lock (deletedPaths)
                    {
                        deletedPaths.Add(item.Recommendation.Item.FullPath);
                    }
                    success++;
                }
                catch
                {
                    failed++;
                }
            }
        });

        foreach (var item in selected.Where(i => deletedPaths.Contains(i.Recommendation.Item.FullPath)))
        {
            CleanupItems.Remove(item);
        }

        ScanStatusText = $"Deleted {success} items.{(failed > 0 ? $" {failed} failed." : string.Empty)}";
        NotifyScanMetrics();
    }

    private void SetAllCleanupSelection(bool selected)
    {
        foreach (var item in CleanupItems) item.IsSelected = selected;
        NotifyScanMetrics();
    }

    private void PreviewCleanupPolicy()
    {
        var plan = BuildCleanupPolicyPlan();
        PolicyPreviewSummary =
            $"Policy selects {plan.SelectedCount:N0} of {plan.TotalRecommendations:N0} recommendations " +
            $"for estimated savings of {plan.EstimatedSavingsFormatted}.";
        ScanStatusText = PolicyPreviewSummary;
    }

    private void ApplyCleanupPolicySelection()
    {
        var plan = BuildCleanupPolicyPlan();
        var selectedPaths = new HashSet<string>(
            plan.Selected.Select(r => r.Item.FullPath),
            StringComparer.OrdinalIgnoreCase);

        foreach (var item in CleanupItems)
        {
            item.IsSelected = selectedPaths.Contains(item.Recommendation.Item.FullPath);
        }

        PolicyPreviewSummary =
            $"Applied policy selection: {plan.SelectedCount:N0} items selected ({plan.EstimatedSavingsFormatted}).";
        ScanStatusText = PolicyPreviewSummary;
        NotifyScanMetrics();
    }

    private async Task ExecuteCleanupPolicyRecycleAsync()
    {
        ApplyCleanupPolicySelection();
        await DeleteCleanupItemsAsync(permanent: false);
    }

    private CleanupAutomationPolicyPlan BuildCleanupPolicyPlan()
    {
        _ = long.TryParse(PolicyMinimumSavingsMb, out var minMb);
        var options = new CleanupAutomationPolicyOptions
        {
            Mode = SelectedCleanupPolicyMode,
            IncludeMediumRisk = IncludeMediumRiskInPolicy,
            IncludeHighRisk = IncludeHighRiskInPolicy,
            IncludeDuplicateRecommendations = IncludeDuplicatesInPolicy,
            MinimumSavingsBytes = Math.Max(0, minMb) * 1024L * 1024L,
        };

        return _cleanupPolicyService.BuildPlan(CleanupItems.Select(c => c.Recommendation).ToList(), options);
    }

    private async Task ElevateAsync()
    {
        if (!_dialogService.Confirm(
            "Restart as Administrator",
            "Restart Windows Utility Pack as Administrator?\n\n"
          + "This enables access to protected system files and locations.\n\n"
          + "Your current scan results will not be preserved."))
            return;
        bool ok = await _elevationService.RestartElevatedAsync();
        if (!ok) ScanStatusText = "Elevation was declined or could not be initiated.";
    }

    private void RefreshDrives()
    {
        Drives.Clear();
        foreach (var d in _driveService.GetAllDrives()) Drives.Add(d);
        if (string.IsNullOrEmpty(SelectedScanPath) && Drives.Count > 0)
        {
            var first = Drives.FirstOrDefault(d => d.DriveType == System.IO.DriveType.Fixed) ?? Drives[0];
            SelectedScanPath = first.RootPath;
        }
    }

    private void BrowseFolder()
    {
        var path = _folderPicker.PickFolder("Select folder to scan");
        if (!string.IsNullOrEmpty(path)) SelectedScanPath = path;
    }

    private void CopyPath(object? parameter)
    {
        string? path = parameter switch
        {
            StorageItem item         => item.FullPath,
            CleanupItemViewModel vm  => vm.ItemPath,
            _                        => null
        };
        if (!string.IsNullOrEmpty(path))
        {
            _clipboardService.SetText(path);
            ScanStatusText = $"Copied: {path}";
        }
    }

    private void OpenInExplorer(object? parameter)
    {
        string? filePath = parameter switch
        {
            StorageItem item         => item.FullPath,
            CleanupItemViewModel vm  => vm.ItemPath,
            _                        => null
        };
        if (string.IsNullOrEmpty(filePath)) return;

        string? folder = File.Exists(filePath) ? Path.GetDirectoryName(filePath) : (Directory.Exists(filePath) ? filePath : null);
        if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
        {
            try { System.Diagnostics.Process.Start("explorer.exe", folder); }
            catch (Exception ex) { ScanStatusText = $"Could not open Explorer: {ex.Message}"; }
        }
    }

    private static void CollectFiles(StorageItem node, List<StorageItem> files)
    {
        foreach (var child in node.Children)
        {
            if (child.IsDirectory) CollectFiles(child, files);
            else files.Add(child);
        }
    }

    private void BuildExtensionSummary(List<StorageItem> files, long totalSize)
    {
        ExtensionSummary.Clear();
        if (totalSize <= 0) return;
        var grouped = files
            .GroupBy(f => string.IsNullOrEmpty(f.Extension) ? "(none)" : f.Extension)
            .Select(g => new ExtensionSummaryItem
            {
                Extension  = g.Key,
                FileCount  = g.Count(),
                TotalBytes = g.Sum(f => f.SizeBytes),
                TotalSize  = StorageItem.FormatBytes(g.Sum(f => f.SizeBytes)),
                Percentage = g.Sum(f => f.SizeBytes) / (double)totalSize * 100,
            })
            .OrderByDescending(e => e.TotalBytes)
            .Take(20);
        foreach (var item in grouped) ExtensionSummary.Add(item);
    }

    private string? GetSavePath(string defaultName, string filter)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog { FileName = defaultName, Filter = filter, DefaultExt = Path.GetExtension(defaultName) };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    private void NotifyScanMetrics()
    {
        OnPropertyChanged(nameof(TotalScanSize));
        OnPropertyChanged(nameof(TotalScanSizeBytes));
        OnPropertyChanged(nameof(TotalFileCount));
        OnPropertyChanged(nameof(TotalDirCount));
        OnPropertyChanged(nameof(TotalDuplicateWasted));
        OnPropertyChanged(nameof(TotalDuplicateWastedFormatted));
        OnPropertyChanged(nameof(TotalCleanupSavings));
        OnPropertyChanged(nameof(TotalCleanupSavingsFormatted));
    }

    private static string BuildComparisonReport(SnapshotComparison c)
    {
        var sb = new StringBuilder();
        sb.AppendLine("====================================================");
        sb.AppendLine("  STORAGE MASTER - SNAPSHOT COMPARISON");
        sb.AppendLine("====================================================");
        sb.AppendLine($"  Baseline : {c.Baseline.DisplayLabel}");
        sb.AppendLine($"  Current  : {c.Current.DisplayLabel}");
        sb.AppendLine("----------------------------------------------------");
        sb.AppendLine($"  Size Delta  : {c.SizeDeltaFormatted}");
        sb.AppendLine($"  File Delta  : {(c.FileDeltaCount >= 0 ? "+" : "")}{c.FileDeltaCount:N0} files");
        sb.AppendLine();
        sb.AppendLine("  FOLDER CHANGES (top 20):");
        foreach (var e in c.FolderGrowth.Take(20))
            sb.AppendLine($"    {e.DeltaFormatted,10}  {e.FolderName}");
        sb.AppendLine("====================================================");
        return sb.ToString();
    }

    private static string TruncatePath(string path, int maxLen) =>
        path.Length <= maxLen ? path : "..." + path[^(maxLen - 1)..];
}
