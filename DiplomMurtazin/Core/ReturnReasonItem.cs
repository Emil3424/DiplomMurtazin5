using DiplomMurtazin.ViewModel;

namespace DiplomMurtazin.Core
{
    public class ReturnReasonItem : BaseViewModel
    {
        private bool _isSelected;

        public string Name { get; set; }

        public bool IsDefective { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }
}