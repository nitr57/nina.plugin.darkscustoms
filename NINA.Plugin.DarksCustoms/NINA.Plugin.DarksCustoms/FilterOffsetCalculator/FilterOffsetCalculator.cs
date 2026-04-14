#region "copyright"

/*
    Copyright © 2016 - 2021 Stefan Berg <isbeorn86+NINA@googlemail.com> and the N.I.N.A. contributors

    This file is part of N.I.N.A. - Nighttime Imaging 'N' Astronomy.

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Core.Model.Equipment;
using NINA.Core.Utility;
using NINA.Core.Utility.WindowService;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Profile.Interfaces;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using NINA.WPF.Base.Interfaces;
using NINA.WPF.Base.Interfaces.Mediator;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace NINA.Plugin.DarksCustoms.FilterOffsetCalculator
{

    /// <summary>
    /// Filter Offset Calculator
    /// </summary>
    [ExportMetadata("Name", "Filter Offset Calculator")]
    [ExportMetadata("Description", "This item will calculate and store filter offsets")]
    [ExportMetadata("Icon", "Filter_Offset_SVG")]
    [ExportMetadata("Category", "Darks Customs")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class FilterOffsetCalculator : SequenceItem, IValidatable
    {
        private readonly IApplicationStatusMediator applicationStatusMediator;
        private readonly ICameraMediator cameraMediator;
        private readonly IFilterWheelMediator filterWheelMediator;
        private readonly IFocuserMediator focuserMediator;
        private readonly IProfileService profileService;
        private int _loops;
        private int _progress;
        private List<FilterInfo> selectedFilters;
        public IAutoFocusVMFactory AutoFocusVMFactory;
        public IWindowServiceFactory WindowServiceFactory;
        private ICommand _setUpFilters;

        /// <summary>
        /// The constructor marked with [ImportingConstructor] will be used to import and construct the object
        /// General device interfaces can be added to the constructor and will be automatically populated on import
        /// </summary>
        /// <remarks>
        /// Available interfaces to be injected:
        ///     - IProfileService,
        ///     - ICameraMediator,
        ///     - ITelescopeMediator,
        ///     - IFocuserMediator,
        ///     - IFilterWheelMediator,
        ///     - IGuiderMediator,
        ///     - IRotatorMediator,
        ///     - IFlatDeviceMediator,
        ///     - IWeatherDataMediator,
        ///     - IImagingMediator,
        ///     - IApplicationStatusMediator,
        ///     - INighttimeCalculator,
        ///     - IPlanetariumFactory,
        ///     - IImageHistoryVM,
        ///     - IDeepSkyObjectSearchVM,
        ///     - IDomeMediator,
        ///     - IImageSaveMediator,
        ///     - ISwitchMediator,
        ///     - IList of IDateTimeProvider
        /// </remarks>
        [ImportingConstructor]
        public FilterOffsetCalculator(IProfileService profileService, ICameraMediator cameraMediator,
            IFocuserMediator focuserMediator, IFilterWheelMediator filterWheelMediator,
            IApplicationStatusMediator applicationStatusMediator, IAutoFocusVMFactory autoFocusVMFactory)
        {
            this.profileService = profileService;
            this.cameraMediator = cameraMediator;
            this.focuserMediator = focuserMediator;
            this.filterWheelMediator = filterWheelMediator;
            this.applicationStatusMediator = applicationStatusMediator;
            AutoFocusVMFactory = autoFocusVMFactory;
            WindowServiceFactory = new WindowServiceFactory();
            Progress = 0;
            SelectedFilters = profileService.ActiveProfile?.FilterWheelSettings.FilterWheelFilters.ToList() ?? new List<FilterInfo>();
            Loops = 3;
            SetUpFilters = new AsyncCommand<Task>(async (obj) =>
            {
                var windowService = WindowServiceFactory.Create();

                var filterSelectionVM = new FilterSelectionVM(profileService.ActiveProfile.FilterWheelSettings.FilterWheelFilters.ToList(),
                    SelectedFilters.Count == 0 ? profileService.ActiveProfile.FilterWheelSettings.FilterWheelFilters.ToList() : SelectedFilters);

                await windowService.ShowDialog(filterSelectionVM, "Select Filters to generate offsets for");

                SelectedFilters.Clear();
                SelectedFilters.AddRange(filterSelectionVM.FilterWheelFilters.Where(f => f.Selected).Select(f => f.Filter));

                return Task.CompletedTask;
            }, (object o) => profileService.ActiveProfile.FilterWheelSettings.FilterWheelFilters.Count > 1);
        }

        public IList<string> Issues { get; set; } = new ObservableCollection<string>();

        [JsonProperty]
        public int Loops
        {
            get => _loops;
            set
            {
                _loops = value;
                RaisePropertyChanged();
            }
        }

        public List<FilterInfo> SelectedFilters
        {
            get => selectedFilters;
            set
            {
                selectedFilters = value;
                RaisePropertyChanged();
            }
        }

        public int Progress
        {
            get => _progress;
            set
            {
                _progress = value;
                RaisePropertyChanged();
            }
        }

        private void ResetAutofocusValues(bool useOffsets, FilterInfo defaultFocusFilter, IEnumerable<(string Name, short Position, int FocusOffset)> filterOffsets)
        {
            profileService.ActiveProfile.FocuserSettings.UseFilterWheelOffsets = useOffsets;
            if (defaultFocusFilter != null)
            {
                profileService.ActiveProfile.FilterWheelSettings.FilterWheelFilters.Single(f => f == defaultFocusFilter).AutoFocusFilter = true;
            }

            foreach (var offset in filterOffsets)
            {
                profileService.ActiveProfile.FilterWheelSettings.FilterWheelFilters.Single(f => f.Position == offset.Position).FocusOffset = offset.FocusOffset;
            }
        }

        /// <summary>
        /// When items are put into the sequence via the factory, the factory will call the clone method. Make sure all the relevant fields are cloned with the object.
        /// </summary>
        /// <returns></returns>
        public override object Clone()
        {
            return new FilterOffsetCalculator(profileService, cameraMediator, focuserMediator, filterWheelMediator, applicationStatusMediator, this.AutoFocusVMFactory)
            {
                Icon = Icon,
                Name = Name,
                Category = Category,
                Description = Description,
                Loops = Loops,
                SelectedFilters = SelectedFilters
            };
        }

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token)
        {
            var oldUseOffsets = profileService.ActiveProfile.FocuserSettings.UseFilterWheelOffsets;
            var oldDefaultFilter = profileService.ActiveProfile.FilterWheelSettings.FilterWheelFilters.SingleOrDefault(f => f.AutoFocusFilter);
            var oldOffsets = SelectedFilters.Select(f => (f.Name, f.Position, f.FocusOffset)).ToList();

            // no filters found
            if (oldOffsets.Count == 0)
            {
                return;
            }

            // set everything up
            profileService.ActiveProfile.FocuserSettings.UseFilterWheelOffsets = false;
            if (oldDefaultFilter != null)
            {
                profileService.ActiveProfile.FilterWheelSettings.FilterWheelFilters.Single(f => f == oldDefaultFilter).AutoFocusFilter = false;
            }
            foreach (var filter in SelectedFilters)
            {
                filter.FocusOffset = 0;
            }

            Dictionary<FilterInfo, List<int>> calculatedOffsets = new Dictionary<FilterInfo, List<int>>();
            var windowService = WindowServiceFactory.Create();

            // do the needful
            var autofocusVM = AutoFocusVMFactory.Create();

            windowService.Show(autofocusVM);

            for (Progress = 1; Progress <= Loops; Progress++)
            {
                var currentFilter = 0;
                var numberOfFilters = SelectedFilters.Count;
                foreach (var filter in SelectedFilters.OrderBy(f => f.Position))
                {
                    progress.Report(new ApplicationStatus()
                    {
                        Status = $"Running AutoFocus for Filter {filter.Name}",
                        Status2 = $"Iterations",
                        Progress2 = Progress,
                        MaxProgress2 = Loops,
                        ProgressType2 = ApplicationStatus.StatusProgressType.ValueOfMaxValue,
                        Status3 = "Filters",
                        Progress3 = ++currentFilter,
                        MaxProgress3 = numberOfFilters,
                        ProgressType3 = ApplicationStatus.StatusProgressType.ValueOfMaxValue,
                        Source = "Filter Offset Calculator"
                    });

                    await autofocusVM.StartAutoFocus(filter, token, progress);

                    var newPosition = focuserMediator.GetInfo().Position;
                    if (calculatedOffsets.ContainsKey(filter))
                    {
                        calculatedOffsets[filter].Add(newPosition);
                    }
                    else
                    {
                        calculatedOffsets.Add(filter, new List<int> { newPosition });
                    }

                    if (token.IsCancellationRequested)
                    {
                        break;
                    }
                }

                if (token.IsCancellationRequested)
                {
                    break;
                }
            }

            await windowService.Close();

            // reset everything on cancel or save new values
            if (token.IsCancellationRequested)
            {
                ResetAutofocusValues(oldUseOffsets, oldDefaultFilter, oldOffsets);
            }
            else
            {
                int temperatureDrift = 0;
                List<int> baseTempDrift = new List<int>();
                for (int i = 0; i < calculatedOffsets.First().Value.Count - 1; i++)
                {
                    baseTempDrift.Add(calculatedOffsets.First().Value[i] - calculatedOffsets.First().Value[i + 1]);
                }
                temperatureDrift = (int)(Math.Ceiling(baseTempDrift.Average()));

                int totalTime = (int)profileService.ActiveProfile.FilterWheelSettings.FilterWheelFilters
                  .Where(f => calculatedOffsets.Keys.Any(k => k.Position == f.Position)).Sum(p => p.AutoFocusExposureTime == new FilterInfo().AutoFocusExposureTime ?
                  profileService.ActiveProfile.FocuserSettings.AutoFocusExposureTime : p.AutoFocusExposureTime);

                List<FilterInfo> calculatedOffsetsFilterInfo = new List<FilterInfo>();
                var totalRatio = 0.0;

                foreach (KeyValuePair<FilterInfo, List<int>> filter in calculatedOffsets)
                {
                    var serviceFilter = profileService.ActiveProfile.FilterWheelSettings.FilterWheelFilters.Single(f => f.Position == filter.Key.Position);
                    var filterTime = serviceFilter.AutoFocusExposureTime == new FilterInfo().AutoFocusExposureTime
                        ? profileService.ActiveProfile.FocuserSettings.AutoFocusExposureTime : serviceFilter.AutoFocusExposureTime;

                    var filterRatio = filterTime / totalTime;

                    calculatedOffsetsFilterInfo.Add(new FilterInfo
                    {
                        Name = filter.Key.Name,
                        Position = filter.Key.Position,
                        FocusOffset = (int)Math.Ceiling(filter.Value.Average() + (totalRatio * temperatureDrift))
                    });

                    totalRatio += filterRatio;
                }

                var filterOffsetDialogVM = new FilterOffsetDialogVM(oldOffsets.Select(f => new FilterInfo() { Name = f.Name, Position = f.Position, FocusOffset = f.FocusOffset }).ToList(),
                                                                    calculatedOffsetsFilterInfo,
                                                                    oldDefaultFilter,
                                                                    null);

                await windowService.ShowDialog(filterOffsetDialogVM, "New Offsets Calculated", ResizeMode.NoResize, WindowStyle.ToolWindow);

                if (!filterOffsetDialogVM.Apply)
                {
                    ResetAutofocusValues(oldUseOffsets, oldDefaultFilter, oldOffsets);
                }
                else
                {
                    profileService.ActiveProfile.FocuserSettings.UseFilterWheelOffsets = true;
                    foreach (var offsets in filterOffsetDialogVM.NewOffsets)
                    {
                        profileService.ActiveProfile.FilterWheelSettings.FilterWheelFilters.Single(f => f.Position == offsets.Position).FocusOffset = offsets.FocusOffset;
                    }

                    var newDefaultFilter = filterOffsetDialogVM.NewDefaultFilter;
                    foreach (var filter in profileService.ActiveProfile.FilterWheelSettings.FilterWheelFilters)
                    {
                        filter.AutoFocusFilter = filter.Position == (newDefaultFilter?.Position ?? -1);
                    }

                    profileService.ActiveProfile.Save();
                }
            }

            progress.Report(new ApplicationStatus() { Status = string.Empty, Status2 = string.Empty, Source = "Filter Offset Calculator" });

            return;
        }

        public ICommand SetUpFilters
        {
            get => _setUpFilters;
            set
            {
                _setUpFilters = value;
                RaisePropertyChanged();
            }
        }

        /// <summary>
        /// This string will be used for logging
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return $"Category: {Category}, Item: {nameof(FilterOffsetCalculator)}, Loops: {Loops}";
        }

        public bool Validate()
        {
            Issues.Clear();
            bool focuserConnected = focuserMediator.GetInfo().Connected;
            bool cameraConnected = cameraMediator.GetInfo().Connected;
            bool filterWheelConnected = filterWheelMediator.GetInfo().Connected;

            if (!focuserConnected)
            {
                Issues.Add("Focuser is not connected");
            }
            if (!cameraConnected)
            {
                Issues.Add("Camera is not connected");
            }
            if (!filterWheelConnected)
            {
                Issues.Add("FilterWheel is not connected");
            }
            if (profileService.ActiveProfile.FilterWheelSettings.FilterWheelFilters.Count < 2)
            {
                Issues.Add("Less than 2 filters are present in the NINA options");
            }

            if (SelectedFilters.Count < 2)
            {
                Issues.Add("Less than 2 filters selected for offset calculation");
            }

            if (Loops < 2)
            {
                Issues.Add("At least 2 loops are required for offset calculation");
            }

            return focuserConnected && cameraConnected && filterWheelConnected && profileService.ActiveProfile.FilterWheelSettings.FilterWheelFilters.Count > 1 && SelectedFilters.Count > 1;
        }
    }
}