using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace HeatMapCSHarp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private List<HeatPoint> HeatPoints = new List<HeatPoint>();
        // Create new memory bitmap the same size as the picture box
        Bitmap bMap;

        public MainWindow()
        {
            InitializeComponent();

            bMap = new Bitmap((int)this.plotBox.Width, (int)this.plotBox.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        }

        private void GenerateButton_Click(object sender, RoutedEventArgs e)
        {
            // Initialize random number generator
            Random rRand = new Random();
            // Loop variables
            int iX;
            int iY;

            int iXStart;
            int iYStart;
            int iXDirection;
            int iYDirection;

            byte iIntense;

            switch (((System.Windows.Controls.ComboBoxItem)this.TypeComboBox.SelectedValue).Content)
            {
                case "Random":
                    // Lets loop 500 times and create a random point each iteration
                    for (int i = 0; i < 500; i++)
                    {
                        // Pick random locations and intensity
                        iX = rRand.Next(0, 800);
                        iY = rRand.Next(0, 800);
                        iIntense = (byte)rRand.Next(0, 120);
                        // Add heat point to heat points list
                        HeatPoints.Add(new HeatPoint(iX, iY, iIntense));
                    }
                    break;
                case "Linear":
                    iXStart = rRand.Next(0, 600);
                    iYStart = rRand.Next(0, 600);
                    iXDirection = rRand.Next(2) == 1 ? 1 : -1;
                    iYDirection = rRand.Next(2) == 1 ? 1 : -1;

                    for (int i = 0; i < 200; i += 5)
                    {
                        iIntense = 25;
                        // Add heat point to heat points list
                        HeatPoints.Add(new HeatPoint(iXStart + (i * iXDirection), iYStart + (i * iYDirection), iIntense));
                    }
                    break;
            }

            UpdatePlot();
        }

        private void UpdatePlot()
        {
            // Call CreateIntensityMask, give it the memory bitmap, and store the result back in the memory bitmap
            bMap = CreateIntensityMask(bMap, HeatPoints);

            switch (((System.Windows.Controls.ComboBoxItem)this.StyleComboBox.SelectedValue).Content)
            {
                case "Colourised":
                    this.plotBox.Source = ImageSourceForBitmap(Colorize(bMap, 255));
                    break;
                case "DensityMap":
                    this.plotBox.Source = ImageSourceForBitmap(CreateIntensityMask(bMap, HeatPoints));
                    break;
            }
        }

        //If you get 'dllimport unknown'-, then add 'using System.Runtime.InteropServices;'
        [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject([In] IntPtr hObject);

        public ImageSource ImageSourceForBitmap(Bitmap bmp)
        {
            var handle = bmp.GetHbitmap();
            try
            {
                return Imaging.CreateBitmapSourceFromHBitmap(handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
            finally { DeleteObject(handle); }
        }


        private Bitmap CreateIntensityMask(Bitmap bSurface, List<HeatPoint> aHeatPoints)
        {
            // Create new graphics surface from memory bitmap
            Graphics DrawSurface = Graphics.FromImage(bSurface);
            // Set background color to white so that pixels can be correctly colorized
            DrawSurface.Clear(System.Drawing.Color.White);
            // Traverse heat point data and draw masks for each heat point
            foreach (HeatPoint DataPoint in aHeatPoints)
            {
                // Render current heat point on draw surface
                DrawHeatPoint(DrawSurface, DataPoint, 15);
            }
            return bSurface;
        }
        private void DrawHeatPoint(Graphics Canvas, HeatPoint HeatPoint, int Radius)
        {
            // Create points generic list of points to hold circumference points
            List<System.Drawing.Point> CircumferencePointsList = new List<System.Drawing.Point>();
            // Create an empty point to predefine the point struct used in the circumference loop
            System.Drawing.Point CircumferencePoint;
            // Create an empty array that will be populated with points from the generic list
            System.Drawing.Point[] CircumferencePointsArray;
            // Calculate ratio to scale byte intensity range from 0-255 to 0-1
            float fRatio = 1F / Byte.MaxValue;
            // Precalulate half of byte max value
            byte bHalf = Byte.MaxValue / 2;
            // Flip intensity on it's center value from low-high to high-low
            int iIntensity = (byte)(HeatPoint.Intensity - ((HeatPoint.Intensity - bHalf) * 2));
            // Store scaled and flipped intensity value for use with gradient center location
            float fIntensity = iIntensity * fRatio;
            // Loop through all angles of a circle
            // Define loop variable as a double to prevent casting in each iteration
            // Iterate through loop on 10 degree deltas, this can change to improve performance
            for (double i = 0; i <= 360; i += 10)
            {
                // Replace last iteration point with new empty point struct
                CircumferencePoint = new System.Drawing.Point();
                // Plot new point on the circumference of a circle of the defined radius
                // Using the point coordinates, radius, and angle
                // Calculate the position of this iterations point on the circle
                CircumferencePoint.X = Convert.ToInt32(HeatPoint.X + Radius * Math.Cos(ConvertDegreesToRadians(i)));
                CircumferencePoint.Y = Convert.ToInt32(HeatPoint.Y + Radius * Math.Sin(ConvertDegreesToRadians(i)));
                // Add newly plotted circumference point to generic point list
                CircumferencePointsList.Add(CircumferencePoint);
            }
            // Populate empty points system array from generic points array list
            // Do this to satisfy the datatype of the PathGradientBrush and FillPolygon methods
            CircumferencePointsArray = CircumferencePointsList.ToArray();
            // Create new PathGradientBrush to create a radial gradient using the circumference points
            PathGradientBrush GradientShaper = new PathGradientBrush(CircumferencePointsArray);
            // Create new color blend to tell the PathGradientBrush what colors to use and where to put them
            ColorBlend GradientSpecifications = new ColorBlend(3);
            // Define positions of gradient colors, use intesity to adjust the middle color to
            // show more mask or less mask
            GradientSpecifications.Positions = new float[3] { 0, fIntensity, 1 };
            // Define gradient colors and their alpha values, adjust alpha of gradient colors to match intensity
            GradientSpecifications.Colors = new System.Drawing.Color[3]
            {
            System.Drawing.Color.FromArgb(0, System.Drawing.Color.White),
            System.Drawing.Color.FromArgb(HeatPoint.Intensity, System.Drawing.Color.Black),
            System.Drawing.Color.FromArgb(HeatPoint.Intensity, System.Drawing.Color.Black)
            };
            // Pass off color blend to PathGradientBrush to instruct it how to generate the gradient
            GradientShaper.InterpolationColors = GradientSpecifications;
            // Draw polygon (circle) using our point array and gradient brush
            Canvas.FillPolygon(GradientShaper, CircumferencePointsArray);
        }
        private double ConvertDegreesToRadians(double degrees)
        {
            double radians = (Math.PI / 180) * degrees;
            return (radians);
        }

        public static Bitmap Colorize(Bitmap Mask, byte Alpha)
        {
            // Create new bitmap to act as a work surface for the colorization process
            Bitmap Output = new Bitmap(Mask.Width, Mask.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            // Create a graphics object from our memory bitmap so we can draw on it and clear it's drawing surface
            Graphics Surface = Graphics.FromImage(Output);
            Surface.Clear(System.Drawing.Color.Transparent);
            // Build an array of color mappings to remap our greyscale mask to full color
            // Accept an alpha byte to specify the transparancy of the output image
            ColorMap[] Colors = CreatePaletteIndex(Alpha);
            // Create new image attributes class to handle the color remappings
            // Inject our color map array to instruct the image attributes class how to do the colorization
            ImageAttributes Remapper = new ImageAttributes();
            Remapper.SetRemapTable(Colors);
            // Draw our mask onto our memory bitmap work surface using the new color mapping scheme
            Surface.DrawImage(Mask, new System.Drawing.Rectangle(0, 0, Mask.Width, Mask.Height), 0, 0, Mask.Width, Mask.Height, GraphicsUnit.Pixel, Remapper);
            // Send back newly colorized memory bitmap
            return Output;
        }
        private static ColorMap[] CreatePaletteIndex(byte Alpha)
        {
            ColorMap[] OutputMap = new ColorMap[256];
            // Change this path to wherever you saved the palette image.

            Assembly myAssembly = Assembly.GetExecutingAssembly();
            Stream myStream = myAssembly.GetManifestResourceStream("HeatMapCSHarp.Images.gradient-palette.jpg");

            Bitmap Palette = new Bitmap(myStream);


            // Loop through each pixel and create a new color mapping
            for (int X = 0; X <= 255; X++)
            {
                OutputMap[X] = new ColorMap();
                OutputMap[X].OldColor = System.Drawing.Color.FromArgb(X, X, X);
                OutputMap[X].NewColor = System.Drawing.Color.FromArgb(Alpha, Palette.GetPixel(X, 0));
            }
            return OutputMap;
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            HeatPoints.Clear();
            UpdatePlot();
        }
    }
}
