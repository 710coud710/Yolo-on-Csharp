using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Client.ViewModels;

namespace Client
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            if (e.Key == Key.Space || e.Key == Key.Enter)
            {
                // Skip shortcut actions if the user is typing in a TextBox
                if (Keyboard.FocusedElement is TextBox)
                {
                    return;
                }

                if (DataContext is MainViewModel mainVm)
                {
                    if (mainVm.CurrentViewModel is DashboardViewModel dashboardVm)
                    {
                        // Skip if settings mode is active
                        if (dashboardVm.IsSettingsMode)
                        {
                            return;
                        }

                        if (dashboardVm.ModelProcess == "Stream")
                        {
                            if (dashboardVm.ToggleStreamDetectionCommand.CanExecute(null))
                            {
                                dashboardVm.ToggleStreamDetectionCommand.Execute(null);
                                e.Handled = true;
                            }
                        }
                        else
                        {
                            if (dashboardVm.StartCommand.CanExecute(null))
                            {
                                dashboardVm.StartCommand.Execute(null);
                                e.Handled = true;
                            }
                        }
                    }
                    else if (mainVm.CurrentViewModel is ResultViewModel resultVm)
                    {
                        if (resultVm.BackCommand.CanExecute(null))
                        {
                            resultVm.BackCommand.Execute(null);
                            e.Handled = true;
                        }
                    }
                }
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}