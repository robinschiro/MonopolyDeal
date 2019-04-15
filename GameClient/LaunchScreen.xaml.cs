using System;
using System.Windows;
using System.Windows.Controls;
using System.Net;
using ResourceList = GameClient.Properties.Resources;
using tvToolbox;
using System.IO;
using System.Windows.Input;

namespace GameClient
{
    /// <summary>
    /// Interaction logic for LaunchScreen.xaml
    /// </summary>
    public partial class LaunchScreen : Window
    {
        private tvProfile settings;

        public LaunchScreen()
        {
            InitializeComponent();

            this.Title = "Monopoly Deal Setup";

            // Load cached settings from Profile.
            string settingsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ResourceList.SettingsFilePath);
            settings = new tvProfile(settingsFilePath, tvProfileFileCreateActions.NoPromptCreateFile);

            // Populate fields with settings.
            this.PlayerNameTextBox.Text = settings.sValue(ResourceList.SettingNameKey, string.Empty);
            this.IPAddressTextBox.Text = settings.sValue(ResourceList.SettingIpAddressKey, string.Empty);
            this.PortTextBox.Text = settings.sValue(ResourceList.SettingPortKey, ResourceList.SettingPortDefaultValue);
        }

        private void LaunchLobby()
        {
            // Retrieve the port number.
            int portNumber = Convert.ToInt32(PortTextBox.Text);

            // Save settings.
            settings[ResourceList.SettingNameKey] = this.PlayerNameTextBox.Text;
            settings[ResourceList.SettingIpAddressKey] = this.IPAddressTextBox.Text;
            settings[ResourceList.SettingPortKey] = this.PortTextBox.Text;
            settings.Save();

            RoomWindow roomWindow = new RoomWindow(IPAddressTextBox.Text, portNumber, PlayerNameTextBox.Text);
            if ( roomWindow.ShowWindow )
            {
                roomWindow.Show();
            }
            this.Close();
        }

        #region Events

        private void OKButton_Click( object sender, RoutedEventArgs e )
        {
            this.LaunchLobby();
        }

        private void Window_KeyDown( object sender, System.Windows.Input.KeyEventArgs e )
        {
            if ( Key.Enter == e.Key && this.OKButton.IsEnabled)
            {
                this.LaunchLobby();
            }
        }

        private void Window_Loaded( object sender, RoutedEventArgs e )
        {
            this.ValidateTextFields();
        }

        private void TextBox_TextChanged( object sender, TextChangedEventArgs e )
        {
            this.ValidateTextFields();
        }

        #endregion

        #region Validation

        private bool IsPlayerNameValid()
        {
            return !String.IsNullOrWhiteSpace(PlayerNameTextBox.Text);
        }

        private bool IsIpAddressValid()
        {
            IPAddress filler;
            return (IPAddressTextBox.Text.ToLower() == "localhost") || (IPAddress.TryParse(IPAddressTextBox.Text, out filler));
        }

        private bool IsPortValid()
        {
            int port;
            return int.TryParse(PortTextBox.Text, out port);
        }

        private void ValidateTextFields()
        {
            // The outer try block is necessary because this event is initially fired before the OKButton exists.
            try
            {
                if ( this.IsIpAddressValid() && this.IsPlayerNameValid() && this.IsPortValid() )
                {
                    OKButton.IsEnabled = true;
                }
                else
                {
                    OKButton.IsEnabled = false;
                }
            }
            catch { }
        }

        #endregion
    }
}
