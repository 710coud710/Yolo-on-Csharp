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
