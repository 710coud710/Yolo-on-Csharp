using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Client.Models
{
    public class SelectableMaterialClass : INotifyPropertyChanged
    {
        private bool _isSelected;

        public MaterialClass MaterialClass { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        public int Id => MaterialClass?.Id ?? 0;
        public string Label => MaterialClass?.Label ?? string.Empty;
        public int LabelId => MaterialClass?.LabelId ?? 0;
        public string MaterialCode => MaterialClass?.MaterialCode ?? string.Empty;
        public string MaterialName => MaterialClass?.MaterialName ?? string.Empty;
        public string Description => MaterialClass?.Description ?? string.Empty;
        public DateTime CreatedAt => MaterialClass?.CreatedAt ?? DateTime.MinValue;

        public SelectableMaterialClass()
        {
            MaterialClass = new MaterialClass();
            _isSelected = false;
        }

        public SelectableMaterialClass(MaterialClass materialClass, bool isSelected = false)
        {
            MaterialClass = materialClass ?? new MaterialClass();
            _isSelected = isSelected;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
