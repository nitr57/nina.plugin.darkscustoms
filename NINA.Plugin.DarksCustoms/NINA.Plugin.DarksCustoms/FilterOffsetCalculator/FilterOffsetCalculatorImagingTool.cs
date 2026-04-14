using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Interfaces.ViewModel;
using NINA.Profile.Interfaces;
using NINA.Sequencer.SequenceItem;
using NINA.WPF.Base.Interfaces;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.ViewModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace NINA.Plugin.DarksCustoms.FilterOffsetCalculator {
    [Export(typeof(IDockableVM))]
    public class FilterOffsetCalculatorImagingTool : DockableVM {
        private FilterOffsetCalculator calculator;
        private ICameraMediator cameraMediator;
        private IApplicationStatusMediator applicationStatusMediator;
        private CancellationTokenSource executeCTS;

        [ImportingConstructor]
        public FilterOffsetCalculatorImagingTool(IProfileService profileService,
                                                 ICameraMediator cameraMediator,
                                                 IFocuserMediator focuserMediator,
                                                 IFilterWheelMediator filterWheelMediator,
                                                 IApplicationStatusMediator applicationStatusMediator,
                                                 IAutoFocusVMFactory autoFocusVMFactory) : base(profileService) {
            Title = "Filter Offset Calculator";
            var dict = new ResourceDictionary();
            dict.Source = new Uri("Darks Customs;component/FilterOffsetCalculator/FilterOffsetCalculatorTemplate.xaml", UriKind.RelativeOrAbsolute); 
            ImageGeometry = (System.Windows.Media.GeometryGroup)dict["Filter_Offset_SVG"];
            ImageGeometry.Freeze();


            this.calculator = new FilterOffsetCalculator(profileService, cameraMediator, focuserMediator, filterWheelMediator, applicationStatusMediator, autoFocusVMFactory);
            this.cameraMediator = cameraMediator;
            this.applicationStatusMediator = applicationStatusMediator;

            FilterSelection = new FilterSelectionVM(
                profileService.ActiveProfile.FilterWheelSettings.FilterWheelFilters.ToList(),
                Calculator.SelectedFilters.Count == 0 ? profileService.ActiveProfile.FilterWheelSettings.FilterWheelFilters.ToList() : Calculator.SelectedFilters
            );
            profileService.ActiveProfile.FilterWheelSettings.FilterWheelFilters.CollectionChanged += FilterWheelFilters_CollectionChanged1;            
            AddEventHandlers();


            ExecuteCommand = new AsyncCommand<bool>(
                 async () => { using (executeCTS = new CancellationTokenSource()) { return await Execute(new Progress<ApplicationStatus>(p => Status = p), executeCTS.Token); } },
                 (object o) => { return ((calculator as FilterOffsetCalculator).Validate() && cameraMediator.IsFreeToCapture(this)); });
            CancelExecuteCommand = new RelayCommand((object o) => { try { executeCTS?.Cancel(); } catch (Exception) { } });
        }

        private void FilterWheelFilters_CollectionChanged1(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
            RemoveEventHandlers();
            FilterSelection = new FilterSelectionVM(
                profileService.ActiveProfile.FilterWheelSettings.FilterWheelFilters.ToList(),
                Calculator.SelectedFilters.Count == 0 ? profileService.ActiveProfile.FilterWheelSettings.FilterWheelFilters.ToList() : Calculator.SelectedFilters
            );
            AddEventHandlers();
        }

        private void AddEventHandlers() {
            foreach(var filter in FilterSelection.FilterWheelFilters) {
                filter.PropertyChanged += Filter_PropertyChanged;
            }
        }
        private void RemoveEventHandlers() {
            foreach (var filter in FilterSelection.FilterWheelFilters) {
                filter.PropertyChanged -= Filter_PropertyChanged;
            }
        }

        private void Filter_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(FilterInfoWrapper.Selected)) {
                Calculator.SelectedFilters.Clear();
                Calculator.SelectedFilters.AddRange(FilterSelection.FilterWheelFilters.Where(f => f.Selected).Select(f => f.Filter));
                Calculator.Validate();
            }
        }

        public IAsyncCommand ExecuteCommand { get; }

        public ICommand CancelExecuteCommand { get; }

        public FilterOffsetCalculator Calculator {
            get => calculator;
            set {
                calculator = value;
                RaisePropertyChanged();
            }
        }

        public FilterSelectionVM FilterSelection {
            get => filterSelection;
            set {
                filterSelection = value;
                RaisePropertyChanged();
            }
        }

        private ApplicationStatus status;
        private FilterSelectionVM filterSelection;

        public ApplicationStatus Status {
            get {
                return status;
            }
            set {
                status = value;
                if (string.IsNullOrWhiteSpace(status.Source)) {
                    status.Source = "Filter Offset Calculator Tool";
                }

                RaisePropertyChanged();

                applicationStatusMediator.StatusUpdate(status);
            }
        }

        public override bool IsTool { get; } = true;

        public async Task<bool> Execute(IProgress<ApplicationStatus> externalProgress, CancellationToken token) {
            try {
                cameraMediator.RegisterCaptureBlock(this);
                Calculator.ResetProgress();
                using (var localCTS = CancellationTokenSource.CreateLinkedTokenSource(token)) {

                    Calculator.SelectedFilters.Clear();
                    Calculator.SelectedFilters.AddRange(FilterSelection.FilterWheelFilters.Where(f => f.Selected).Select(f => f.Filter));
                    await Calculator.Run(externalProgress, localCTS.Token);
                }
            } catch (OperationCanceledException) {
            } catch (Exception ex) {
                Logger.Error(ex);
                Notification.ShowError(ex.Message);
            } finally {
                cameraMediator.ReleaseCaptureBlock(this);
                externalProgress?.Report(GetStatus(string.Empty));
            }
            return false;
        }
        private ApplicationStatus GetStatus(string status) {
            return new ApplicationStatus { Source = "Filter Offset Calculator Tool", Status = status };
        }
    }
}
