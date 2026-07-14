using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Client.Views
{
    public partial class ItemsView : UserControl
    {
        public ItemsView()
        {
            InitializeComponent();
            ManageButton.Click += ManageButton_Click;
        }

        private void ManageButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.ContextMenu != null)
            {
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.Placement = PlacementMode.Bottom;
                button.ContextMenu.IsOpen = true;
            }
        }
    }
}
