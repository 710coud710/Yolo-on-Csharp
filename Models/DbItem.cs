using System;
using Client.ViewModels;

namespace Client.Models
{
    public class DbItem : ViewModelBase
    {
        private int _index;
        private int _id;
        private int _classId;
        private string _itemCode = string.Empty;
        private string _itemName = string.Empty;
        private string _itemType = string.Empty;
        private bool _isActive;
        private int _classCode;
        private string _className = string.Empty;

        public int Index
        {
            get => _index;
            set => SetProperty(ref _index, value);
        }

        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public int ClassId
        {
            get => _classId;
            set => SetProperty(ref _classId, value);
        }

        public string ItemCode
        {
            get => _itemCode;
            set
            {
                if (SetProperty(ref _itemCode, value))
                {
                    OnPropertyChanged(nameof(DisplayName));
                }
            }
        }

        public string ItemName
        {
            get => _itemName;
            set
            {
                if (SetProperty(ref _itemName, value))
                {
                    OnPropertyChanged(nameof(DisplayName));
                }
            }
        }

        public string ItemType
        {
            get => _itemType;
            set => SetProperty(ref _itemType, value);
        }

        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        public int ClassCode
        {
            get => _classCode;
            set => SetProperty(ref _classCode, value);
        }

        public string ClassName
        {
            get => _className;
            set => SetProperty(ref _className, value);
        }

        public string DisplayName => $"{ItemCode}-{ItemName}";
    }
}

