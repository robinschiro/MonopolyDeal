using System;
using System.Windows;
using System.Windows.Controls;
using System.Net;
using ResourceList = GameClient.Properties.Resources;
using tvToolbox;
using System.IO;

namespace GameClient
{
    /// <summary>
    /// Interaction logic for LaunchScreen.xaml
    /// </summary>
    public partial class LaunchScreen : Window
    {
        private bool isIPAddressValid = true;
        private bool isPlayerNameValid = true;
        private bool isPortValid = true;
        private tvProfile settings;

        public LaunchScreen()
        {
            InitializeComponent();
            this.Title = "Monopoly Deal Setup";

            // Load cached settings from Profile.
            string settingsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ResourceList.SettingsFilePath);
            settings = new tvProfile(settingsFilePath, tvProfileFileCreateActions.NoPromptCreateFile);

            // Populate fields with settings.
            this.PlayerNameTextBox.Text = settings.sValue(ResourceList.SettingNameKey, ResourceList.SettingNameDefaultValue);
            this.IPAddressTextBox.Text = settings.sValue(ResourceList.SettingIpAddressKey, ResourceList.SettingIpAddressDefaultValue);
            this.PortTextBox.Text = settings.sValue(ResourceList.SettingPortKey, ResourceList.SettingPortDefaultValue);            

            this.OKButton.Focus();
        }

        private void OKButton_Click( object sender, RoutedEventArgs e )
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

        // The OKButton is enabled only when text exists in the IPAddress textbox.
        private void IPAddress_TextChanged( object sender, TextChangedEventArgs e )
        {
            IPAddress filler;
            isIPAddressValid = (IPAddressTextBox.Text.ToLower() == "localhost") || (IPAddress.TryParse(IPAddressTextBox.Text, out filler));
            ValidateTextFields();            
        }

        private void PlayerName_TextChanged( object sender, TextChangedEventArgs e )
        {
            isPlayerNameValid = !String.IsNullOrWhiteSpace(PlayerNameTextBox.Text);
            ValidateTextFields();
        }

        private void ValidateTextFields()
        {
            // The outer try block is necessary because this event is initially fired before the OKButton exists.
            try
            {
                if ( isIPAddressValid && isPlayerNameValid && isPortValid )
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

        private void Port_TextChanged( object sender, TextChangedEventArgs e )
        {
            isPortValid = true;

            try
            {
                Convert.ToInt32(PortTextBox.Text);
            }
            catch (Exception ex)
            {
                isPortValid = false;
            }

            ValidateTextFields();
        }
    }
}
