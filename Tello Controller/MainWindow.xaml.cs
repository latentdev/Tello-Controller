using MahApps.Metro.Controls;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using TelloLib;

namespace Tello_Controller
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow, INotifyPropertyChanged
    {
        private Uri uri;

        private Tello.ConnectionState connection;
        public Tello.ConnectionState Connection
        {
            get
            {
                return connection;
            }
            set
            {
                connection = value;
                NotifyPropertyChanged(nameof(Connection));
            }
        }

        private String joystick;
        public String Joystick
        {
            get
            {
                return joystick;
            }
            set
            {
                joystick = value;
                NotifyPropertyChanged(nameof(Joystick));
            }
        }

        private int droneHeight;
        public int DroneHeight {
            get
            {
                return droneHeight;
            }
            set
            {
                droneHeight = value;
                NotifyPropertyChanged(nameof(DroneHeight));
            }
        }

        private int droneSpeed;
        public int DroneSpeed
        {
            get
            {
                return droneSpeed;
            }
            set
            {
                droneSpeed = value;
                NotifyPropertyChanged(nameof(DroneSpeed));
            }
        }


        private int droneBatt;
        public int DroneBatt
        {
            get
            {
                return droneBatt;
            }

            set
            {
                droneBatt = value;
                NotifyPropertyChanged(nameof(DroneBatt));
            }
        }

        private bool flightMode;
        public bool FlightMode
        {
            get => flightMode;
            set
            {
                flightMode = value;
                Tello.controllerState.setSpeedMode(flightMode == true ? 1:0);
                Tello.sendControllerUpdate();
                NotifyPropertyChanged(nameof(FlightMode));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            Init();

            Tello.startConnecting();//Start trying to connect.
        }

        private void Init()
        {
            var builder = new UriBuilder("udp", "127.0.0.1");
            builder.Port = 7038;
            uri = builder.Uri;
            DroneHeight = 0;
            DroneSpeed = 0;
            DroneBatt = 100;

            Tello.picPath = $@"{Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)}\tello\";

            Tello.onConnection += (Tello.ConnectionState newState) =>
            {
                if (newState != Tello.ConnectionState.Connected)
                {
                    //handle wifi connection
                }
                    if (newState == Tello.ConnectionState.Connected)
                {
                    FlightMode = true;
                    Tello.setMaxHeight(40);
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        _streamPlayerControl.StartPlay(uri);
                    }));   
                }
                if (newState == Tello.ConnectionState.Disconnected)
                {
                    bool playing = false;
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        playing = _streamPlayerControl.IsPlaying;
                    }));

                    if (playing)
                    {
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            _streamPlayerControl.Stop();
                        }));
                    }
                }

                Connection = newState;
            };

            //subscribe to Tello update events. Called when update data arrives from drone.
            Tello.onUpdate += (Tello.FlyData newState) =>
            {
                DroneHeight = newState.height/10;
                DroneSpeed = newState.flySpeed;
                DroneBatt = newState.batteryPercentage;
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    _streamPlayerControl.Visibility = _streamPlayerControl.IsPlaying ? Visibility.Visible : Visibility.Hidden;
                }));
            };

            //subscribe to Joystick update events. Called ~10x second.
            PCJoystick.onUpdate += (SharpDX.DirectInput.JoystickState joyState) =>
            {

                var rx = (((float)joyState.RotationX / 0x8000) - 1);
                var ry = (((float)joyState.RotationY / 0x8000) - 1) * -1;//invert
                var lx = (((float)joyState.X / 0x8000) - 1);
                var ly = (((float)joyState.Y / 0x8000) - 1) * -1;//invert
                //var boost = joyState.Z
                
                float[] axes = new float[] { lx, ly, rx, ry, 0 };
                Tello.controllerState.setAxis(lx, ly, rx, ry);
                Tello.sendControllerUpdate();

                if(joyState.Buttons[7])
                {
                    Tello.takeOff();
                }
                else if(joyState.Buttons[6])
                {
                    Tello.land();
                }
                else if(joyState.Buttons[5])
                {
                    if (!Directory.Exists(Tello.picPath))
                    {
                        Directory.CreateDirectory(Tello.picPath);
                    }

                    Tello.takePicture();
                }
                else if(joyState.Buttons[4])
                {
                    //Video
                }
                else if(joyState.Buttons[0])
                {
                    Tello.doFlip(2);
                }
                else if (joyState.Buttons[1])
                {
                    Tello.doFlip(3);
                }
                else if (joyState.Buttons[2])
                {
                    Tello.doFlip(1);
                }
                else if (joyState.Buttons[3])
                {
                    Tello.doFlip(0);
                }

            };
            PCJoystick.Init();

            //Connection to send raw video data to local udp port.
            //To play: ffplay -probesize 32 -sync ext udp://127.0.0.1:7038
            //To play with minimum latency:ffmpeg -i udp://127.0.0.1:7038 -f sdl "Tello"
            var videoClient = UdpUser.ConnectTo("127.0.0.1", 7038);

            //subscribe to Tello video data
            Tello.onVideoData += (byte[] data) =>
            {
                try
                {
                    videoClient.Send(data.Skip(2).ToArray());//Skip 2 byte header and send to ffplay. 
                }
                catch (Exception ex)
                {

                }
            }; 
        }

        private void HandlePlayerEvent(object sender, RoutedEventArgs e)
        {
            if (e.RoutedEvent.Name == "StreamStarted")
            {
            }
            else if (e.RoutedEvent.Name == "StreamFailed")
            {

                MessageBox.Show(
                    ((WebEye.Controls.Wpf.StreamPlayerControl.StreamFailedEventArgs)e).Error,
                    "Stream Player",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            else if (e.RoutedEvent.Name == "StreamStopped")
            {
            }
        }

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void TakeOff_Click(object sender, RoutedEventArgs e)
        {
            Tello.takeOff();
        }

        private void Land_Click(object sender, RoutedEventArgs e)
        {
            Tello.land();
        }

        private void Picture_Click(object sender, RoutedEventArgs e)
        {
            Tello.takePicture();
        }

        private void Record_Click(object sender, RoutedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (_streamPlayerControl.IsPlaying)
            {
                _streamPlayerControl.Stop();
            }
            if(Connection == Tello.ConnectionState.Connected)
            {
                Tello.disconnect();
            }
            Thread.Sleep(1000); //allow threads time to exit.
        }
    }
}
