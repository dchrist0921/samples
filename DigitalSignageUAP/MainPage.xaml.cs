﻿using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;
using Windows.UI.Core;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace DigitalSignageUAP
{
    /// <summary>
    /// singleton-similar style to maintain only one instance of the timer, and make sure it's not controlled by the MainPage, but actually global
    /// </summary>
    static class GlobalTimerWrapper
    {
        static DispatcherTimer heartBeatTimer;
        static DispatcherTimer reloadContentTimer;
        static private Page currentPage;
        static readonly TimeSpan heartbeatDuration = new TimeSpan(0, 30, 0);
        static readonly TimeSpan reloadContentIntervalDuration = new TimeSpan(0, 1, 0);

        // maintain a global counter of heartbeat events
        // when the timer ticks, upload the duration as counter*30 minutes because the timer ticks every 30 minutes
        // this is to avoid any thing like system time being updated when app is runningS
        static int heartBeatCounter = 0;
        static public void StartHeartBeat()
        {
            if (heartBeatTimer == null)
            {
                heartBeatTimer = new DispatcherTimer();
                heartBeatTimer.Interval = heartbeatDuration;
                heartBeatTimer.Tick += heartBeatTimer_Tick;
                heartBeatTimer.Start();
                TelemetryHelper.eventLogger.Write(TelemetryHelper.HeartbeatEvent, TelemetryHelper.TelemetryInfoOption, new
                {
                    duration = 0,
                }); // send a heartbeat at the very begining
            }

            // if it's already initialized, do nothing
        }

        static public void StartReloadContentTimer(Page page)
        {
            currentPage = page;
            if (reloadContentTimer == null)
            {
                reloadContentTimer = new DispatcherTimer();
                reloadContentTimer.Interval = reloadContentIntervalDuration;
                reloadContentTimer.Tick += reloadContentTimer_Tick; // ensure this is only added once
                reloadContentTimer.Start();
            }
        }

        /// <summary>
        /// when the timer ticks, check if it's mid night, if yes, reload slideshowpage and trigger a content resync
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        static void reloadContentTimer_Tick(object sender, object e)
        {
            DateTime time = DateTime.Now;
            if (time.Hour == 0 && time.Minute == 0) // we want the content reload happens only at 0:00 every day, exact mid-night
            {
                // go to main page and reload slideshow page would trigger a content reload
                currentPage.Frame.Navigate(typeof(MainPage));
                currentPage.Frame.Navigate(typeof(SlideshowPage));
            }
        }

        /// <summary>
        /// when the timer ticks, send heartBeatCounter*30 duration in minutes for telemetry
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        static void heartBeatTimer_Tick(object sender, object e)
        {
            TelemetryHelper.eventLogger.Write(TelemetryHelper.HeartbeatEvent, TelemetryHelper.TelemetryInfoOption, new
            {
                duration = ++heartBeatCounter * 30,
            });
        }
    }
    /// <summary>
    /// Mainpage of the digital signage app
    /// </summary>
    public sealed partial class MainPage : Page
    {
        static SettingsFlyout1 flyout = new SettingsFlyout1();
        static bool MainPageFirstTimeLoad = true;
        string vszVisionVideoPath = "http://iot-digisign01/ds/IoTVision.wmv";
        AppServiceConnection _appServiceConnection;

        // once the vision video is started, this timer starts, after it ticks, mouse movement will stop the slideshow
        DispatcherTimer VisionVideoAcceptMouseEventTimer = new DispatcherTimer();

        // this is a timer to start slideshow automatically if mainpage is loaded and no user action for 5 minutes.
        DispatcherTimer slideShowAutoStart = new DispatcherTimer();

        static readonly TimeSpan slideShowAutoStartTimerSpan = new TimeSpan(0, 1, 0);
        static readonly TimeSpan VisionVideoAcceptInputTimeSpan = new TimeSpan(0, 0, 1);

        public MainPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = Windows.UI.Xaml.Navigation.NavigationCacheMode.Enabled;
            GlobalTimerWrapper.StartHeartBeat();

            VisionVideoAcceptMouseEventTimer.Interval = VisionVideoAcceptInputTimeSpan;
            VisionVideoAcceptMouseEventTimer.Tick += VisionVideoAcceptMouseEventTimer_Tick;
            Window.Current.CoreWindow.KeyDown += CoreWindow_KeyDown;
            this.PointerMoved += MainPage_PointerMoved;

            // put some special initialization here, that we only want to call once
            if (MainPageFirstTimeLoad)
            {
                Application.Current.UnhandledException += Current_UnhandledException;
                MainPageFirstTimeLoad = false;
            }

            // restart the timer every time navigate to mainpage
            slideShowAutoStart.Interval = slideShowAutoStartTimerSpan;
            slideShowAutoStart.Tick += slideShowAutoStart_Tick;
            slideShowAutoStart.Start(); // any mouse/keyboard event will restart the timer
        }
        
        void VisionVideoAcceptMouseEventTimer_Tick(object sender, object e)
        {
            VisionVideoAcceptMouseEventTimer.Stop();
        }

        /// <summary>
        /// whenever a mouse event happens, and the timer is stopped, go back to mainpage
        /// the timer is to give user some tolerance to start the video and play
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void MainPage_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (visionVideoInstance.IsFullWindow && VisionVideoAcceptMouseEventTimer.IsEnabled == false)
                {
                visionVideoInstance.Pause();
                visionVideoInstance.AutoPlay = false;
                visionVideoInstance.IsFullWindow = false;
                visionVideoInstance.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            }
        }
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            slideShowAutoStart.Start();

            InitAppSvc();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            slideShowAutoStart.Stop();
        }

        /// <summary>
        /// whenever a keyboard event happens, and the timer is stopped, go back to mainpage
        /// the timer is to give user some tolerance to start the video and play
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CoreWindow_KeyDown(CoreWindow sender, KeyEventArgs args)
        {
            if (visionVideoInstance.IsFullWindow && VisionVideoAcceptMouseEventTimer.IsEnabled == false)
                {
                visionVideoInstance.Pause();
                visionVideoInstance.AutoPlay = false;
                visionVideoInstance.IsFullWindow = false;
                visionVideoInstance.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            }
        }

        /// <summary>
        /// upload any unhandled exception as telemetry event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Current_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            TelemetryHelper.eventLogger.Write(TelemetryHelper.UnhandledExceptionEvent, TelemetryHelper.DebugErrorOption, new
            {
                ExceptionMessage = e.Message
            });
        }

        /// <summary>
        /// start slide show once this timer ticks
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void slideShowAutoStart_Tick(object sender, object e)
        {
            if (visionVideoInstance.CurrentState == MediaElementState.Playing)
                return;

            slideShowAutoStart.Stop();
            this.Frame.Navigate(typeof(SlideshowPage));
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SlideshowGrid_Tapped(object sender, TappedRoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(SlideshowPage));
            // GlobalTimerWrapper.StartReloadContentTimer(this.Frame);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BrowserGrid_Tapped(object sender, TappedRoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(BrowserPage));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SettingGrid_Tapped(object sender, TappedRoutedEventArgs e)
        {
            flyout.ShowIndependent();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FeedbackGrid_Tapped(object sender, TappedRoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(FeedbackPage));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void visionVideoGrid_Tapped(object sender, TappedRoutedEventArgs e)
        {
            TelemetryHelper.eventLogger.Write(TelemetryHelper.VisionVideoEvent, TelemetryHelper.TelemetryStartOption);
            visionVideoInstance.Source = new Uri(vszVisionVideoPath);
            visionVideoInstance.IsFullWindow = true;
            visionVideoInstance.Visibility = Windows.UI.Xaml.Visibility.Visible;
            visionVideoInstance.Play();
            visionVideoInstance.AutoPlay = true;
            VisionVideoAcceptMouseEventTimer.Start();
        }

        /// <summary>
        /// any user interaction will reset this timer, this is for the auto-play of the slideshow
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Page_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            slideShowAutoStart.Stop();
            slideShowAutoStart.Start();
        }

        /// <summary>
        /// any user interaction will reset this timer, this is for the auto-play of the slideshow
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Page_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            slideShowAutoStart.Stop();
            slideShowAutoStart.Start();
        }

        /// <summary>
        /// Initialize the connection to the Motion Sensor app service
        /// </summary>
        private async void InitAppSvc()
        {
            if (false) //(_appServiceConnection == null)
            {
                // Initialize the AppServiceConnection
                _appServiceConnection = new AppServiceConnection();
                _appServiceConnection.PackageFamilyName = "MotionSensorCBT_w7mxzscebfmq4";
                _appServiceConnection.AppServiceName = "DigitalSignAppService";
                _appServiceConnection.ServiceClosed += _appServiceConnection_ServiceClosed;

                // Send a initialize request 
                var res = await _appServiceConnection.OpenAsync();
                if (res == AppServiceConnectionStatus.Success)
                {
                    var message = new ValueSet();
                    message.Add("Command", "Initialize");
                    var response = await _appServiceConnection.SendMessageAsync(message);
                    if (response.Status != AppServiceResponseStatus.Success)
                    {
                        _appServiceConnection = null;
                        throw new Exception("Failed to send message");
                    }
                    _appServiceConnection.RequestReceived += OnMessageReceived;
                }
            }
        }

        private void _appServiceConnection_ServiceClosed(AppServiceConnection sender, AppServiceClosedEventArgs args)
        {
            // If the service was closed, restart it
            _appServiceConnection = null;
            InitAppSvc();
        }


        /// <summary>
        /// Event handler for messages sent from the Motion Sensor CBT
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private async void OnMessageReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            var deferral = args.GetDeferral();

            var message = args.Request.Message;
            string text = message["Message"] as string;
            if ("MotionDetected".Equals(text))
            {
                await Dispatcher.RunAsync(
                    CoreDispatcherPriority.High,
                    () =>
                    {
                        if (this.Frame.CurrentSourcePageType != typeof(MainPage))
                        {
                            this.Frame.Navigate(typeof(MainPage));
                        }
                        else
                        {
                            MainPage_PointerMoved(this, null);
                            Page_PointerMoved(this, null);
                        }
                    });
            }

            deferral.Complete();
        }
    }
}
