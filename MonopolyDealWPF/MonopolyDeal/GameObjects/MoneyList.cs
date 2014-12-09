using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace GameObjects
{
    public class MoneyList : ObservableCollection<int>, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public MoneyList()
        {
            // Add six '0's representing each possible money value.
            for ( int i = 0; i < 6; ++i )
            {
                this.Add(0);
            }
        }

        public bool Remove( Card card )
        {
            switch ( card.Value )
            {
                case 1:
                {
                    if ( this[0] != 0 )
                    {
                        this[0]--;
                        return true;
                    }
                    break;
                }

                case 2:
                {
                    if ( this[1] != 0 )
                    {
                        this[1]--;
                        return true;
                    }
                    break;
                }

                case 3:
                {
                    if ( this[2] != 0 )
                    {
                        this[2]--;
                        return true;
                    }
                    break;
                }

                case 4:
                {
                    if ( this[3] != 0 )
                    {
                        this[3]--;
                        return true;
                    }
                    break;
                }

                case 5:
                {
                    if ( this[4] != 0 )
                    {
                        this[4]--;
                        return true;
                    }
                    break;
                }

                case 10:
                {
                    if ( this[5] != 0 )
                    {
                        this[5]--;
                        return true;
                    }
                    break;
                }
            }

            return false;
        }

        public void Add( Card card )
        {   
            switch ( card.Value )
            {
                case 1:
                {
                    this[0]++;
                    break;
                }

                case 2:
                {
                    this[1]++;
                    break;
                }

                case 3:
                {
                    this[2]++;
                    break;
                }

                case 4:
                {
                    this[3]++;
                    break;
                }

                case 5:
                {
                    this[4]++;
                    break;
                }

                case 10:
                {
                    this[5]++;
                    break;
                }
            }
        }

        // Create the OnPropertyChanged method to raise the event 
        protected void OnPropertyChanged( string name )
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if ( handler != null )
            {
                handler(this, new PropertyChangedEventArgs(name));
            }
        }



    }
}
