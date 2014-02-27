﻿/* ARDrone Control .NET - An application for flying the Parrot AR drone in Windows.
 * Copyright (C) 2010 Thomas Endres, Stephen Hobley, Julien Vinel
 * 
 * This program is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation; either version 3 of the License, or (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License along with this program; if not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using AviationInstruments;
using ARDrone.Control;
using ARDrone.Capture;
using ARDrone.Hud;
using ARDrone.Input;
using ARDrone.Input.Utils;
using ARDrone.Control.Commands;
using ARDrone.Control.Data;
using ARDrone.Control.Events;
using ARDrone.Control.Workers;
using System.Diagnostics;
using System.Configuration;



namespace ARDrone.UI
{
    public partial class MainWindow : Window
    {
        private readonly TimeSpan booleanInputTimeout = new TimeSpan(0, 0, 0, 0, 250);

        private delegate void OutputEventHandler(String output);

        private DispatcherTimer timerStatusUpdate;
        private DispatcherTimer timerVideoUpdate;
        private DispatcherTimer timerHudStatusUpdate;

        private VideoRecorder videoRecorder;
        private SnapshotRecorder snapshotRecorder;

        private InstrumentsManager instrumentsManager;
        private HudInterface hudInterface;

        private ARDrone.Input.InputManager inputManager;
        private Dictionary<String, DateTime> booleanInputFadeout;

        private DroneControl droneControl;

        private DroneConfig currentDroneConfig;
        private HudConfig currentHudConfig;

        private GPSDataRetriever gps_start;
        private Process cmdVideo;

        int frameCountSinceLastCapture = 0;
        DateTime lastFrameRateCaptureTime;
        int averageFrameRate = 0;

        bool gps_running = false;

        String snapshotFilePath = string.Empty;
        int snapshotFileCount = 0;

        public MainWindow()
        {
            InitializeDroneControl();

            if (ShowSplashScreen())
            {
                InitializeComponent();
                InitializeOtherComponents();
            }
            else
            {
                this.Close();
            }

            //Fill in the drone animation dropdown box
            DroneAnimationComboBox.Items.Clear();
            DroneAnimationComboBox.Items.Add("Frontflip");
            DroneAnimationComboBox.Items.Add("Backflip");
            DroneAnimationComboBox.Items.Add("Leftflip");
            DroneAnimationComboBox.Items.Add("Rightflip");

            DroneAnimationComboBox.Items.Add("Phi M30 Deg");
            DroneAnimationComboBox.Items.Add("Phi 30 Deg");
            
            DroneAnimationComboBox.Items.Add("Theta M30 Deg");
            DroneAnimationComboBox.Items.Add("Theta 30 Deg");

            DroneAnimationComboBox.Items.Add("Theta 20 Deg Yaw 200 Deg");

            DroneAnimationComboBox.Items.Add("Theta 20 Deg Yaw M200 Deg");

            DroneAnimationComboBox.Items.Add("Turnaround");
            DroneAnimationComboBox.Items.Add("Turnaround Go Down");

            DroneAnimationComboBox.Items.Add("Yaw Dance");
            DroneAnimationComboBox.Items.Add("Phi Dance");
            DroneAnimationComboBox.Items.Add("Theta Dance");
            DroneAnimationComboBox.Items.Add("VZ Dance");

            DroneAnimationComboBox.Items.Add("Wave");

            DroneAnimationComboBox.Items.Add("Phi Theta Mixed");
            DroneAnimationComboBox.Items.Add("Double Phi Theta Mixed");

            DroneAnimationComboBox.Items.Add("MAYDAY");

            //Fill in the Led Animation Box
            LedAnimationComboBox.Items.Clear();
            LedAnimationComboBox.Items.Add("Blink green/red");
            LedAnimationComboBox.Items.Add("Blink Green");
            LedAnimationComboBox.Items.Add("Blink Red");
            LedAnimationComboBox.Items.Add("Blink Orange");
            LedAnimationComboBox.Items.Add("Snake Green/red");
            LedAnimationComboBox.Items.Add("Fire / shoot");
            LedAnimationComboBox.Items.Add("Standard");
            LedAnimationComboBox.Items.Add("Red");
            LedAnimationComboBox.Items.Add("Green");
            LedAnimationComboBox.Items.Add("Red Snake");
            LedAnimationComboBox.Items.Add("Blank");
            LedAnimationComboBox.Items.Add("Left Green Right Red");
            LedAnimationComboBox.Items.Add("Left Red Right Green");
            LedAnimationComboBox.Items.Add("Standard blink");
        }

        public void Dispose()
        {
            if (inputManager != null)
                inputManager.Dispose();
            if (videoRecorder != null)
                videoRecorder.Dispose();
            if (instrumentsManager != null)
                instrumentsManager.stopManage();
        }

        private void InitializeDroneControl()
        {
            currentDroneConfig = new DroneConfig();
            currentDroneConfig.Load();

            InitializeDroneControl(currentDroneConfig);
        }

        private void InitializeDroneControl(DroneConfig droneConfig)
        {
            droneControl = new DroneControl(droneConfig);
            //gps_start = new GPSDataRetriever();
        }

        private bool ShowSplashScreen()
        {
            SplashScreen splashScreen = new SplashScreen(droneControl);
            splashScreen.ShowDialog();

            return splashScreen.ConnectionSuccessful;
        }

        private void InitializeOtherComponents()
        {
            InitializeDroneControlEventHandlers();

            InitializeTimers();
            InitializeInputManager();

            InitializeAviationControls();
            InitializeHudInterface();

            InitializeRecorders();
        }

        private void InitializeDroneControlEventHandlers()
        {
            droneControl.Error += droneControl_Error_Async;
            droneControl.ConnectionStateChanged += droneControl_ConnectionStateChanged_Async;
        }

        private void InitializeTimers()
        {
            timerStatusUpdate = new DispatcherTimer();
            timerStatusUpdate.Interval = new TimeSpan(0, 0, 1);
            timerStatusUpdate.Tick += new EventHandler(timerStatusUpdate_Tick);

            timerHudStatusUpdate = new DispatcherTimer();
            timerHudStatusUpdate.Interval = new TimeSpan(0, 0, 0, 0, 50);
            timerHudStatusUpdate.Tick += new EventHandler(timerHudStatusUpdate_Tick);

            timerVideoUpdate = new DispatcherTimer();
            timerVideoUpdate.Interval = new TimeSpan(0, 0, 0, 0, 50);
            timerVideoUpdate.Tick += new EventHandler(timerVideoUpdate_Tick);
        }

        private void InitializeInputManager()
        {
            inputManager = new ARDrone.Input.InputManager(Utility.GetWindowHandle(this));
            inputManager.SwitchInputMode(Input.InputManager.InputMode.ControlInput);

            inputManager.NewInputState += inputManager_NewInputState;
            inputManager.NewInputDevice += inputManager_NewInputDevice;
            inputManager.InputDeviceLost += inputManager_InputDeviceLost;

            booleanInputFadeout = new Dictionary<String, DateTime>();
        }

        private void InitializeAviationControls()
        {
            instrumentsManager = new InstrumentsManager(droneControl);
            instrumentsManager.addInstrument(this.attitudeControl);
            instrumentsManager.addInstrument(this.altimeterControl);
            instrumentsManager.addInstrument(this.headingControl);
            instrumentsManager.startManage();
        }

        private void InitializeHudInterface()
        {
            currentHudConfig = new HudConfig();
            currentHudConfig.Load();

            InitializeHudInterface(currentHudConfig);
        }

        private void InitializeHudInterface(HudConfig hudConfig)
        {
            HudConstants hudConstants = new HudConstants(droneControl.FrontCameraFieldOfViewDegrees);

            hudInterface = new HudInterface(hudConfig, hudConstants);
        }

        private void InitializeRecorders()
        {
            videoRecorder = new VideoRecorder();
            snapshotRecorder = new SnapshotRecorder();

            videoRecorder.CompressionComplete += new EventHandler(videoRecorder_CompressionComplete);
            videoRecorder.CompressionError += new System.IO.ErrorEventHandler(videoRecorder_CompressionError);
        }

        public void Init()
        {
            timerStatusUpdate.Start();

            UpdateStatus();
            UpdateInteractiveElements();
        }

        private void processOpen()
        {
            //Process cmdVideo = new Process();
            string command = "/c C:\\ffmpeg\\ffplay.exe tcp://" + droneControl.droneConfig.DroneIpAddress + ":" + droneControl.droneConfig.VideoPort;
            //if called from connect, x will be 1 and the video will open in a new window
            cmdVideo.StartInfo.FileName = "cmd.exe";
            cmdVideo.StartInfo.Arguments = command;
            cmdVideo.StartInfo.Verb = "runas";
            cmdVideo.Start();
            UpdateUISync("Video started");

            gps_start = new GPSDataRetriever(droneControl);
            gps_running = true;
            //UpdateUIAsync(gps_start.Write());
            //gps_start.Connect();
            //textBoxOutput.AppendText(gps_start.Write() + "\r\n");
            //scrollViewerOutput.ScrollToBottom();
            //UpdateInteractiveElements();
        }
        private void processClose()
        {
            // otherwise it is called from disconnect and will close the cmd prompt and video
                
                //cmdVideo.Kill();
                cmdVideo.Close();
                cmdVideo.CloseMainWindow();
                UpdateUISync("Video closed");
        }

        private void Connect()
        {
            //after clicking "Startup", the computer connects and opens a window
            droneControl.ConnectToDrone();
            UpdateUISync("Connecting to the drone");
            
            timerHudStatusUpdate.Start();
            timerVideoUpdate.Start();
            
            //Opens video to AR Drone in new window
            cmdVideo = new Process();
            processOpen();

            UpdateUIAsync(gps_start.Write());

            lastFrameRateCaptureTime = DateTime.Now;
        }

        private void Disconnect()
        {
            timerHudStatusUpdate.Stop();
            timerVideoUpdate.Stop();

            if (videoRecorder != null && videoRecorder.IsVideoCaptureRunning)
            {
                videoRecorder.EndVideo();
            }
            
            droneControl.Disconnect();
            //processClose();
            UpdateUISync("Disconnecting from drone");
            
        }

        //Start command list for drone control
        private void ChangeCamera()
        {
            Command switchCameraCommand = new SwitchCameraCommand(DroneCameraMode.NextMode);

            if (!droneControl.IsCommandPossible(switchCameraCommand) || videoRecorder.IsVideoCaptureRunning)
                return;

            droneControl.SendCommand(switchCameraCommand);
            UpdateUIAsync("Changing camera");
        }
        
        private void Takeoff()
        {
            Command takeOffCommand = new FlightModeCommand(DroneFlightMode.TakeOff);

            if (!droneControl.IsCommandPossible(takeOffCommand))
                return;

            droneControl.SendCommand(takeOffCommand);


            UpdateUIAsync("Taking off");
        }

        private void Land()
        {
            Command landCommand = new FlightModeCommand(DroneFlightMode.Land);

            if (!droneControl.IsCommandPossible(landCommand))
                return;

            droneControl.SendCommand(landCommand);
            UpdateUIAsync("Landing");
        }

        private void Emergency()
        {
            Command emergencyCommand = new FlightModeCommand(DroneFlightMode.Emergency);

            if (droneControl.droneVersion == 1)
            {
                if (!droneControl.IsCommandPossible(emergencyCommand)) //Because the Emergency function only turns off on the AR2.0 - don't know about the AR 1, but i want to be able to un-set the emergency :)
                    return;
            }

            droneControl.SendCommand(emergencyCommand);
            UpdateUIAsync("Sending emergency signal");
        }

        private void FlatTrim()
        {
            Command resetCommand = new FlightModeCommand(DroneFlightMode.Reset);
            Command flatTrimCommand = new FlatTrimCommand();

            if (!droneControl.IsCommandPossible(resetCommand) || !droneControl.IsCommandPossible(flatTrimCommand))
                return;

            droneControl.SendCommand(resetCommand);
            droneControl.SendCommand(flatTrimCommand);
            UpdateUIAsync("Sending flat trim");
        }

        private void EnterHoverMode()
        {
            Command enterHoverModeCommand = new HoverModeCommand(DroneHoverMode.Hover);

            if (!droneControl.IsCommandPossible(enterHoverModeCommand))
                return;

            droneControl.SendCommand(enterHoverModeCommand);
            UpdateUIAsync("Entering hover mode");
        }

        private void LeaveHoverMode()
        {
            Command leaveHoverModeCommand = new HoverModeCommand(DroneHoverMode.StopHovering);

            if (!droneControl.IsCommandPossible(leaveHoverModeCommand))
                return;

            droneControl.SendCommand(leaveHoverModeCommand);
            UpdateUIAsync("Leaving hover mode");
        }

        private void Navigate(float roll, float pitch, float yaw, float gaz)
        {
            FlightMoveCommand flightMoveCommand = new FlightMoveCommand(roll, pitch, yaw, gaz);

            if (droneControl.IsCommandPossible(flightMoveCommand))
                droneControl.SendCommand(flightMoveCommand);
        }
        //End commands

        private void UpdateUIAsync(String message)
        {
            Dispatcher.BeginInvoke(new OutputEventHandler(UpdateUISync), message);
        }

        private void UpdateUISync(String message)
        {
            textBoxOutput.AppendText(message + "\r\n");
            scrollViewerOutput.ScrollToBottom();

            UpdateInteractiveElements();
        }

        private void UpdateInteractiveElements()
        {
            //inputManager.SetFlags(droneControl.IsConnected, droneControl.IsEmergency, droneControl.IsFlying, droneControl.IsHovering);

            if (!droneControl.IsConnected && !droneControl.IsConnecting) { buttonConnect.IsEnabled = true; } else { buttonConnect.IsEnabled = false; }
            if (droneControl.IsConnected) { buttonShutdown.IsEnabled = true; } else { buttonShutdown.IsEnabled = false; }

            if (droneControl.CanTakeoff || droneControl.CanLand) { buttonCommandTakeoff.IsEnabled = true; } else { buttonCommandTakeoff.IsEnabled = false; }
            if (droneControl.CanEnterHoverMode || droneControl.CanLeaveHoverMode) { buttonCommandHover.IsEnabled = true; } else { buttonCommandHover.IsEnabled = false; }
            if (droneControl.CanCallEmergency) { buttonCommandEmergency.IsEnabled = true; } else { buttonCommandEmergency.IsEnabled = false; }
            if (droneControl.CanSendFlatTrim) { buttonCommandFlatTrim.IsEnabled = true; } else { buttonCommandFlatTrim.IsEnabled = false; }
            if (droneControl.IsCommandPossible(new SwitchCameraCommand(DroneCameraMode.NextMode)) && !videoRecorder.IsVideoCaptureRunning && !videoRecorder.IsCompressionRunning) { buttonCommandChangeCamera.IsEnabled = true; } else { buttonCommandChangeCamera.IsEnabled = false; }

            if (!droneControl.IsFlying) { buttonCommandTakeoff.Content = "Take off"; } else { buttonCommandTakeoff.Content = "Land"; }
            if (!droneControl.IsHovering) { buttonCommandHover.Content = "Start hover"; } else { buttonCommandHover.Content = "Stop hover"; }

            if (droneControl.IsConnected) { buttonSnapshot.IsEnabled = true; } else { buttonSnapshot.IsEnabled = false; }
            if (!droneControl.IsConnected || videoRecorder.IsVideoCaptureRunning || videoRecorder.IsCompressionRunning) { checkBoxVideoCompress.IsEnabled = false; } else { checkBoxVideoCompress.IsEnabled = true; }
            if (CanCaptureVideo && !videoRecorder.IsVideoCaptureRunning && !videoRecorder.IsCompressionRunning) { buttonVideoStart.IsEnabled = true; } else { buttonVideoStart.IsEnabled = false; }
            if (CanCaptureVideo && videoRecorder.IsVideoCaptureRunning && !videoRecorder.IsCompressionRunning) { buttonVideoEnd.IsEnabled = true; } else { buttonVideoEnd.IsEnabled = false; }

            if (droneControl.IsConnected && !droneControl.IsFlying) { buttonShowConfig.IsEnabled = true; } else { buttonShowConfig.IsEnabled = false; }
            if (!droneControl.IsConnected && !droneControl.IsConnecting) { buttonGeneralSettings.IsEnabled = true; } else { buttonGeneralSettings.IsEnabled = false; }
            if (!droneControl.IsConnected && !droneControl.IsConnecting) { buttonInputSettings.IsEnabled = true; } else { buttonInputSettings.IsEnabled = false; }

            if (videoRecorder.IsCompressionRunning) { labelVideoStatus.Content = "Compressing"; }
            else if (videoRecorder.IsVideoCaptureRunning) { labelVideoStatus.Content = "Recording"; }
            else { labelVideoStatus.Content = "Idling ..."; }

            //if (gps_running) { UpdateUIAsync(gps_start.Write()); }
        }

        private void UpdateStatus()
        {
            if (!droneControl.IsConnected)
            {
                labelCamera.Content = "No picture";
                labelStatusCamera.Content = "None";

                labelStatusBattery.Content = "N/A";
                labelStatusAltitude.Content = "N/A";

                labelStatusTime.Content = "0.0";
                labelStatusLat.Content = "0.0";
                labelStatusLong.Content = "0.0";

                labelStatusFrameRate.Content = "No video";

                imageVideo.Source = null;
            }
            else
            {
                DroneData data = droneControl.NavigationData;
                int frameRate = GetCurrentFrameRate();

                ChangeCameraStatus();

                labelStatusBattery.Content = data.BatteryLevel.ToString() + "%";
                labelStatusAltitude.Content = data.Altitude.ToString();
                /*
                labelStatusTime.Content = data.time.ToString();
                labelStatusLat.Content = data.lat.ToString();
                labelStatusLong.Content = data.longi.ToString();
                */
                labelStatusFrameRate.Content = frameRate.ToString();
            }


            labelStatusConnected.Content = droneControl.IsConnected.ToString();
            labelStatusFlying.Content = droneControl.IsFlying.ToString();
            labelStatusHovering.Content = droneControl.IsHovering.ToString();

            UpdateCurrentBooleanInputState();
        }

        private void ChangeCameraStatus()
        {
            if (droneControl.CurrentCameraType == DroneCameraMode.FrontCamera)
            {
                labelCamera.Content = "Front camera";
                labelStatusCamera.Content = "Front";
            }
            else if (droneControl.CurrentCameraType == DroneCameraMode.PictureInPictureFront)
            {
                labelCamera.Content = "Front camera (PiP)";
                labelStatusCamera.Content = "Front (PiP)";
            }
            else if (droneControl.CurrentCameraType == DroneCameraMode.BottomCamera)
            {
                labelCamera.Content = "Bottom camera";
                labelStatusCamera.Content = "Bottom";
            }
            else if (droneControl.CurrentCameraType == DroneCameraMode.PictureInPictureBottom)
            {
                labelCamera.Content = "Bottom camera (PiP)";
                labelStatusCamera.Content = "Bottom (PiP)";
            }
        }

        private void UpdateHudStatus()
        {
            if (droneControl.IsConnected)
            {
                DroneData data = droneControl.NavigationData;

                hudInterface.SetFlightVariables(data.Phi, data.Theta, data.Psi);
                hudInterface.SetAltitude(data.Altitude);
                hudInterface.SetOverallSpeed(data.VX, data.VY, data.VZ);
                hudInterface.SetBatteryLevel(data.BatteryLevel);

                //hudInterface.SetLatLong(data.lat, data.longi);
            }
        }

        private int GetCurrentFrameRate()
        {
            int timePassed = (int)(DateTime.Now - lastFrameRateCaptureTime).TotalMilliseconds;
            int frameRate = frameCountSinceLastCapture * 1000 / timePassed;
            averageFrameRate = (averageFrameRate + frameRate) / 2;

            lastFrameRateCaptureTime = DateTime.Now;
            frameCountSinceLastCapture = 0;

            return averageFrameRate;
        }

        private void UpdateDroneState(InputState inputState)
        {
            labelInputRoll.Content = String.Format("{0:+0.000;-0.000;+0.000}", inputState.Roll);
            labelInputPitch.Content = String.Format("{0:+0.000;-0.000;+0.000}", -inputState.Pitch);
            labelInputYaw.Content = String.Format("{0:+0.000;-0.000;+0.000}", -inputState.Yaw);
            labelInputGaz.Content = String.Format("{0:+0.000;-0.000;+0.000}", -inputState.Gaz);

            UpdateCurrentBooleanInputState(inputState);
        }

        private void UpdateCurrentBooleanInputState()
        {
            RemoveOldBooleanInputStates();

            labelCurrentBooleanInput.Content = GetCurrentBooleanInputStates();
        }

        private void UpdateCurrentBooleanInputState(InputState inputState)
        {
            RemoveOldBooleanInputStates();
            AddNewBooleanInputStates(inputState);

            labelCurrentBooleanInput.Content = GetCurrentBooleanInputStates();
        }

        private void AddNewBooleanInputStates(InputState inputState)
        {
            if (inputState.TakeOff)
                AddNewBooleanInputState("TakeOff");
            if (inputState.Land)
                AddNewBooleanInputState("Land");
            if (inputState.Emergency)
                AddNewBooleanInputState("Emergency");
            if (inputState.FlatTrim)
                AddNewBooleanInputState("FlatTrim");
            if (inputState.Hover)
                AddNewBooleanInputState("Hover");
            if (inputState.CameraSwap)
                AddNewBooleanInputState("Camera");
            if (inputState.SpecialAction)
                AddNewBooleanInputState("Special");
        }

        private void AddNewBooleanInputState(String command)
        {
            DateTime expirationDate = DateTime.Now + booleanInputTimeout;
            if (booleanInputFadeout.ContainsKey(command))
                booleanInputFadeout[command] = expirationDate;
            else
                booleanInputFadeout.Add(command, expirationDate);
        }

        private void RemoveOldBooleanInputStates()
        {
            List<String> keysToDelete = new List<String>();
            foreach (KeyValuePair<String, DateTime> keyValuePair in booleanInputFadeout)
            {
                String command = keyValuePair.Key;
                DateTime expirationDate = keyValuePair.Value;

                if (expirationDate < DateTime.Now)
                    keysToDelete.Add(command);
            }

            foreach (String key in keysToDelete)
                booleanInputFadeout.Remove(key);
        }

        private String GetCurrentBooleanInputStates()
        {
            String commandText = "";

            List<String> commands = new List<String>();
            foreach (KeyValuePair<String, DateTime> keyValuePair in booleanInputFadeout)
                commands.Add(keyValuePair.Key);

            for (int i = 0; i < commands.Count; i++)
            {
                commandText += commands[i];

                if (i != commands.Count - 1)
                    commandText += ", ";
            }

            if (commandText == "")
                commandText = "No buttons";

            return commandText;
        }

        private void SendDroneCommands(InputState inputState)
        {
            if (inputState.CameraSwap)
            {
                ChangeCamera();
            }

            if (inputState.TakeOff && droneControl.CanTakeoff)
            {
                Takeoff();
            }
            else if (inputState.Land && droneControl.CanLand)
            {
                Land();
            }

            if (inputState.Hover && droneControl.CanEnterHoverMode)
            {
                EnterHoverMode();
            }
            else if (inputState.Hover && droneControl.CanLeaveHoverMode)
            {
                LeaveHoverMode();
            }

            if (inputState.Emergency)
            {
                Emergency();
            }
            else if (inputState.FlatTrim)
            {
                FlatTrim();
            }

            float roll = inputState.Roll / 1.0f;
            float pitch = inputState.Pitch / 1.0f;
            float yaw = inputState.Yaw / 2.0f;
            float gaz = inputState.Gaz / 2.0f;

            Navigate(roll, pitch, yaw, gaz);
        }

        private void SetNewVideoImage()
        {
            if (droneControl.IsConnected)
            {
                System.Drawing.Image image = droneControl.BitmapImage;

                if (image != null)
                {
                    frameCountSinceLastCapture++;

                    if (videoRecorder.IsVideoCaptureRunning)
                    {
                        videoRecorder.AddFrame((System.Drawing.Bitmap)image.Clone());
                    }
                }

                ImageSource imageSource = droneControl.ImageSourceImage;

                if (imageSource != null &&
                    (droneControl.CurrentCameraType == DroneCameraMode.FrontCamera ||
                     droneControl.CurrentCameraType == DroneCameraMode.PictureInPictureFront))
                {
                    ImageSource resultingSource = hudInterface.DrawHud((BitmapSource)imageSource);
                    imageVideo.Source = resultingSource;
                }
                else
                {
                    imageVideo.Source = imageSource;
                }
            }
        }

        private void TakeSnapshot()
        {
            if (snapshotFilePath == string.Empty)
            {
                snapshotFilePath = ShowFileDialog(".png", "PNG files (.png)|*.png");
                if (snapshotFilePath == null) { return; }
            }

            System.Drawing.Bitmap currentImage = (System.Drawing.Bitmap)droneControl.BitmapImage.Clone();
            snapshotRecorder.SaveSnapshot(currentImage, snapshotFilePath.Replace(".png", "_" + snapshotFileCount.ToString() + ".png"));
            UpdateUISync("Saved image #" + snapshotFileCount.ToString());
            snapshotFileCount++;
        }

        private void StartVideoCapture()
        {
            if (!CanCaptureVideo || videoRecorder.IsVideoCaptureRunning) { return; }

            String videoFilePath = ShowFileDialog(".avi", "Video files (.avi)|*.avi");
            if (videoFilePath == null) { return; }

            System.Drawing.Size size;
            if (droneControl.CurrentCameraType == DroneCameraMode.FrontCamera)
            {
                size = droneControl.FrontCameraPictureSize;
            }
            else
            {
                size = droneControl.BottomCameraPictureSize;
            }

            videoRecorder.StartVideo(videoFilePath, averageFrameRate, size.Width, size.Height, System.Drawing.Imaging.PixelFormat.Format32bppRgb, 4, checkBoxVideoCompress.IsChecked == true ? true : false);
            UpdateInteractiveElements();
        }

        private void EndVideoCapture()
        {
            if (!videoRecorder.IsVideoCaptureRunning)
            {
                return;
            }

            videoRecorder.EndVideo();

            UpdateInteractiveElements();
        }

        private String ShowFileDialog(String extension, String filter)
        {
            Microsoft.Win32.SaveFileDialog fileDialog = new Microsoft.Win32.SaveFileDialog();
            fileDialog.FileName = "ARDroneOut";
            fileDialog.DefaultExt = extension;
            fileDialog.Filter = filter;

            bool? result = fileDialog.ShowDialog();

            String fileName = null;
            if (result == true)
            {
                fileName = fileDialog.FileName;
            }

            try
            {
                if (File.Exists(fileName))
                {
                    File.Delete(fileName);
                }
            }
            catch (Exception)
            {
                MessageBox.Show(null, "The file could not be deleted", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                fileName = null;
            }

            return fileName;
        }

        private void OpenInputConfigDialog()
        {
            if (droneControl.IsConnected)
                return;

            inputManager.SwitchInputMode(Input.InputManager.InputMode.NoInput);

            InputConfigDialog configInput = new InputConfigDialog(inputManager);
            configInput.ShowDialog();

            inputManager.SwitchInputMode(Input.InputManager.InputMode.ControlInput);
        }

        private void OpenGeneralConfigDialog()
        {
            if (droneControl.IsConnected)
                return;

            GeneralConfigWindow configGeneral = new GeneralConfigWindow(currentDroneConfig, currentHudConfig);
            configGeneral.ShowDialog();

            if (configGeneral.ConfigChanged)
            {
                SaveDroneAndHudConfigStates(configGeneral.DroneConfig, configGeneral.HudConfig);
                ReinitializeDroneControlAndHud();
            }
        }

        private void SaveDroneAndHudConfigStates(DroneConfig droneConfig, HudConfig hudConfig)
        {
            currentDroneConfig = droneConfig;
            currentHudConfig = hudConfig;

            droneConfig.Save();
            hudConfig.Save();
        }

        private void ReinitializeDroneControlAndHud()
        {
            InitializeDroneControl(currentDroneConfig);
            InitializeDroneControlEventHandlers();
            InitializeHudInterface(currentHudConfig);
        }

        private void OpenDroneConfigDialog()
        {
            if (!droneControl.IsConnected)
                return;

            DroneConfigurationOutput configOutput = new DroneConfigurationOutput(droneControl.InternalDroneConfiguration);
            configOutput.ShowDialog();
        }

        private void HandleConnectionStateChange(DroneConnectionStateChangedEventArgs args)
        {
            UpdateInteractiveElements();

            if (args.Connected)
                UpdateUISync("Connected to the drone");
            else
                UpdateUISync("Disconnected from the drone");
        }

        private void HandleError(DroneErrorEventArgs args)
        {
            String errorText = SerializeException(args.CausingException);
            MessageBox.Show(errorText, "An error occured", MessageBoxButton.OK, MessageBoxImage.Error);

            UpdateInteractiveElements();
        }

        private String SerializeException(Exception e)
        {
            String errorMessage = e.Message;
            String exceptionTypeText = e.GetType().ToString();
            String stackTrace = e.StackTrace == null ? "No stack trace given" : e.StackTrace.ToString();

            String errorText = "An exception '" + exceptionTypeText + "' occured:\n" + errorMessage;
            errorText += "\n\nStack trace:\n" + stackTrace;

            if (e.InnerException != null)
            {
                errorText += "\n\n";
                errorText += SerializeException(e.InnerException);
            }

            return errorText;
        }

        private bool CanCaptureVideo
        {
            get
            {
                return droneControl.CanSwitchCamera;
            }
        }

        // Event handlers

        private void droneControl_Error_Async(object sender, DroneErrorEventArgs e)
        {
            Dispatcher.BeginInvoke(new DroneErrorEventHandler(droneControl_Error_Sync), sender, e);
        }

        private void droneControl_Error_Sync(object sender, DroneErrorEventArgs e)
        {
            HandleError(e);
        }

        private void droneControl_ConnectionStateChanged_Async(object sender, DroneConnectionStateChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new DroneConnectionStateChangedEventHandler(droneControl_ConnectionStateChanged_Sync), sender, e);
        }

        private void droneControl_ConnectionStateChanged_Sync(object sender, DroneConnectionStateChangedEventArgs e)
        {
            HandleConnectionStateChange(e);
        }

        private void inputManager_NewInputDevice(object sender, NewInputDeviceEventArgs e)
        {
            UpdateUIAsync("New input device: " + e.DeviceName);
        }

        private void inputManager_InputDeviceLost(object sender, InputDeviceLostEventArgs e)
        {
            UpdateUIAsync("Lost input device: " + e.DeviceName);
        }

        private void inputManager_NewInputState(object sender, NewInputStateEventArgs e)
        {
            SendDroneCommands(e.CurrentInputState);
            Dispatcher.BeginInvoke(new NewInputStateHandler(inputManagerSync_NewInputState), this, e);
        }

        private void inputManagerSync_NewInputState(object sender, NewInputStateEventArgs e)
        {
            UpdateDroneState(e.CurrentInputState);
        }

        private void videoRecorder_CompressionComplete(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new EventHandler(videoRecoderSync_CompressionComplete), this, e);
        }

        private void videoRecoderSync_CompressionComplete(object sender, EventArgs e)
        {
            MessageBox.Show(this, "Successfully compressed video!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            UpdateInteractiveElements();
        }

        private void videoRecorder_CompressionError(object sender, ErrorEventArgs e)
        {
            Dispatcher.BeginInvoke(new System.IO.ErrorEventHandler(videoRecoderSync_CompressionError), this, e);
        }

        private void videoRecoderSync_CompressionError(object sender, ErrorEventArgs e)
        {
            MessageBox.Show(this, e.GetException().Message, "Success", MessageBoxButton.OK, MessageBoxImage.Error);
            UpdateInteractiveElements();
        }


        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Init();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Dispose();
            Disconnect();
        }

        private void buttonConnect_Click(object sender, RoutedEventArgs e)
        {
            Connect();
        }

        private void buttonShutdown_Click(object sender, RoutedEventArgs e)
        {
            Disconnect();
        }

        private void buttonCommandChangeCamera_Click(object sender, RoutedEventArgs e)
        {
            ChangeCamera();
        }

        private void buttonCommandTakeoff_Click(object sender, RoutedEventArgs e)
        {
            if (!droneControl.IsFlying)
            {
                Takeoff();
            }
            else
            {
                Land();
            }
        }

        private void buttonCommandHover_Click(object sender, RoutedEventArgs e)
        {
            if (!droneControl.IsHovering)
            {
                EnterHoverMode();
            }
            else
            {
                LeaveHoverMode();
            }
        }

        private void buttonCommandEmergency_Click(object sender, RoutedEventArgs e)
        {
            Emergency();
        }

        private void buttonCommandFlatTrim_Click(object sender, RoutedEventArgs e)
        {
            FlatTrim();
        }

        private void buttonSnapshot_Click(object sender, RoutedEventArgs e)
        {
            TakeSnapshot();
        }

        private void buttonVideoStart_Click(object sender, RoutedEventArgs e)
        {
            StartVideoCapture();
        }

        private void buttonVideoEnd_Click(object sender, RoutedEventArgs e)
        {
            EndVideoCapture();
        }

        private void buttonInputSettings_Click(object sender, RoutedEventArgs e)
        {
            OpenInputConfigDialog();
        }

        private void buttonGeneralSettings_Click(object sender, RoutedEventArgs e)
        {
            OpenGeneralConfigDialog();
        }

        private void buttonShowConfig_Click(object sender, RoutedEventArgs e)
        {
            OpenDroneConfigDialog();
        }

        private void timerStatusUpdate_Tick(object sender, EventArgs e)
        {
            UpdateStatus();
        }

        private void timerHudStatusUpdate_Tick(object sender, EventArgs e)
        {
            UpdateHudStatus();
        }

        private void timerVideoUpdate_Tick(object sender, EventArgs e)
        {
            SetNewVideoImage();
        }

        private void imageVideo_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {

        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {

            //Process.Start("cmd.exe", "ffplay tcp://" + droneControl.droneConfig.StandardOwnIpAddress + ":" + droneControl.droneConfig.VideoPort);
            //gps_start = new GPSDataRetriever();
            
            string command = "/k C:\\ffmpeg\\ffplay.exe tcp://" + droneControl.droneConfig.StandardOwnIpAddress + ":" + droneControl.droneConfig.VideoPort;
            Process p = new Process();
            p.StartInfo.FileName = "cmd.exe";
            p.StartInfo.Arguments = command;
            p.StartInfo.Verb = "runas";
            p.Start();
             

        }

        private void button2_Click(object sender, RoutedEventArgs e)
        {
            Command DroneAnimationCommand = new PlayDroneAnimationCommand(ARDrone.Control.Commands.DroneAnimation.VZ_DANCE, 4000);

            droneControl.SendCommand(DroneAnimationCommand);
            UpdateUIAsync("Sending Dance animation");

        }

        private void button3_Click(object sender, RoutedEventArgs e)
        {
            

        }

        private void button4_Click(object sender, RoutedEventArgs e)
        {
            Command DroneAnimationCommand = new PlayDroneAnimationCommand(ARDrone.Control.Commands.DroneAnimation.FLIP_FRONT, 40);

            droneControl.SendCommand(DroneAnimationCommand);
            UpdateUIAsync("Sending frontflip animation");
        }

        private void button6_Click(object sender, RoutedEventArgs e)
        {

            DroneAnimation animation = ARDrone.Control.Commands.DroneAnimation.WAVE; 
            int miliseconds = Convert.ToInt16(animationDurationBox.Text.ToString()) * 1000;

            switch (DroneAnimationComboBox.SelectedIndex)
            {
                case 0: animation = ARDrone.Control.Commands.DroneAnimation.FLIP_FRONT; break;
                case 1: animation = ARDrone.Control.Commands.DroneAnimation.FLIP_BACK; break;
                case 2: animation = ARDrone.Control.Commands.DroneAnimation.FLIP_LEFT; break;
                case 3: animation = ARDrone.Control.Commands.DroneAnimation.FLIP_RIGHT; break;
                case 4: animation = ARDrone.Control.Commands.DroneAnimation.PHI_M30_DEG; break;
                case 5: animation = ARDrone.Control.Commands.DroneAnimation.PHI_30_DEG; break;
                case 6: animation = ARDrone.Control.Commands.DroneAnimation.THETA_M30_DEG; break;
                case 7: animation = ARDrone.Control.Commands.DroneAnimation.THETA_30_DEG; break;
                case 8: animation = ARDrone.Control.Commands.DroneAnimation.THETA_20DEG_YAW_200DEG; break;
                case 9: animation = ARDrone.Control.Commands.DroneAnimation.THETA_20DEG_YAW_M200DEG; break;
                case 10: animation = ARDrone.Control.Commands.DroneAnimation.TURNAROUND; break;
                case 11: animation = ARDrone.Control.Commands.DroneAnimation.TURNAROUND_GODOWN; break;
                case 12: animation = ARDrone.Control.Commands.DroneAnimation.YAW_DANCE; break;
                case 13: animation = ARDrone.Control.Commands.DroneAnimation.PHI_DANCE; break;
                case 14: animation = ARDrone.Control.Commands.DroneAnimation.THETA_DANCE; break;
                case 15: animation = ARDrone.Control.Commands.DroneAnimation.VZ_DANCE; break;
                case 16: animation = ARDrone.Control.Commands.DroneAnimation.WAVE; break;
                case 17: animation = ARDrone.Control.Commands.DroneAnimation.PHI_THETA_MIXED; break;
                case 18: animation = ARDrone.Control.Commands.DroneAnimation.DOUBLE_PHI_THETA_MIXED; break;
                case 19: animation = ARDrone.Control.Commands.DroneAnimation.MAYDAY; break;
            }

            Command DroneAnimationCommand = new PlayDroneAnimationCommand(animation, miliseconds);

            droneControl.SendCommand(DroneAnimationCommand);
        }



        private void button5_Click(object sender, RoutedEventArgs e)
        {
            Command DroneAnimationCommand = new PlayDroneAnimationCommand(ARDrone.Control.Commands.DroneAnimation.WAVE, 4000);

            droneControl.SendCommand(DroneAnimationCommand);
            UpdateUIAsync("Sending MAYDAY animation");
        }

        private void comboBox1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void button7_Click(object sender, RoutedEventArgs e)
        {
            Command DroneAnimationCommand = new PlayDroneAnimationCommand(ARDrone.Control.Commands.DroneAnimation.YAW_DANCE, 4000);

            droneControl.SendCommand(DroneAnimationCommand);
            UpdateUIAsync("Sending MAYDAY animation");
        }

        private void button8_Click(object sender, RoutedEventArgs e)
        {
            Command DroneAnimationCommand = new PlayDroneAnimationCommand(ARDrone.Control.Commands.DroneAnimation.FLIP_LEFT, 40);

            droneControl.SendCommand(DroneAnimationCommand);
            UpdateUIAsync("Sending MAYDAY animation");
        }

        private void button9_Click(object sender, RoutedEventArgs e)
        {
            LedAnimation animation = ARDrone.Control.Commands.LedAnimation.BLANK;
            int miliseconds = Convert.ToInt16(LedDuration.Text.ToString());
            int frequency = Convert.ToInt16(LedFrequency.Text.ToString());

            switch (LedAnimationComboBox.SelectedIndex)
            {
                case 0: animation = ARDrone.Control.Commands.LedAnimation.BLINK_GREEN_RED; break;
                case 1: animation = ARDrone.Control.Commands.LedAnimation.BLINK_GREEN; break;
                case 2: animation = ARDrone.Control.Commands.LedAnimation.BLINK_RED; break;
                case 3: animation = ARDrone.Control.Commands.LedAnimation.BLINK_ORANGE; break;
                case 4: animation = ARDrone.Control.Commands.LedAnimation.SNAKE_GREEN_RED; break;
                case 5: animation = ARDrone.Control.Commands.LedAnimation.FIRE; break;
                case 6: animation = ARDrone.Control.Commands.LedAnimation.STANDARD; break;
                case 7: animation = ARDrone.Control.Commands.LedAnimation.RED; break;
                case 8: animation = ARDrone.Control.Commands.LedAnimation.GREEN; break;
                case 9: animation = ARDrone.Control.Commands.LedAnimation.RED_SNAKE; break;
                case 10: animation = ARDrone.Control.Commands.LedAnimation.BLANK; break;
                case 11: animation = ARDrone.Control.Commands.LedAnimation.LEFT_GREEN_RIGHT_RED; break;
                case 12: animation = ARDrone.Control.Commands.LedAnimation.LEFT_RED_RIGHT_GREEN; break;
                case 13: animation = ARDrone.Control.Commands.LedAnimation.BLINK_STANDARD; break;
            }

            Command DroneAnimationCommand = new PlayLedAnimationCommand(animation, frequency, miliseconds);

            droneControl.SendCommand(DroneAnimationCommand);
        }

    }
}