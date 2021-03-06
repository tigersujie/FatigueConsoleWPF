﻿using Microsoft.Kinect;
using Microsoft.Kinect.Face;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;

namespace Faigute_WPF
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow
    {
        /// <summary>
        /// 深度数据数组用于转换成视觉图像
        /// </summary>
        private byte[] depthPixels = null;

        //疲劳监测器
        Faigute myClock=new Faigute();

        /// <summary>
        /// 各种帧的描述
        /// </summary>
        FrameDescription infraredFrameDescription = null;
        FrameDescription depthFrameDescription = null;

        //kinect主体
        private KinectSensor kinectSensor = null;

        //坐标映射器
        private CoordinateMapper coordinateMapper = null;

        //复源帧读取器
        private MultiSourceFrameReader multiFrameSourceReader = null;

        //骨骼帧
        private BodyFrameReader bodyFrameReader = null;

        //人体索引数组
        private Body[] bodies = null;

        //被捕捉到的人体的数
        private int bodyCount;

        //面部资源数组
        private FaceFrameSource[] faceFrameSources = null;

        /// <summary>
        /// 深度帧读取器
        /// </summary>
        private DepthFrameReader depthFrameReader = null;
        //面部帧读取器
        private FaceFrameReader[] faceFrameReaders = null;
        //人体索引读取器
        private BodyIndexFrameReader bodyIndexFrameReader = null;

        //彩色帧和深度帧结合映射的点
        private DepthSpacePoint[] colorMappedToDepthPoints = null;

        //面部基本信息仓库数组
        private FaceFrameResult[] faceFrameResults = null;

        //显示的宽度
        private int displayWidth;

        //显示的高度
        private int displayHeight;
      
        /// <summary>
        /// 模型创建的线程
        /// </summary>
        private BackgroundWorker CorrectModule;

        /// <summary>
        /// 驾驶员锁定模块的线程
        /// </summary>
        private BackgroundWorker runDriverIndex;

        //人体索引
        MyQueue[] DriverIndex=new MyQueue[6]; 

        /// <summary>
        /// 索引数据转换成彩色图像
        /// </summary>
        private uint[] bodyIndexPixels = null;

        //人体索引图像描述
        private FrameDescription bodyIndexFrameDescription = null;

        /// <summary>
        /// 驾驶员索引
        /// </summary>
        private int myBodyIndex = -1;

        /// <summary>
        /// 驾驶员平均深度值
        /// </summary>
        private double myBodyDepth = 0.0f;

        /// <summary>
        /// 收集用于显示BodyIndexFrame数据的颜色
        /// </summary>
        private static readonly uint[] BodyColor =
        {
            0x0000FF00,
            0x00FF0000,
            0xFFFF4000,
            0x40FFFF00,
            0xFF40FF00,
            0xFF808000,
        };

        //疲劳值
        private double NumeFaigute;

        public MainWindow()
        {
            // 获取一个默认的传感器
            this.kinectSensor = KinectSensor.GetDefault();

            //获取坐标映射器
            this.coordinateMapper = this.kinectSensor.CoordinateMapper;

            //获取帧描述
            FrameDescription frameDescription = this.kinectSensor.ColorFrameSource.FrameDescription;
            this.depthFrameDescription = this.kinectSensor.DepthFrameSource.FrameDescription;

            //设置显示的图像属性
            this.displayWidth = frameDescription.Width;
            this.displayHeight = frameDescription.Height;
            //打开骨骼帧读取器
            this.bodyFrameReader = this.kinectSensor.BodyFrameSource.OpenReader();

            //打开深度帧读取器
            this.depthFrameReader = this.kinectSensor.DepthFrameSource.OpenReader();
            //打开人体索引帧读取器
            this.bodyIndexFrameReader = this.kinectSensor.BodyIndexFrameSource.OpenReader();

            //骨骼帧处理机制
            this.bodyFrameReader.FrameArrived += this.Reader_BodyFrameArrived;

            //初始化彩色深度映射
            FrameDescription depthFrameDescription = this.kinectSensor.DepthFrameSource.FrameDescription;
            int depthWidth = depthFrameDescription.Width;
            int depthHeight = depthFrameDescription.Height;
            FrameDescription colorFrameDescription = this.kinectSensor.ColorFrameSource.FrameDescription;
            int colorWidth = colorFrameDescription.Width;
            int colorHeight = colorFrameDescription.Height;
            this.colorMappedToDepthPoints = new DepthSpacePoint[colorWidth * colorHeight];

            //初始化驾驶员锁定模块
            runDriverIndex = new BackgroundWorker();
            //坐标映射器
            this.coordinateMapper = this.kinectSensor.CoordinateMapper;

            //设置传感器可捕捉的人体的最大的数
            this.bodyCount = this.kinectSensor.BodyFrameSource.BodyCount;

            //分配存储骨骼对象
            this.bodies = new Body[this.bodyCount];

            //指定所需的面框结果
            FaceFrameFeatures faceFrameFeatures =
                FaceFrameFeatures.BoundingBoxInColorSpace
                | FaceFrameFeatures.PointsInColorSpace
                | FaceFrameFeatures.RotationOrientation
                | FaceFrameFeatures.FaceEngagement
                | FaceFrameFeatures.Glasses
                | FaceFrameFeatures.Happy
                | FaceFrameFeatures.LeftEyeClosed
                | FaceFrameFeatures.RightEyeClosed
                | FaceFrameFeatures.LookingAway
                | FaceFrameFeatures.MouthMoved
                | FaceFrameFeatures.MouthOpen;

            //创建一个面部帧资源和读取器来跟踪每个人的脸
            this.faceFrameSources = new FaceFrameSource[this.bodyCount];
            this.faceFrameReaders = new FaceFrameReader[this.bodyCount];
            for (int i = 0; i < this.bodyCount; i++)
            {
                //创建所需的人脸特征帧和初始ID为0的面部帧源
                this.faceFrameSources[i] = new FaceFrameSource(this.kinectSensor, 0, faceFrameFeatures);

                //打开面部帧读取器
                this.faceFrameReaders[i] = this.faceFrameSources[i].OpenReader();
            }

            //初始化面部基本信息
            this.faceFrameResults = new FaceFrameResult[this.bodyCount];

            // set IsAvailableChanged event notifier
            this.kinectSensor.IsAvailableChanged += this.Sensor_IsAvailableChanged;

            //初始化人体索引帧描述
            this.bodyIndexFrameDescription = this.kinectSensor.BodyIndexFrameSource.FrameDescription;

            //初始化人体索引彩色图
            this.bodyIndexPixels = new uint[this.bodyIndexFrameDescription.Width * this.bodyIndexFrameDescription.Height];

            //初始化人体坐标数据集
            for(int i = 0; i < 6; i++)
            {
                this.DriverIndex[i] = new MyQueue(this.bodyIndexFrameDescription.Width * this.bodyIndexFrameDescription.Height);
            }
            ///this.DriverIndex = new[6] MyQueue();

            // allocate space to put the pixels being received and converted
            this.depthPixels = new byte[this.depthFrameDescription.Width * this.depthFrameDescription.Height];

            //打开传感器
            this.kinectSensor.Open();

            this.infraredFrameDescription = this.kinectSensor.InfraredFrameSource.FrameDescription;
        }

        /// <summary>
        /// 加载函数
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void MainWindow_Run()
        {
            if (this.depthFrameReader != null)
            {
                this.depthFrameReader.FrameArrived += this.Reader_FrameArrived;
            }

            for (int i = 0; i < this.bodyCount; i++) { 
                if (this.faceFrameReaders[i] != null)
                {
                    // wire handler for face frame arrival
                    this.faceFrameReaders[i].FrameArrived += this.Reader_FaceFrameArrived;
                }
            }
               
            if (this.bodyFrameReader != null)
            {
                this.bodyFrameReader.FrameArrived += this.Reader_BodyFrameArrived;
            }

            if (this.bodyIndexFrameReader != null)
            {
                this.bodyIndexFrameReader.FrameArrived += this.Reader_BodyIndexFrameArrived;
            }
            runDriverIndex.DoWork += RunDriverIndex_DoWork;
        }


        /// <summary>
        /// 驾驶员索引锁定线程运行函数
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private unsafe void  RunDriverIndex_DoWork(object sender, DoWorkEventArgs e)
        {
            DateTime dt = DateTime.Now;
                //String format = "ffffff";
                //DateTime date = DateTime.Now;
                //String start = date.ToString(format, DateTimeFormatInfo.InvariantInfo);
                int index = -1;
                double depth = 0.0f;
                double[] depthDrivers = new double[6];
                if (this.DriverIndex != null)
                {
                    for (int a = 0; a < 6; ++a)
                    {
                        if (this.DriverIndex[a] != null)
                        {
                            int length = this.DriverIndex[a].getData().Length;
                            ///随机取一百个像素点的平均值
                            for(int i=0;i<100;i++)
                            {
                                Random ran = new Random();
                                int j = ran.Next(0, 100);
                                depthDrivers[a] += ((double)(this.frameDatas[this.DriverIndex[a].getData()[j]])) / 100;
                            }
                            ///求所有像素点的平均值
                            /*foreach (long i in this.DriverIndex[a].getData())
                            {
                                depthDrivers[a] += ((double)(this.frameDatas[i]) / (512 * 424));
                            }*/
                        }
                    }

                    double[] functionDD = { 999999, 999999, 999999, 999999, 999999, 99999 };
                    int fddI = 0;
                    for (int a = 0; a < 6; a++)
                    {
                        if (depthDrivers[a] != 0)
                        {
                            functionDD[fddI] = depthDrivers[a];
                            fddI++;
                        }
                    }

                    depth = functionDD.Min();
                    /*StreamWriter minW = new StreamWriter("Min.txt", true);
                    minW.WriteLine(depth);
                    minW.Close();*/
                    for (int a = 0; a < 6; a++)
                    {
                        if (depth == depthDrivers[a])
                            index = a;
                    }
                }
                /*if (index >= 0)
                {
                    ///把计算得到的驾驶员人体索引录入文件中
                    StreamWriter sw = new StreamWriter("m_index.txt", true);
                    sw.WriteLine(index);
                    sw.Close();
                }*/
                //DateTime date2 = DateTime.Now;
                //String start2 = date2.ToString(format, DateTimeFormatInfo.InvariantInfo);
                this.myBodyIndex = index;
                this.myBodyDepth = depth;

            TimeSpan x = DateTime.Now - dt;

            //Console.WriteLine();
            //throw new NotImplementedException();
        }

        /// <summary>
        /// 驾驶员模型纠正模块的完成函数
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CorrectModule_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            StreamWriter sw = new StreamWriter("run.txt", true);
            sw.WriteLine("CorrectModule_RunWorkerCompleted");
            sw.Close();
            //throw new NotImplementedException();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Sensor_IsAvailableChanged(object sender, IsAvailableChangedEventArgs e)
        {
            StreamWriter sw = new StreamWriter("test.txt", true);
            sw.WriteLine("test");
            sw.Close();
            if (this.kinectSensor != null)
            {
                // on failure, set the status text
                Console.WriteLine("Kinect Error");
            }
        }


        private void Reader_MultiSourceFrameArrived(object sender, MultiSourceFrameArrivedEventArgs e)
        {
            int depthWidth = 0;
            int depthHeight = 0;

            DepthFrame depthFrame = null;
            ColorFrame colorFrame = null;
            BodyIndexFrame bodyIndexFrame = null;
            bool isBitmapLocked = false;

            MultiSourceFrame multiSourceFrame = e.FrameReference.AcquireFrame();

            // If the Frame has expired by the time we process this event, return.
            if (multiSourceFrame == null)
            {
                return;
            }

            // We use a try/finally to ensure that we clean up before we exit the function.  
            // This includes calling Dispose on any Frame objects that we may have and unlocking the bitmap back buffer.
            try
            {
                depthFrame = multiSourceFrame.DepthFrameReference.AcquireFrame();
                colorFrame = multiSourceFrame.ColorFrameReference.AcquireFrame();
                bodyIndexFrame = multiSourceFrame.BodyIndexFrameReference.AcquireFrame();

                // If any frame has expired by the time we process this event, return.
                // The "finally" statement will Dispose any that are not null.
                if ((depthFrame == null) || (colorFrame == null) || (bodyIndexFrame == null))
                {
                    return;
                }

                

                // Process Depth
                FrameDescription depthFrameDescription = depthFrame.FrameDescription;

                depthWidth = depthFrameDescription.Width;
                depthHeight = depthFrameDescription.Height;

                // Access the depth frame data directly via LockImageBuffer to avoid making a copy
                // KinectBuffer depthData = depthFrame.LockImageBuffer();
                //uint size = 515*424;
                //depthFrame.CopyFrameDataToIntPtr(depthData.UnderlyingBuffer, depthData.Size);

                /*StreamWriter sw = File.AppendText("depthData.txt");
                  sw.Write(depthData.UnderlyingBuffer);
                  sw.Close();*/

                using (KinectBuffer depthFrameData = depthFrame.LockImageBuffer())
                {
                    this.coordinateMapper.MapColorFrameToDepthSpaceUsingIntPtr(
                        depthFrameData.UnderlyingBuffer,
                        depthFrameData.Size,
                        this.colorMappedToDepthPoints);
                }

                // We're done with the DepthFrame 
                depthFrame.Dispose();
                depthFrame = null;

                // Process Color

                // Lock the bitmap for writing
                //this.bitmap.Lock();
                isBitmapLocked = true;

                //colorFrame.CopyConvertedFrameDataToIntPtr(this.bitmap.BackBuffer, this.bitmapBackBufferSize, ColorImageFormat.Bgra);

                // We're done with the ColorFrame 
                colorFrame.Dispose();
                colorFrame = null;

                // We'll access the body index data directly to avoid a copy
                using (KinectBuffer bodyIndexData = bodyIndexFrame.LockImageBuffer())
                {
                    unsafe
                    {
                        byte* bodyIndexDataPointer = (byte*)bodyIndexData.UnderlyingBuffer;

                        int colorMappedToDepthPointCount = this.colorMappedToDepthPoints.Length;

                        fixed (DepthSpacePoint* colorMappedToDepthPointsPointer = this.colorMappedToDepthPoints)
                        {
                            // Treat the color data as 4-byte pixels
                            //uint* bitmapPixelsPointer = (uint*)this.bitmap.BackBuffer;

                            // Loop over each row and column of the color image
                            // Zero out any pixels that don't correspond to a body index
                            for (int colorIndex = 0; colorIndex < colorMappedToDepthPointCount; ++colorIndex)
                            {
                                float colorMappedToDepthX = colorMappedToDepthPointsPointer[colorIndex].X;
                                float colorMappedToDepthY = colorMappedToDepthPointsPointer[colorIndex].Y;

                                // The sentinel value is -inf, -inf, meaning that no depth pixel corresponds to this color pixel.
                                if (!float.IsNegativeInfinity(colorMappedToDepthX) &&
                                    !float.IsNegativeInfinity(colorMappedToDepthY))
                                {
                                    // Make sure the depth pixel maps to a valid point in color space
                                    int depthX = (int)(colorMappedToDepthX + 0.5f);
                                    int depthY = (int)(colorMappedToDepthY + 0.5f);

                                    // If the point is not valid, there is no body index there.
                                    if ((depthX >= 0) && (depthX < depthWidth) && (depthY >= 0) && (depthY < depthHeight))
                                    {
                                        int depthIndex = (depthY * depthWidth) + depthX;

                                        // If we are tracking a body for the current pixel, do not zero out the pixel
                                        if (bodyIndexDataPointer[depthIndex] != 0xff)
                                        {
                                            continue;
                                        }
                                    }
                                }

                                //bitmapPixelsPointer[colorIndex] = 0;
                            }
                        }

                        //this.bitmap.AddDirtyRect(new Int32Rect(0, 0, this.bitmap.PixelWidth, this.bitmap.PixelHeight));
                    }
                }
            }
            finally
            {
                if (isBitmapLocked)
                {
                    //this.bitmap.Unlock();
                }

                if (depthFrame != null)
                {
                    depthFrame.Dispose();
                }

                if (colorFrame != null)
                {
                    colorFrame.Dispose();
                }

                if (bodyIndexFrame != null)
                {
                    bodyIndexFrame.Dispose();
                }
            }
        }

        /// <summary>
        /// 处理从传感器到达的深度帧数据
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Reader_FrameArrived(object sender, DepthFrameArrivedEventArgs e)
        {
            
            bool depthFrameProcessed = false;

            using (DepthFrame depthFrame = e.FrameReference.AcquireFrame())
            {
                //FrameDescription depthFrameDescription = depthFrame.FrameDescription;
                if (depthFrame != null)
                {
                    // the fastest way to process the body index data is to directly access 
                    // the underlying buffer
                    using (Microsoft.Kinect.KinectBuffer depthBuffer = depthFrame.LockImageBuffer())
                    {
                        // verify data and write the color data to the display bitmap
                        if (((depthFrameDescription.Width * depthFrameDescription.Height) == (depthBuffer.Size / depthFrameDescription.BytesPerPixel)))
                        {
                            // Note: In order to see the full range of depth (including the less reliable far field depth)
                            // we are setting maxDepth to the extreme potential depth threshold
                            ushort maxDepth = ushort.MaxValue;

                            // If you wish to filter by reliable depth distance, uncomment the following line:
                            //// maxDepth = depthFrame.DepthMaxReliableDistance

                            this.ProcessDepthFrameData(depthBuffer.UnderlyingBuffer, depthBuffer.Size, depthFrame.DepthMinReliableDistance, maxDepth);

                            depthFrameProcessed = true;
                        }
                    }
                }
            }
        }



        private ushort[] frameDatas=new ushort[512*424];


        /// <summary>
        /// Directly accesses the underlying image buffer of the DepthFrame to 
        /// create a displayable bitmap.
        /// This function requires the /unsafe compiler option as we make use of direct
        /// access to the native memory pointed to by the depthFrameData pointer.
        /// </summary>
        /// <param name="depthFrameData">Pointer to the DepthFrame image data</param>
        /// <param name="depthFrameDataSize">Size of the DepthFrame image data</param>
        /// <param name="minDepth">The minimum reliable depth value for the frame</param>
        /// <param name="maxDepth">The maximum reliable depth value for the frame</param>
        private unsafe void ProcessDepthFrameData(IntPtr depthFrameData, uint depthFrameDataSize, ushort minDepth, ushort maxDepth)
        {
            // depth frame data is a 16 bit value
            ushort* frameData = (ushort*)depthFrameData;

            //追踪驾驶员索引
            this.myBodyIndex=this.getDriverIndex(frameData);
        }

        /// <summary>
        /// 绘制面部基本信息
        /// </summary>
        /// <param name="faceIndex"></param>
        /// <param name="faceResult"></param>
        /// <param name="drawingContext"></param>
        private void DrawFaceFrameResults(int faceIndex, FaceFrameResult faceResult)
        {
            //this.myClock.soundPlayer();
            this.NumeFaigute=this.myClock.Scheduler(faceResult);
            Console.WriteLine("疲劳值为"+this.NumeFaigute);
        }

        
        /// <summary>
        /// 跟踪人体
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Reader_BodyFrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            using (var bodyFrame = e.FrameReference.AcquireFrame())
            {
                if (bodyFrame != null)
                {
                    // 更新人体数据
                    bodyFrame.GetAndRefreshBodyData(this.bodies);

                    bool drawFaceResult = false;

                    if (this.myBodyIndex >= 0)
                    {
                        if (this.faceFrameResults[this.myBodyIndex] != null)
                        {

                            this.DrawFaceFrameResults(this.myBodyIndex, this.faceFrameResults[this.myBodyIndex]);
                            Console.WriteLine("当前跟踪的面部ID：" + this.myBodyIndex+"\n距离为："+this.myBodyDepth);
                        }
                        else
                        {
                            // 检查是否跟踪相应的body
                            if (this.bodies[this.myBodyIndex].IsTracked)
                            {
                                // 更新人脸源来跟踪这个body
                                this.faceFrameSources[this.myBodyIndex].TrackingId = this.bodies[this.myBodyIndex].TrackingId;
                            }
                        }
                    }

                    if (!drawFaceResult)
                    {
                        // if no faces were drawn then this indicates one of the following:
                        // a body was not tracked 
                        // a body was tracked but the corresponding face was not tracked
                        // a body and the corresponding face was tracked though the face box or the face points were not valid
                        // 如果没有人脸信息
                        Console.WriteLine("当前没有检测到Face");
                    }
                }
            }
            //this.changeFrame();
        }

        /// <summary>
        /// 人体索引帧临帧事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void Reader_BodyIndexFrameArrived(Object sender,BodyIndexFrameArrivedEventArgs e)
        {

            bool bodyIndexFrameProcessed = false;

            using (BodyIndexFrame bodyIndexFrame = e.FrameReference.AcquireFrame())
            {
                if (bodyIndexFrame != null)
                {
                    // the fastest way to process the body index data is to directly access 
                    // the underlying buffer
                    using (Microsoft.Kinect.KinectBuffer bodyIndexBuffer = bodyIndexFrame.LockImageBuffer())
                    {
                        // verify data and write the color data to the display bitmap
                        if (((this.bodyIndexFrameDescription.Width * this.bodyIndexFrameDescription.Height) == bodyIndexBuffer.Size))
                        {
                            //this.BodyIndexToDepth(bodyIndexBuffer.UnderlyingBuffer, bodyIndexBuffer.Size);
                            this.ProcessBodyIndexFrameData(bodyIndexBuffer.UnderlyingBuffer, bodyIndexBuffer.Size);
                            bodyIndexFrameProcessed = true;
                        }
                    }
                }
                
            }
        }

        /// <summary>
        /// 人体索引映射到深度图像矩形中
        /// </summary>
        /// <param name="bodyIndexFrameData">人体帧数据</param>
        /// <param name="bodyIndexFrameDataSize"></param>
        private unsafe void BodyIndexToDepth(IntPtr bodyIndexFrameData, uint bodyIndexFrameDataSize)
        {
            byte* frameData = (byte*)bodyIndexFrameData;

            for (long i = 0; i < (int)bodyIndexFrameDataSize; ++i)
            {
                //将每个人体的坐标分别存储到队列中
                if (frameData[i] < 5&&frameData[i]>=0)
                {
                    //this.DriverIndex[frameData[i]].
                }
            }
        }

        /// <summary>
        /// 通过深度图像和人体索引在深度图像中的坐标，采集到最近的人体的索引
        /// </summary>
        /// <param name="depthData"></param>
        /// <returns></returns>
        public unsafe int getDriverIndex(ushort* depthData)
        {
            int index = -1;
            double depth=0.0f;
            double[] depthDrivers=new double[6];
            if (this.DriverIndex!= null)
            {
                for (int a = 0; a < 6; ++a)
                {
                    if (this.DriverIndex[a] != null)
                    {
                        foreach (long i in this.DriverIndex[a].getData())
                        {
                            depthDrivers[a] += ((double)(depthData[i]) / (this.depthFrameDescription.Width * this.depthFrameDescription.Height));
                        }
                    }
                    
                }

                double[] functionDD={999999,999999,999999,999999,999999,99999 };
                int fddI = 0;
                for (int a = 0; a < 6; a++)
                {
                    if (depthDrivers[a] != 0)
                    {
                        functionDD[fddI] = depthDrivers[a];
                        fddI++;
                    }
                }

                depth = functionDD.Min();
                StreamWriter minW = new StreamWriter("Min.txt", true);
                minW.WriteLine(depth);
                minW.Close();
                for(int a = 0; a < 6; a++)
                {
                    if (depth == depthDrivers[a])
                        index = a;
                }
            }
            if (index >= 0)
            {
                ///把计算得到的驾驶员人体索引录入文件中
                StreamWriter sw = new StreamWriter("m_index.txt", true);
                sw.WriteLine(index);
                sw.Close();
            }
            this.myBodyIndex = index;
            this.myBodyDepth = depth;
            return index;
        }

        /// <summary>
        /// Directly accesses the underlying image buffer of the BodyIndexFrame to 
        /// create a displayable bitmap.
        /// This function requires the /unsafe compiler option as we make use of direct
        /// access to the native memory pointed to by the bodyIndexFrameData pointer.
        /// </summary>
        /// <param name="bodyIndexFrameData">Pointer to the BodyIndexFrame image data</param>
        /// <param name="bodyIndexFrameDataSize">Size of the BodyIndexFrame image data</param>
        private unsafe void ProcessBodyIndexFrameData(IntPtr bodyIndexFrameData, uint bodyIndexFrameDataSize)
        {
            byte* frameData = (byte*)bodyIndexFrameData;
            for (int i = 0; i < 6; i++)
            {
                this.DriverIndex[i] = new MyQueue(this.bodyIndexFrameDescription.Width * this.bodyIndexFrameDescription.Height);
            }

            // convert body index to a visual representation
            for (int i = 0; i < (int)bodyIndexFrameDataSize; ++i)
            {
                // the BodyColor array has been sized to match
                // BodyFrameSource.BodyCount
                if (frameData[i] < 5)
                {
                    // this pixel is part of a player,
                    // display the appropriate color
                    int temp = frameData[i];
                    this.bodyIndexPixels[i] = BodyColor[frameData[i]];
                    this.DriverIndex[frameData[i]].Push(i);
                }
                else
                {
                    // this pixel is not part of a player
                    // display black
                    this.bodyIndexPixels[i] = 0x00000000;
                }
            }
        }

        /// <summary>
        /// 确认面部特征点有效
        /// </summary>
        /// <param name="faceFrameSource"></param>
        /// <returns></returns>
        private int GetFaceSourceIndex(FaceFrameSource faceFrameSource)
        {
            int index = -1;

            for (int i = 0; i < this.bodyCount; i++)
            {
                if (this.faceFrameSources[i] == faceFrameSource)
                {
                    index = i;
                    break;
                }
            }

            return index;
        }

        /// <summary>
        /// 确认面部特征点有效
        /// </summary>
        /// <param name="faceResult"></param>
        /// <returns></returns>
        private bool ValidateFaceBoxAndPoints(FaceFrameResult faceResult)
        {
            bool isFaceValid = faceResult != null;

            if (isFaceValid)
            {
                var faceBox = faceResult.FaceBoundingBoxInColorSpace;
                if (faceBox != null)
                {
                    //检测屏幕空间内范围内是否有有效矩形
                    isFaceValid = (faceBox.Right - faceBox.Left) > 0 &&
                                  (faceBox.Bottom - faceBox.Top) > 0 &&
                                  faceBox.Right <= this.displayWidth &&
                                  faceBox.Bottom <= this.displayHeight;

                    if (isFaceValid)
                    {
                        var facePoints = faceResult.FacePointsInColorSpace;
                        if (facePoints != null)
                        {
                            foreach (PointF pointF in facePoints.Values)
                            {
                                //检查特征点是否有效
                                bool isFacePointValid = pointF.X > 0.0f &&
                                                        pointF.Y > 0.0f &&
                                                        pointF.X < this.displayWidth &&
                                                        pointF.Y < this.displayHeight;

                                if (!isFacePointValid)
                                {
                                    isFaceValid = false;
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            return isFaceValid;
        }

        /// <summary>
        /// 面部数据的采集
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Reader_FaceFrameArrived(object sender, FaceFrameArrivedEventArgs e)
        {
            using (FaceFrame faceFrame = e.FrameReference.AcquireFrame())
            {
                if (faceFrame != null)
                {
                    //获取面部索引
                    int index = this.GetFaceSourceIndex(faceFrame.FaceFrameSource);

                    //检查改面部数据是否有效
                    if (this.ValidateFaceBoxAndPoints(faceFrame.FaceFrameResult))
                    {
                        
                        //储存数据以待绘制和疲劳检测
                        this.faceFrameResults[index] = faceFrame.FaceFrameResult;
                    }
                    else
                    {
                        //
                        this.faceFrameResults[index] = null;
                    }
                }
            }
        }
    }
}