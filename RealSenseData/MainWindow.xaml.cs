//--------------------------------------------------------------------------------------
// Copyright 2016 Intel Corporation
// All Rights Reserved
//
// Permission is granted to use, copy, distribute and prepare derivative works of this
// software for any purpose and without fee, provided, that the above copyright notice
// and this statement appear in all copies.  Intel makes no representations about the
// suitability of this software for any purpose.  THIS SOFTWARE IS PROVIDED "AS IS."
// INTEL SPECIFICALLY DISCLAIMS ALL WARRANTIES, EXPRESS OR IMPLIED, AND ALL LIABILITY,
// INCLUDING CONSEQUENTIAL AND OTHER INDIRECT DAMAGES, FOR THE USE OF THIS SOFTWARE,
// INCLUDING LIABILITY FOR INFRINGEMENT OF ANY PROPRIETARY RIGHTS, AND INCLUDING THE
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE.  Intel does not
// assume any responsibility for any errors which may appear in this software nor any
// responsibility to update it.
//--------------------------------------------------------------------------------------
using System;
using System.Windows;
using System.Windows.Media.Imaging;
using System.IO;
using System.Drawing;
using System.Threading;
using System.Windows.Controls;
using Fleck;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using System.Windows.Shapes;
using System.Windows.Media;

namespace RealSenseData
{
    public partial class MainWindow : Window
    {
        private PXCMSenseManager sm;
        private PXCMPersonTrackingData personData;
        private PXCMPersonTrackingModule personModule;
        private PXCMFaceData faceData;
        private PXCMBlobData blobData;
        private Thread update;
        private const int ImageWidth = 640;
        private const int ImageHeight = 480;

        private WebSocketServer server;
        List<IWebSocketConnection> allSockets;
        List<IWebSocketConnection> blobSockets;
        List<IWebSocketConnection> personSockets;

        public MainWindow()
        {
            InitializeComponent();
            ConfigureRealSense();
            ConfigureWebSockets();

            // Start the Update (data acquisition) thread
            update = new Thread(new ThreadStart(Update));
            update.Start();
        }

        private void ConfigureWebSockets()
        {
            allSockets = new List<IWebSocketConnection>();
            blobSockets = new List<IWebSocketConnection>();
            personSockets = new List<IWebSocketConnection>();

            server = new WebSocketServer("ws://0.0.0.0:8181");
            server.ListenerSocket.NoDelay = true;
            server.Start(socket =>
            {
                socket.OnOpen = () =>
                {
                    Console.WriteLine("Open!");
                    allSockets.Add(socket);
                };
                socket.OnClose = () =>
                {
                    Console.WriteLine("Close!");
                    allSockets.Remove(socket);
                    blobSockets.Remove(socket);
                    personSockets.Remove(socket);
                };
                socket.OnMessage = (message) => 
                {
                    dynamic req = JsonConvert.DeserializeObject(message);
                    if (req.type == "blob")
                        blobSockets.Add(socket);
                    else if (req.type == "person")
                        personSockets.Add(socket);

                    //socket.Send(message);
                    //allSockets.ToList().ForEach(s => s.Send("Echo: " + message));
                };
            });
        }

