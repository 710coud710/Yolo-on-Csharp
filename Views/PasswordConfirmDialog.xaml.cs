using System;
using System.Windows;
using System.Windows.Input;
using Client.Services;

namespace Client.Views
{
    public partial class PasswordConfirmDialog : Window
    {
        private readonly ISettingsService _settingsService;

        public PasswordConfirmDialog(ISettingsService settingsService)
        {
            InitializeComponent();
            _settingsService = settingsService;
            TxtPassword.Focus();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private async void Confirm_Click(object sender, RoutedEventArgs e)
        {
            string password = TxtPassword.Password;
            if (string.IsNullOrEmpty(password))
            {
                LblError.Text = "Password cannot be empty.";
                LblError.Visibility = Visibility.Visible;
                return;
            }

            BtnConfirm.IsEnabled = false;
            LblError.Visibility = Visibility.Collapsed;

            try
            {
                var dbService = new DatabaseService(_settingsService);
                string hostname = Environment.MachineName;
                bool isCorrect = false;

                try
                {
                    isCorrect = await dbService.VerifyMachinePasswordAsync(hostname, password);
                }
                catch (Exception)
                {
                    // Fallback to local default password check in case of database offline
                    isCorrect = string.Equals(password, "admin");
                }

                if (isCorrect)
                {
                    DialogResult = true;
                    Close();
                }
                else
                {
                    LblError.Text = "Incorrect machine password.";
                    LblError.Visibility = Visibility.Visible;
                    TxtPassword.Clear();
                    TxtPassword.Focus();
                }
            }
            catch (Exception ex)
            {
                LblError.Text = $"Error: {ex.Message}";
                LblError.Visibility = Visibility.Visible;
            }
            finally
            {
                BtnConfirm.IsEnabled = true;
            }
        }

        private void TxtPassword_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Confirm_Click(sender, e);
            }
        }
    }
}
