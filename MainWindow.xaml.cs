namespace KinectProject
{
    using System;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using System.Drawing;
    using System.Collections.Generic;

    using Kinect.Toolbox;
    using Microsoft.Kinect;
    using Microsoft.Kinect.Toolkit;
    using Microsoft.Kinect.Toolkit.Controls;
    using Microsoft.Kinect.Toolkit.BackgroundRemoval;
 
    public partial class MainWindow : Window, IDisposable
    {

        private const DepthImageFormat DepthFormat = DepthImageFormat.Resolution320x240Fps30;

        private const ColorImageFormat ColorFormat = ColorImageFormat.RgbResolution640x480Fps30;

        private WriteableBitmap foregroundBitmap;

        private KinectSensorChooser sensorChooser;

        private BackgroundRemovedColorStream backgroundRemovedColorStream;

        private Skeleton[] skeletons;

        private SwipeGestureDetector swipeGestureRecognizer;

        private int currentlyTrackedSkeletonId;

        private bool disposed;

        private List<Bitmap> backgrounds = new List<Bitmap>();

        private int position = 0;

        public MainWindow()
        {
            this.InitializeComponent();

            // initialize the sensor chooser and UI
            this.sensorChooser = new KinectSensorChooser();
            this.sensorChooserUi.KinectSensorChooser = this.sensorChooser;
            this.sensorChooser.KinectChanged += this.SensorChooserOnKinectChanged;
            this.sensorChooser.Start();

            backgrounds.Add(new Bitmap(Properties.Resources.image1));
            backgrounds.Add(new Bitmap(Properties.Resources.image2));
            backgrounds.Add(new Bitmap(Properties.Resources.image3));
            backgrounds.Add(new Bitmap(Properties.Resources.image4));
            backgrounds.Add(new Bitmap(Properties.Resources.image5));
            backgrounds.Add(new Bitmap(Properties.Resources.image6));
            backgrounds.Add(new Bitmap(Properties.Resources.image7));
            backgrounds.Add(new Bitmap(Properties.Resources.image8));

            createButtons();
            setBackground();
            InitializeGestures();
        }

        ~MainWindow()
        {
            this.Dispose(false);
        }

        // Dispose
        public void Dispose()
        {
            this.Dispose(true);

            // This object will be cleaned up by the Dispose method.
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed && false)
            {
                if (null != this.backgroundRemovedColorStream)
                {
                    this.backgroundRemovedColorStream.Dispose();
                    this.backgroundRemovedColorStream = null;
                }
                
                this.disposed = true;
            }
        }

        // Draw
        private void prevBackground()
        {
            var count = backgrounds.Count;
            position -= 1;
            if (position == -1)
                position = count - 1;
            setBackground();
        }

        private void nextBackground()
        {
            var count = backgrounds.Count;
            position += 1;
            if (count == position)
                position = 0;
            setBackground();
        }

        private void setBackground()
        {
            ImageSourceConverter c = new ImageSourceConverter();
            Bitmap bitmap = backgrounds[position];

            System.Windows.Media.Imaging.BitmapSource b = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                bitmap.GetHbitmap(), IntPtr.Zero, System.Windows.Int32Rect.Empty, 
                BitmapSizeOptions.FromWidthAndHeight(bitmap.Width, bitmap.Height)
            );

            Backdrop.Source = b;
        }

        // Actions
        private void createButtons()
        {
            for (int i = 1; i <= backgrounds.Count; i++)
            {
                KinectCircleButton btn = new KinectCircleButton();

                btn.Name = "button_" + i.ToString();
                btn.Content = i.ToString();
                btn.Height = 80;
                btn.Click += OnClickRandomButton;

                scrollContent.Children.Add(btn);
            }
        }

        private void OnClickRandomButton(object sender, RoutedEventArgs e)
        {
            KinectCircleButton btn = (KinectCircleButton) e.Source;
            position = int.Parse(btn.Content.ToString()) - 1;
            setBackground();
        }

        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            this.sensorChooser.Stop();
            this.sensorChooser = null;
        }

        // Gestures 
        private void InitializeGestures()
        {
            swipeGestureRecognizer = new SwipeGestureDetector();
            swipeGestureRecognizer.OnGestureDetected += OnGestureDetected;
        }

        private void OnGestureDetected(string gesture)
        {

            this.statusBarText.Text = gesture;

            System.Console.WriteLine(gesture);

            switch (gesture)
            {
                case "SwipeToLeft":
                    prevBackground();
                    break;

                case "SwipeToRight":
                    nextBackground();
                    break;
            }
        }

        // All Sensors
        private void SensorAllFramesReady(object sender, AllFramesReadyEventArgs e)
        {

            // in the middle of shutting down, or lingering events from previous sensor, do nothing here.
            if (null == this.sensorChooser || null == this.sensorChooser.Kinect || this.sensorChooser.Kinect != sender)
            {
                return;
            }

            try
            {
                using (var depthFrame = e.OpenDepthImageFrame())
                {
                    if (null != depthFrame)
                    {
                        this.backgroundRemovedColorStream.ProcessDepth(depthFrame.GetRawPixelData(), depthFrame.Timestamp);
                    }
                }

                using (var colorFrame = e.OpenColorImageFrame())
                {
                    if (null != colorFrame)
                    {
                        this.backgroundRemovedColorStream.ProcessColor(colorFrame.GetRawPixelData(), colorFrame.Timestamp);
                    }
                }

                using (var skeletonFrame = e.OpenSkeletonFrame())
                {
                    if (null != skeletonFrame)
                    {
                        skeletonFrame.CopySkeletonDataTo(this.skeletons);

                        foreach (var skel in this.skeletons)
                        {
                            if (null == skel)
                            {
                                continue;
                            }

                            if (skel.TrackingState != SkeletonTrackingState.Tracked)
                            {
                                continue;
                            }

                           if (skel.TrackingId == this.currentlyTrackedSkeletonId)
                           {
                                foreach (Joint joint in skel.Joints)
                                {
                                    
                                    if (joint.JointType == JointType.HandLeft)
                                    {
                                        swipeGestureRecognizer.Add(joint.Position, sensorChooser.Kinect);
                                    }

                                    if (joint.JointType == JointType.HandRight)
                                    {
                                        swipeGestureRecognizer.Add(joint.Position, sensorChooser.Kinect);
                                    }
                                }
                            }
                        }

                        this.backgroundRemovedColorStream.ProcessSkeleton(this.skeletons, skeletonFrame.Timestamp);
                    }
                }

                this.ChooseSkeleton();
            }
            catch (InvalidOperationException)
            {
                // Ignore the exception. 
            }
        }

        private void BackgroundRemovedFrameReadyHandler(object sender, BackgroundRemovedColorFrameReadyEventArgs e)
        {
            using (var backgroundRemovedFrame = e.OpenBackgroundRemovedColorFrame())
            {
                if (backgroundRemovedFrame != null)
                {
                    if (null == this.foregroundBitmap || this.foregroundBitmap.PixelWidth != backgroundRemovedFrame.Width 
                        || this.foregroundBitmap.PixelHeight != backgroundRemovedFrame.Height)
                    {
                        this.foregroundBitmap = new WriteableBitmap(backgroundRemovedFrame.Width, backgroundRemovedFrame.Height, 96.0, 96.0, PixelFormats.Bgra32, null);

                        // Set the image we display to point to the bitmap where we'll put the image data
                        this.MaskedColor.Source = this.foregroundBitmap;
                    }

                    // Write the pixel data into our bitmap
                    this.foregroundBitmap.WritePixels(
                        new Int32Rect(0, 0, this.foregroundBitmap.PixelWidth, this.foregroundBitmap.PixelHeight),
                        backgroundRemovedFrame.GetRawPixelData(),
                        this.foregroundBitmap.PixelWidth * sizeof(int),
                        0);
                }
            }
        }

        private void ChooseSkeleton()
        {
            var isTrackedSkeltonVisible = false;
            var nearestDistance = float.MaxValue;
            var nearestSkeleton = 0;

            foreach (var skel in this.skeletons)
            {
                if (null == skel)
                {
                    continue;
                }

                if (skel.TrackingState != SkeletonTrackingState.Tracked)
                {
                    continue;
                }

                if (skel.TrackingId == this.currentlyTrackedSkeletonId)
                {
                    isTrackedSkeltonVisible = true;
                    break;
                }

                if (skel.Position.Z < nearestDistance)
                {
                    nearestDistance = skel.Position.Z;
                    nearestSkeleton = skel.TrackingId;
                }
            }

            if (!isTrackedSkeltonVisible && nearestSkeleton != 0)
            {
                this.backgroundRemovedColorStream.SetTrackedPlayer(nearestSkeleton);
                this.currentlyTrackedSkeletonId = nearestSkeleton;
            }
        }

        private void SensorChooserOnKinectChanged(object sender, KinectChangedEventArgs args)
        {
            bool error = false;

            if (args.OldSensor != null)
            {
                try
                {
                    args.OldSensor.AllFramesReady -= this.SensorAllFramesReady;
                    args.OldSensor.DepthStream.Disable();
                    args.OldSensor.ColorStream.Disable();
                    args.OldSensor.SkeletonStream.Disable();

                    // Create the background removal stream to process the data and remove background, and initialize it.
                    if (null != this.backgroundRemovedColorStream)
                    {
                        this.backgroundRemovedColorStream.BackgroundRemovedFrameReady -= this.BackgroundRemovedFrameReadyHandler;
                        this.backgroundRemovedColorStream.Dispose();
                        this.backgroundRemovedColorStream = null;
                    }
                }
                catch (InvalidOperationException)
                {
                    error = true;
                }
            }


            if (args.NewSensor != null)
            {
                try
                {
                    args.NewSensor.DepthStream.Enable(DepthFormat);
                    args.NewSensor.ColorStream.Enable(ColorFormat);
                    args.NewSensor.SkeletonStream.Enable();


                    this.backgroundRemovedColorStream = new BackgroundRemovedColorStream(args.NewSensor);
                    this.backgroundRemovedColorStream.Enable(ColorFormat, DepthFormat);

                    // Allocate space to put the depth, color, and skeleton data we'll receive
                    if (null == this.skeletons)
                    {
                        this.skeletons = new Skeleton[args.NewSensor.SkeletonStream.FrameSkeletonArrayLength];
                    }

                    // Add an event handler to be called when the background removed color frame is ready, so that we can
                    // composite the image and output to the app
                    this.backgroundRemovedColorStream.BackgroundRemovedFrameReady += this.BackgroundRemovedFrameReadyHandler;

                    // Add an event handler to be called whenever there is new depth frame data
                    args.NewSensor.AllFramesReady += this.SensorAllFramesReady;

                    try
                    {
                        args.NewSensor.DepthStream.Range = DepthRange.Default;
                        args.NewSensor.SkeletonStream.EnableTrackingInNearRange = true;
                    }
                    catch (InvalidOperationException)
                    {
                        // Non Kinect for Windows devices do not support Near mode, so reset back to default mode.
                        args.NewSensor.DepthStream.Range = DepthRange.Default;
                        args.NewSensor.SkeletonStream.EnableTrackingInNearRange = false;
                    }
                   
                    try
                    {
                        kinectRegion.KinectSensor = args.NewSensor;
                    }
                    catch (InvalidOperationException)
                    {

                    }
                    
                }
                catch (InvalidOperationException)
                {
                    error = true;
                }



            }

        }

    }
}
