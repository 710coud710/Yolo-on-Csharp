using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Client.Models
{
    public class SelectableModelClass : INotifyPropertyChanged
    {
        private bool _isLocal = true;
        public bool IsLocal
        {
            get => _isLocal;
            set
            {
                if (_isLocal != value)
                {
                    _isLocal = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isSelected;
        public ModelClass ModelClass { get; set; }

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

        public int Id => ModelClass?.Id ?? 0;
        public string ModelCode => ModelClass?.ModelCode ?? string.Empty;
        public string ModelName => ModelClass?.ModelName ?? string.Empty;
        public string ModelPath => ModelClass?.ModelPath ?? string.Empty;
        public bool IsActive => ModelClass?.IsActive ?? false;
        public string Description => ModelClass?.Description ?? string.Empty;
        public DateTime CreatedAt => ModelClass?.CreatedAt ?? DateTime.MinValue;

        public SelectableModelClass()
        {
            ModelClass = new ModelClass();
            _isSelected = false;
        }

        public SelectableModelClass(ModelClass modelClass, bool isSelected = false)
        {
            ModelClass = modelClass ?? new ModelClass();
            _isSelected = isSelected;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