        private void ConfigureRealSense()
        {
            try
            {
                // Create the SenseManager instance  
                sm = PXCMSenseManager.CreateInstance();

                // Enable the color stream
                sm.EnableStream(PXCMCapture.StreamType.STREAM_TYPE_COLOR, ImageWidth, ImageHeight, 30);

                // Enable person tracking
                sm.EnablePersonTracking();
                personModule = sm.QueryPersonTracking();
                PXCMPersonTrackingConfiguration personConfig = personModule.QueryConfiguration();
                personConfig.SetTrackedAngles(PXCMPersonTrackingConfiguration.TrackingAngles.TRACKING_ANGLES_ALL);

                // Enable skeleton tracking - not supported on r200?
                //PXCMPersonTrackingConfiguration.SkeletonJointsConfiguration skeletonConfig = personConfig.QuerySkeletonJoints();
                //skeletonConfig.Enable();
           
                // Enable the face module
                sm.EnableFace();
                PXCMFaceModule faceModule = sm.QueryFace();
                PXCMFaceConfiguration faceConfig = faceModule.CreateActiveConfiguration();
                faceConfig.SetTrackingMode(PXCMFaceConfiguration.TrackingModeType.FACE_MODE_COLOR_PLUS_DEPTH);
                faceConfig.strategy = PXCMFaceConfiguration.TrackingStrategyType.STRATEGY_APPEARANCE_TIME;
                faceConfig.detection.maxTrackedFaces = 1;
                faceConfig.ApplyChanges();

                sm.EnableBlob();
                PXCMBlobModule blobModule = sm.QueryBlob();
                PXCMBlobConfiguration blobConfig = blobModule.CreateActiveConfiguration();
                blobConfig.SetMaxBlobs(4); // 4 is the max
                blobConfig.SetMaxDistance(2000); // in mm's
                blobConfig.ApplyChanges();

                //initialize the SenseManager
                sm.Init();
                faceData = faceModule.CreateOutput();
                blobData = blobModule.CreateOutput();

                // Mirror the image
                sm.QueryCaptureManager().QueryDevice().SetMirrorMode(PXCMCapture.Device.MirrorMode.MIRROR_MODE_HORIZONTAL);

                // Release resources
                personConfig.Dispose();
                faceConfig.Dispose();
                faceModule.Dispose();
                blobConfig.Dispose();
                blobModule.Dispose();
            }
            catch (Exception)
            {
                MessageBox.Show("Unable to configure the RealSense camera. Please make sure a R200 camera is connected.", "System Error");
                throw;
            }
        }

