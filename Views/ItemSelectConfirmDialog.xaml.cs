using System.Windows;

namespace Client.Views
{
    public partial class ItemSelectConfirmDialog : Window
    {
        public ItemSelectConfirmDialog(string displayName)
        {
            InitializeComponent();
            TxtMessage.Text = $"Are you sure you want to select '{displayName}' for detection?";
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
