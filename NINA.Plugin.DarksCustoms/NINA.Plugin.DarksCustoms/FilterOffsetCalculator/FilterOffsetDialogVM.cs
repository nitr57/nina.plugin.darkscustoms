using NINA.Core.Model.Equipment;
using NINA.Core.Utility;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace NINA.Plugin.DarksCustoms.FilterOffsetCalculator
{

    public class FilterOffsetDialogVM : BaseINPC
    {
        private ObservableCollection<FilterInfo> _calculatedOffsets;

        private ObservableCollection<FilterInfo> _oldOffsets;

        private bool _useRelativeOffsets;

        private int baseValue = 0;

        public FilterInfo _newDefaultFilter;

        public FilterInfo _oldDefaultFilter;

        public FilterOffsetDialogVM(List<FilterInfo> oldOffsets, List<FilterInfo> newOffsets, FilterInfo oldDefaultFilter, FilterInfo newDefaultFilter)
        {
            NewOffsets = new ObservableCollection<FilterInfo>(newOffsets);
            OldOffsets = new ObservableCollection<FilterInfo>(oldOffsets);
            OldDefaultFilter = oldDefaultFilter;
            NewDefaultFilter = oldDefaultFilter;
            UseRelativeOffsets = false;
            Apply = false;
            OKCommand = new RelayCommand((obj) => {
                Apply = true;
            });
            CancelCommand = new RelayCommand((obj) => {
                Apply = false;
            });
        }

        public ICommand OKCommand { get; set; }
        public ICommand CancelCommand { get; set; }

        public bool Apply { get; set; }

        public FilterInfo NewDefaultFilter
        {
            get => _newDefaultFilter;
            set
            {
                if (value == null || value.Position == -1)
                {
                    _newDefaultFilter = null;
                }
                else
                {
                    _newDefaultFilter = value;
                }
                if (UseRelativeOffsets && _newDefaultFilter != null)
                {
                    CalculateRelativeOffsets(_newDefaultFilter);
                }
                else if (_newDefaultFilter == null)
                {
                    UseRelativeOffsets = false;
                }
                RaisePropertyChanged();
            }
        }

        public List<FilterInfo> OffsetsWithNull
        {
            get
            {
                var list = new List<FilterInfo>(NewOffsets);
                list.Insert(0, new FilterInfo("-", 0, -1));
                return list;
            }
        }

        public ObservableCollection<FilterInfo> NewOffsets
        {
            get => _calculatedOffsets;
            set
            {
                _calculatedOffsets = value;
                RaisePropertyChanged();
            }
        }

        public FilterInfo OldDefaultFilter
        {
            get => _oldDefaultFilter;
            set
            {
                _oldDefaultFilter = value;
                RaisePropertyChanged();
            }
        }

        public ObservableCollection<FilterInfo> OldOffsets
        {
            get => _oldOffsets;
            set
            {
                _oldOffsets = value;
                RaisePropertyChanged();
            }
        }

        public bool UseRelativeOffsets
        {
            get => _useRelativeOffsets;
            set
            {
                _useRelativeOffsets = value;
                if (_useRelativeOffsets)
                {
                    if (NewDefaultFilter == null)
                    {
                        if (OldDefaultFilter == null)
                        {
                            NewDefaultFilter = OffsetsWithNull.Where(f => f.Position >= 0).OrderBy(f => f.Position).First();
                        }
                        else
                        {
                            NewDefaultFilter = OldDefaultFilter;
                        }
                    }

                    CalculateRelativeOffsets(NewDefaultFilter);
                }
                else
                {
                    RestoreAbsoluteOffsets();
                }

                RaisePropertyChanged();
            }
        }

        private void CalculateRelativeOffsets(FilterInfo baseFilter)
        {
            RestoreAbsoluteOffsets();

            baseValue = NewOffsets.Single(f => f.Position == baseFilter.Position).FocusOffset;
            foreach (var filter in NewOffsets)
            {
                filter.FocusOffset -= baseValue;
            }
        }

        private void RestoreAbsoluteOffsets()
        {
            foreach (var filter in NewOffsets)
            {
                filter.FocusOffset += baseValue;
            }

            baseValue = 0;
        }
    }
}