        private void Update()
        {
            // Start AcquireFrame-ReleaseFrame loop
            while (sm.AcquireFrame(true) >= pxcmStatus.PXCM_STATUS_NO_ERROR)
            {
                // Acquire color image data
                PXCMCapture.Sample sample = sm.QuerySample();
                Bitmap colorBitmap;
                PXCMImage.ImageData colorData;
                sample.color.AcquireAccess(PXCMImage.Access.ACCESS_READ, PXCMImage.PixelFormat.PIXEL_FORMAT_RGB32, out colorData);
                colorBitmap = colorData.ToBitmap(0, sample.color.info.width, sample.color.info.height);

                // Create an instance of MyTrackedPerson
                MyTrackedPerson myTrackedPerson = new MyTrackedPerson();
                MyBlobs myBlobs = new MyBlobs();

                // Acquire person tracking data
                personData = personModule.QueryOutput();
                myTrackedPerson.PersonsDetected = personData.QueryNumberOfPeople();

                if (myTrackedPerson.PersonsDetected == 1)
                {
                    PXCMPersonTrackingData.Person trackedPerson = personData.QueryPersonData(PXCMPersonTrackingData.AccessOrderType.ACCESS_ORDER_BY_ID, 0);
                    PXCMPersonTrackingData.PersonTracking trackedPersonData = trackedPerson.QueryTracking();
                    PXCMPersonTrackingData.BoundingBox2D personBox = trackedPersonData.Query2DBoundingBox();
                    myTrackedPerson.X = personBox.rect.x;
                    myTrackedPerson.Y = personBox.rect.y;
                    myTrackedPerson.H = personBox.rect.h;
                    myTrackedPerson.W = personBox.rect.w;

                    /*
                    PXCMPersonTrackingData.PersonJoints personJoints = trackedPerson.QuerySkeletonJoints();
                    PXCMPersonTrackingData.PersonJoints.SkeletonPoint[] skeletonPoints = new PXCMPersonTrackingData.PersonJoints.SkeletonPoint[personJoints.QueryNumJoints()];
                    trackedPerson.QuerySkeletonJoints().QueryJoints(skeletonPoints);
                    if (skeletonPoints.Length > 0)
                        skeletonPoints[0].GetType();
                    */
                }

                // Acquire face tracking data
                faceData.Update();
                myTrackedPerson.FacesDetected = faceData.QueryNumberOfDetectedFaces();

                if (myTrackedPerson.FacesDetected == 1)
                {
                    PXCMFaceData.Face face = faceData.QueryFaceByIndex(0);
                    PXCMFaceData.DetectionData faceDetectionData = face.QueryDetection();
                    PXCMRectI32 faceRectangle;
                    faceDetectionData.QueryBoundingRect(out faceRectangle);
                    myTrackedPerson.FaceH = faceRectangle.h;
                    myTrackedPerson.FaceW = faceRectangle.w;
                    myTrackedPerson.FaceX = faceRectangle.x;
                    myTrackedPerson.FaceY = faceRectangle.y;
                    float faceDepth;
                    faceDetectionData.QueryFaceAverageDepth(out faceDepth);
                    myTrackedPerson.FaceDepth = faceDepth;
                }

                blobData.Update();
                int numBlobs = blobData.QueryNumberOfBlobs();
                myBlobs.numBlobs = numBlobs;
                myBlobs.blobs = new List<List<PXCMPointI32>>(numBlobs);
                myBlobs.closestPoints = new List<PXCMPoint3DF32>(4);
                for (int i = 0; i < numBlobs; i++)
                {
                    PXCMBlobData.IBlob blob;
                    pxcmStatus result1 = blobData.QueryBlob(i, PXCMBlobData.SegmentationImageType.SEGMENTATION_IMAGE_DEPTH, PXCMBlobData.AccessOrderType.ACCESS_ORDER_NEAR_TO_FAR, out blob);
                    if (result1 == pxcmStatus.PXCM_STATUS_NO_ERROR)
                    {
                        PXCMPoint3DF32 closestPoint = blob.QueryExtremityPoint(PXCMBlobData.ExtremityType.EXTREMITY_CLOSEST);
                        myBlobs.closestPoints.Add(closestPoint);

                        int numContours = blob.QueryNumberOfContours();
                        if (numContours > 0)
                        {
                            // only deal with outer contour
                            for (int j = 0; j < numContours; j++)
                            {
                                PXCMBlobData.IContour contour;
                                pxcmStatus result2 = blob.QueryContour(j, out contour);
                                if (result2 == pxcmStatus.PXCM_STATUS_NO_ERROR)
                                {
                                    if (contour.IsOuter())
                                    {
                                        PXCMPointI32[]  points;
                                        pxcmStatus result3 = contour.QueryPoints(out points);
                                        if (result3 == pxcmStatus.PXCM_STATUS_NO_ERROR)
                                        {
                                            int numPoints = points.Length;
                                            myBlobs.blobs.Add(points.ToList<PXCMPointI32>());
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                // Update UI
                Render(colorBitmap, myTrackedPerson, myBlobs);

                // serialize to json and send all clients

                var personJson = JsonConvert.SerializeObject(myTrackedPerson);
                personSockets.ToList().ForEach(s => s.Send(personJson));

                var blobJson = JsonConvert.SerializeObject(myBlobs);
                blobSockets.ToList().ForEach(s => s.Send(blobJson));

                // deserialize json as follows
                //MyTrackedPerson deserializedProduct = JsonConvert.DeserializeObject<MyTrackedPerson>(json);

                // Release resources
                colorBitmap.Dispose();
                sample.color.ReleaseAccess(colorData);
                sm.ReleaseFrame();
            }
        }

        private void Render(Bitmap bitmap, MyTrackedPerson myTrackedPerson, MyBlobs myBlobs)
        {
            Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(delegate ()
            {
                // Update the bitmap image
                BitmapImage bitmapImage = ConvertBitmap(bitmap);

                if (bitmapImage != null)
                {
                    imgStream.Source = bitmapImage;
                }

                // Update the data labels
                lblFacesDetected.Content = string.Format("Faces Detected: {0}", myTrackedPerson.FacesDetected);
                lblFaceH.Content = string.Format("Face Rect H: {0}", myTrackedPerson.FaceH);
                lblFaceW.Content = string.Format("Face Rect W: {0}", myTrackedPerson.FaceW);
                lblFaceX.Content = string.Format("Face Coord X: {0}", myTrackedPerson.FaceX);
                lblFaceY.Content = string.Format("Face Coord Y: {0}", myTrackedPerson.FaceY);
                lblFaceDepth.Content = string.Format("Face Depth: {0}", myTrackedPerson.FaceDepth);
                lblNumberPersons.Content = string.Format("Persons Detected: {0}", myTrackedPerson.PersonsDetected);
                lblPersonH.Content = string.Format("Person Rect H: {0}", myTrackedPerson.H);
                lblPersonW.Content = string.Format("Person Rect W: {0}", myTrackedPerson.W);
                lblPersonX.Content = string.Format("Person Coord X: {0}", myTrackedPerson.X);
                lblPersonY.Content = string.Format("Person Coord Y: {0}", myTrackedPerson.Y);

                // Show or hide the markers
                if (chkShowMarkers.IsChecked == true)
                {
                    if (myTrackedPerson.FacesDetected == 1)
                    {
                        rectFaceMarker.Height = myTrackedPerson.FaceH;
                        rectFaceMarker.Width = myTrackedPerson.FaceW;
                        Canvas.SetLeft(rectFaceMarker, myTrackedPerson.FaceX);
                        Canvas.SetTop(rectFaceMarker, myTrackedPerson.FaceY);
                        rectFaceMarker.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        rectFaceMarker.Visibility = Visibility.Hidden;
                    }

                    if (myTrackedPerson.PersonsDetected == 1)
                    {
                        rectPersonMarker.Height = myTrackedPerson.H;
                        rectPersonMarker.Width = myTrackedPerson.W;
                        Canvas.SetLeft(rectPersonMarker, myTrackedPerson.X);
                        Canvas.SetTop(rectPersonMarker, myTrackedPerson.Y);
                        rectPersonMarker.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        rectPersonMarker.Visibility = Visibility.Hidden;
                    }

                    // draw blobs' contours (outer only)
                    // actually, don't bother drawing because it's stupid slow
                    /*
                    if (myBlobs.numBlobs > 0)
                    {
                        for (int i = 0; i < myBlobs.numBlobs; i++)
                        {
                            List<PXCMPointI32> currentBlob = myBlobs.blobs[i];

                            for (int j = 0; j < currentBlob.Count; j++)
                            { 
                                Ellipse dot = new Ellipse();
                                dot.Width = 2;
                                dot.Height = 2;

                                SolidColorBrush blueBrush = new SolidColorBrush();
                                blueBrush.Color = Colors.Blue;
                                dot.Fill = blueBrush;

                                Canvas.SetLeft(dot, currentBlob[0].x);
                                Canvas.SetTop(dot, currentBlob[0].y);

                                //MainCanvas.Children.Add(dot);
                            }
                        }
                    }
                    */
                }
                else
                {
                    rectFaceMarker.Visibility = Visibility.Hidden;
                    rectPersonMarker.Visibility = Visibility.Hidden;
                }
            }));
        }

        private BitmapImage ConvertBitmap(Bitmap bitmap)
        {
            BitmapImage bitmapImage = null;

            if (bitmap != null)
            {
                MemoryStream memoryStream = new MemoryStream();
                bitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Bmp);
                memoryStream.Position = 0;
                bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memoryStream;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze();
            }

            return bitmapImage;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            update.Abort();
            personData.Dispose();
            personModule.Dispose();
            faceData.Dispose();
            blobData.Dispose();
            sm.Dispose();
        }
    }
}
