﻿using AxoCover.Models;
using AxoCover.Models.Data;
using AxoCover.Models.Extensions;
using AxoCover.Properties;
using AxoCover.Views;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Input;

namespace AxoCover.ViewModels
{
  public class SettingsViewModel : ViewModel
  {
    private readonly IEditorContext _editorContext;
    private readonly IOutputCleaner _outputCleaner;
    private readonly ITestRunner _testRunner;

    public PackageManifest Manifest
    {
      get
      {
        return AxoCoverPackage.Manifest;
      }
    }

    public bool IsShowingLineCoverage
    {
      get
      {
        return LineCoverageAdornment.IsShowingLineCoverage;
      }
      set
      {
        LineCoverageAdornment.IsShowingLineCoverage = value;
        NotifyPropertyChanged(nameof(IsShowingLineCoverage));
      }
    }

    public bool IsShowingBranchCoverage
    {
      get
      {
        return LineCoverageAdornment.IsShowingBranchCoverage;
      }
      set
      {
        LineCoverageAdornment.IsShowingBranchCoverage = value;
        NotifyPropertyChanged(nameof(IsShowingBranchCoverage));
      }
    }

    public bool IsShowingExceptions
    {
      get
      {
        return LineCoverageAdornment.IsShowingExceptions;
      }
      set
      {
        LineCoverageAdornment.IsShowingExceptions = value;
        NotifyPropertyChanged(nameof(IsShowingExceptions));
      }
    }

    public bool IsShowingPartialCoverage
    {
      get
      {
        return LineCoverageAdornment.IsShowingPartialCoverage;
      }
      set
      {
        LineCoverageAdornment.IsShowingPartialCoverage = value;
        NotifyPropertyChanged(nameof(IsShowingPartialCoverage));
      }
    }

    private string _excludeAttributes = Settings.Default.ExcludeAttributes;
    public string ExcludeAttributes
    {
      get
      {
        return _excludeAttributes;
      }
      set
      {
        _excludeAttributes = value;
        Settings.Default.ExcludeAttributes = value;
        NotifyPropertyChanged(nameof(ExcludeAttributes));
      }
    }

    private string _excludeFiles = Settings.Default.ExcludeFiles;
    public string ExcludeFiles
    {
      get
      {
        return _excludeFiles;
      }
      set
      {
        _excludeFiles = value;
        Settings.Default.ExcludeFiles = value;
        NotifyPropertyChanged(nameof(ExcludeFiles));
      }
    }

    private string _excludeDirectories = Settings.Default.ExcludeDirectories;
    public string ExcludeDirectories
    {
      get
      {
        return _excludeDirectories;
      }
      set
      {
        _excludeDirectories = value;
        Settings.Default.ExcludeDirectories = value;
        NotifyPropertyChanged(nameof(ExcludeDirectories));
      }
    }

    private string _filters = Settings.Default.Filters;
    public string Filters
    {
      get
      {
        return _filters;
      }
      set
      {
        _filters = value;
        Settings.Default.Filters = value;
        NotifyPropertyChanged(nameof(Filters));
      }
    }

    private TestItemViewModel _testSolution;
    public TestItemViewModel TestSolution
    {
      get
      {
        return _testSolution;
      }
      set
      {
        _testSolution = value;
        NotifyPropertyChanged(nameof(TestSolution));
      }
    }

    private readonly ObservableEnumeration<string> _testSettingsFiles;
    public ObservableEnumeration<string> TestSettingsFiles
    {
      get
      {
        return _testSettingsFiles;
      }
    }

    private string _selectedTestSettings;
    public string SelectedTestSettings
    {
      get
      {
        return _selectedTestSettings;
      }
      set
      {
        _selectedTestSettings = value;
        NotifyPropertyChanged(nameof(SelectedTestSettings));
      }
    }

    public IEnumerable<string> TestRunners
    {
      get
      {
        return (_testRunner as IMultiplexer).Implementations;
      }
    }

    public string SelectedTestRunner
    {
      get
      {
        return (_testRunner as IMultiplexer).Implementation;
      }
      set
      {
        (_testRunner as IMultiplexer).Implementation = value;
        NotifyPropertyChanged(nameof(SelectedTestRunner));
      }
    }

    public ICommand OpenWebSiteCommand
    {
      get
      {
        return new DelegateCommand(p => Process.Start(Manifest.WebSite));
      }
    }

    public ICommand OpenLicenseDialogCommand
    {
      get
      {
        return new DelegateCommand(p =>
        {
          var dialog = new ViewDialog<TextView>()
          {
            Title = Manifest.Name + " " + Resources.License
          };
          dialog.View.ViewModel.Text = Manifest.License;
          dialog.ShowDialog();
        });
      }
    }

    public ICommand OpenReleaseNotesDialogCommand
    {
      get
      {
        return new DelegateCommand(p =>
        {
          var dialog = new ViewDialog<TextView>()
          {
            Title = Manifest.Name + " " + Resources.ReleaseNotes
          };
          dialog.View.ViewModel.Text = Manifest.ReleaseNotes;
          dialog.ShowDialog();
        });
      }
    }

    public ICommand CleanTestOutputCommand
    {
      get
      {
        return new DelegateCommand(async p =>
        {
          await _outputCleaner.CleanOutputAsync(p as TestOutputDescription);
          RefreshProjectSizes();
        });
      }
    }

    public ICommand OpenPathCommand
    {
      get
      {
        return new DelegateCommand(p => _editorContext.OpenPathInExplorer(p as string));
      }
    }

    public ICommand ClearTestSettingsCommand
    {
      get
      {
        return new DelegateCommand(
          p => SelectedTestSettings = null,
          p => SelectedTestSettings != null,
          p => ExecuteOnPropertyChange(p, nameof(SelectedTestSettings)));
      }
    }

    public ICommand NavigateToFileCommand
    {
      get
      {
        return new DelegateCommand(
          p =>
          {
            _editorContext.NavigateToFile(p as string);
          });
      }
    }

    public SettingsViewModel(IEditorContext editorContext, IOutputCleaner outputCleaner, ITestRunner testRunner)
    {
      _editorContext = editorContext;
      _outputCleaner = outputCleaner;
      _testRunner = testRunner;

      _testSettingsFiles = new ObservableEnumeration<string>(() =>
        _editorContext?.Solution.FindFiles(new Regex("^.*\\.testSettings$", RegexOptions.Compiled | RegexOptions.IgnoreCase)) ?? new string[0], StringComparer.OrdinalIgnoreCase.Compare);

      editorContext.BuildFinished += (o, e) => Refresh();
      editorContext.SolutionOpened += (o, e) => Refresh();
    }

    public async void RefreshProjectSizes()
    {
      if (TestSolution != null)
      {
        foreach (TestProjectViewModel testProject in TestSolution.Children.ToArray())
        {
          testProject.Output = await _outputCleaner.GetOutputFilesAsync(testProject.CodeItem as TestProject);
        }
      }
    }

    public void Refresh()
    {
      TestSettingsFiles.Refresh();
      RefreshProjectSizes();
    }
  }
}