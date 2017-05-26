using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.ComponentModel;

namespace AdditionalWindows
{
    public class ModalWindow : Window
    {
        protected bool dialogResult = false;
        protected bool isModal = false;

        private bool closeWindow = false;
        public bool CloseWindow
        {
            get
            {
                return closeWindow;
            }
            set
            {
                closeWindow = value;

                // For some reason, this.DialogResult must be set IMMEDIATELY before this.Close() is called.
                // Otherwise, it will be null when the window closes.
                this.DialogResult = this.dialogResult;
                if ( closeWindow )
                {
                    this.Close();
                }
            }
        }

        public ModalWindow( bool isModal = true )
        {
            this.isModal = isModal;
        }

        // Only allow the this window to be closed when closeWindow has been enabled.
        protected override void OnClosing( CancelEventArgs e )
        {
            if ( !closeWindow && isModal )
            {
                e.Cancel = true;
            }
        }
    }
}
