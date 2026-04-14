using NINA.Core.Model.Equipment;
using NINA.Core.Utility;
using System.Collections.Generic;
using System.Linq;

namespace NINA.Plugin.DarksCustoms.FilterOffsetCalculator
{

    public class FilterSelectionVM : BaseINPC
    {
        private ObserveAllCollection<FilterInfoWrapper> filterWheelFilters;

        public FilterSelectionVM(List<FilterInfo> filterWheelFilters, List<FilterInfo> previousSelectedFilters)
        {
            FilterWheelFilters = new ObserveAllCollection<FilterInfoWrapper>(filterWheelFilters.Select(f => new FilterInfoWrapper(previousSelectedFilters.Contains(f), f)));
        }

        public ObserveAllCollection<FilterInfoWrapper> FilterWheelFilters
        {
            get => filterWheelFilters;
            set
            {
                filterWheelFilters = value;
                RaisePropertyChanged();
            }
        }
    }

    public class FilterInfoWrapper : BaseINPC
    {
        private bool selected;

        public FilterInfoWrapper(bool selected, FilterInfo filter)
        {
            Selected = selected;
            Filter = filter;
        }

        public FilterInfo Filter { get; set; }

        public bool Selected
        {
            get => selected;
            set
            {
                selected = value;
                RaisePropertyChanged();
            }
        }
    }
}