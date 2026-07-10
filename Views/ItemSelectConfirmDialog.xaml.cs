using System.Windows;

namespace Client.Views
{
    public partial class ItemSelectConfirmDialog : Window
    {
        public ItemSelectConfirmDialog(string displayName)
        {
            InitializeComponent();
            TxtTitle.Text = "Confirm Selection";
            TxtMessage.Text = $"Are you sure you want to select '{displayName}' for detection?";
        }

        public ItemSelectConfirmDialog(string message, string title)
        {
            InitializeComponent();
            TxtTitle.Text = title;
            TxtMessage.Text = message;
        }

        public ItemSelectConfirmDialog(string message, string title, string iconKind, string hexColor)
        {
            InitializeComponent();
            TxtTitle.Text = title;
            TxtMessage.Text = message;
            
            if (System.Enum.TryParse(iconKind, out MaterialDesignThemes.Wpf.PackIconKind parsedKind))
            {
                IconPack.Kind = parsedKind;
            }
            
            try
            {
                IconPack.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hexColor));
            }
            catch
            {
                IconPack.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Orange);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
