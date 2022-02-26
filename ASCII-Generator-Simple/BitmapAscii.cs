using System.Collections.Generic;
using System.Drawing;
using System.ComponentModel;
using System;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows.Media.Imaging;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ASCII_Generator_Simple
{
    // 1. determine an image kernel width and height
    // 2. load an image
    // 3. for each kernel:
    //      a. convert rgb values to a normalized greyscale value (0 - 1)
    //      b. convert normalized values to ascii char


    // TODO
    // 1. add gif / video support
    // 2. add color support
    // 3. multithread??
    // 4. add sizing options to settings
    // 5. fix normalizing??


    class BitmapAscii : INotifyPropertyChanged
    {
        // FIELDS
        string _ascii = "";
        int _kernelWidth = 4;
        int _kernelHeight = 4;
        double _txtBoxWidth = 0;
        double _txtBoxHeight = 0;
        int _colorRange = 1;
        double _fontSize = 6;
        string _fontName = "Cascadia Mono";
        int[] _comboSelections = { 1, 2, 3, 4, 5 };
        int _selection = 0;
        BackgroundWorker? _worker;
        Bitmap? _bmp;
        BitmapImage? _asciiImage;
        string _gifPath = "";
        bool _isGif = false;

        #region PROPERTIES
        public string Ascii { get { return _ascii; } set { _ascii = value; OnPropertyChanged(nameof(Ascii)); } }
        public int KernelWidth { get { return _kernelWidth; } set { _kernelWidth = value; } }
        public int KernelHeight { get { return _kernelHeight; } set { _kernelHeight = value; } }
        public double TxtBoxWidth { get { return _txtBoxWidth; } set { _txtBoxWidth = value; } }
        public double TxtBoxHeight { get { return _txtBoxHeight; } set { _txtBoxHeight = value; } }
        public int ColorRange { get { return _colorRange; } set { _colorRange = value; } }
        public double FontSize { get { return _fontSize; } set { _fontSize = value; OnPropertyChanged(nameof(Ascii)); } }
        public string FontName { get { return _fontName; } set { _fontName = value; } }
        public int[] ComboSelections { get { return _comboSelections; } set { _comboSelections = value; } }
        public int Selection { get { return _selection; } set { _selection = value; } }
        public BackgroundWorker? Worker { get { return _worker; } set { _worker = value; } }
        public Bitmap? Bmp { get { return _bmp; } set { _bmp = value; } }
        public BitmapImage? AsciiImage { get { return _asciiImage; } set { _asciiImage = value; OnPropertyChanged(nameof(AsciiImage)); } }
        public string GifPath { get { return _gifPath; } set { _gifPath = value; } }
        public bool IsGif { get { return _isGif;} set { _isGif = value; } }

        #endregion

        #region METHODS

        public string Asciitize(Bitmap bmp)
        {// take a bitmap and return the ascii version

            // 1. Resize bitmap, make it smaller so it the ascii art fits on the screen
            Bmp = Resize(bmp, (int)TxtBoxWidth);
            if (Worker != null) Worker.ReportProgress(25);

            // 2. Get a list of all the pixels in the bitmap
            List<Color> pixels = GetPixels(Bmp);
            if (Worker != null) Worker.ReportProgress(50);

            // 3. Get the normalized values of the pixels
            double[] normalized = AverageColor(pixels);
            if (Worker != null) Worker.ReportProgress(75);

            // 4. Convert the normalized values into ascii characters
            string ascii = GrayToString(normalized);
            if (Worker != null) Worker.ReportProgress(100);

            MakeImage(ascii);

            if (AsciiImage != null) {
                Bmp = BitmapImage2Bitmap(AsciiImage);
            }
            
            return Ascii;
        }

        public void AsciitizeGIF(Image gif) {
            // called only if image is a gif

            // get each frame in the gif
            int numberOfFrames = gif.GetFrameCount(FrameDimension.Time);
            Image[] frames = new Image[numberOfFrames];

            for (int i = 0; i < numberOfFrames; i++) {
                gif.SelectActiveFrame(FrameDimension.Time, i);
                frames[i] = ((Image)gif.Clone());
            }

            // now convert each frame into the ascii version
            List<Bitmap> bitmaps = new List<Bitmap>();

            for (int i = 0; i < frames.Length; i++) {

                // 1. Resize bitmap, make it smaller so it the ascii art fits on the screen
                Bmp = Resize(frames[i], (int)TxtBoxWidth);

                // 2. Get a list of all the pixels in the bitmap
                List<Color> pixels = GetPixels(Bmp);

                // 3. Get the normalized values of the pixels
                double[] normalized = AverageColor(pixels);

                // 4. Convert the normalized values into ascii characters
                string ascii = GrayToString(normalized);

                bitmaps.Add(ReturnImage(ascii));

                // update progress bar
                if (Worker != null) Worker.ReportProgress((int)((decimal)i / frames.Length * 100));
            }

            // gifSupport class converts the list of bitmaps into a gif
            gifSupport gs = new gifSupport();
            GifPath = gs.Start(bitmaps);

            // work complete fill progress bar
            if (Worker != null) Worker.ReportProgress(100);
        }

        private double[] AverageColor(List<Color> colors)
        {// returns the normalized values for a list of colors

            // catch exceptions
            if (Bmp == null)
            {
                throw new Exception("Bitmap is null");
            }

            // return values
            double[] greyPixels = new double[colors.Count];

            // convert all the pixels to grey
            for (int i = 0; i < greyPixels.Length; i++)
            {
                greyPixels[i] = ConvertToGrey(colors[i]);
            }

            // divide array up into the kernels, get average of kernel
            List<double> averagedPixels = new List<double>();
            double min = 0;
            double max = 0;

            // this fixes bug with uneven widths causing the image to come out strange
            int width = Bmp.Width / KernelWidth;
            int height = Bmp.Height / KernelHeight;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    double average = 0.0;
                    int numberOfPixels = 0;

                    for (int i = 0; i < KernelHeight; i++)
                    {
                        for (int j = 0; j < KernelWidth; j++)
                        {
                            // calculate the index for the 1d array
                            int currentIndex = ((y*KernelHeight) + i) * Bmp.Width + ((x*KernelWidth) + j);

                            if (currentIndex < greyPixels.Length)
                            {
                                // add pixel to average, increment numberOfPixels
                                average += greyPixels[currentIndex];
                                numberOfPixels++;

                                // Check for new min and max
                                max = greyPixels[currentIndex] > max ? greyPixels[currentIndex] : max;
                                min = greyPixels[currentIndex] < min ? greyPixels[currentIndex] : min;
                            }//end if
                        }//end width loop
                    }//end height loop
                    // calc the average
                    average /= numberOfPixels;
                    averagedPixels.Add(average);
                }//end x axis loop
            }//end y axis loop

            // Once out of the loops, normalize the averaged pixels
            double[] normalized = Normalize(averagedPixels, min, max);

            return normalized;
        }

        private string GrayToString(double[] normalized)
        {// takes an array of normalized values, converts them to an ascii art string, then calls MakeImage()
            
            StringBuilder result = new StringBuilder();
            int asciiWidth = (int)(TxtBoxWidth / KernelWidth);
            bool toggle = true;
            
            for (int i = 0; i < normalized.Length; i++)
            {
                // toggle is used to skip every other line
                if (toggle)
                {
                    result.Append(GetAsciiChar(normalized[i]));
                    
                }//end if

                // if not on first run, and i is at the end of the row
                if (i != 0 && i % asciiWidth == 0)
                {
                    if (toggle)
                    {
                        result.Append('\n');
                        toggle = false;
                    }
                    else
                    {
                        toggle = true;
                    }//end if
                }//end if
            }//end for

            return result.ToString();
        }

        override public string ToString()
        {// returns the ascii art string
            return Ascii;
        }

        private Bitmap Resize(Bitmap bmpIn, int newWidth)
        {// scales the bitmap to the given width

            // calculate height, bmp.H * new.W / bmp.W == new.H
            int newHeight = (int)Math.Ceiling((double)bmpIn.Height * newWidth / bmpIn.Width);

            // create bitmap with the new sizes
            Bitmap newMap = new Bitmap(newWidth, newHeight);

            // graphic object draws on the given image
            Graphics g = Graphics.FromImage(newMap);

            // INTERPOLATION: In the mathematical field of numerical analysis, interpolation is a 
            // type of estimation, a method of constructing new data points based on
            //the range of a discrete set of known data points.

            //The interpolation mode produces high quality images
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

            // draws original bmp {bmpIn} on the {newMap} at (0,0), with the given width and height
            g.DrawImage(bmpIn, 0, 0, newWidth, newHeight);

            // free memory
            g.Dispose();

            return newMap;
        }

        private Bitmap Resize(Image bmpIn, int newWidth) {// scales the bitmap to the given width

            // calculate height, bmp.H * new.W / bmp.W == new.H
            int newHeight = (int)Math.Ceiling((double)bmpIn.Height * newWidth / bmpIn.Width);

            // create bitmap with the new sizes
            Bitmap newMap = new Bitmap(newWidth, newHeight);

            // graphic object draws on the given image
            Graphics g = Graphics.FromImage(newMap);

            // INTERPOLATION: In the mathematical field of numerical analysis, interpolation is a 
            // type of estimation, a method of constructing new data points based on
            //the range of a discrete set of known data points.

            //The interpolation mode produces high quality images
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

            // draws original bmp {bmpIn} on the {newMap} at (0,0), with the given width and height
            g.DrawImage(bmpIn, 0, 0, newWidth, newHeight);

            // free memory
            g.Dispose();

            return newMap;
        }

        private List<Color> GetPixels(Bitmap bmp)
        {// returns a list of pixels from the bitmap

            List<Color> pixels = new List<Color>();

            // loop thru bitmap
            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    // get pixel at specified location
                    Color pixel = (bmp.GetPixel(x, y));
                    pixels.Add(pixel);
                }//end for
            }//end for
            return pixels;
        }

        private double ConvertToGrey(Color color)
        {// converts pixel to grey
            double grey = (color.R * 0.299) + (color.G * 0.587) + (color.B * 0.114);
            return grey;
        }

        private double[] Normalize(List<double> pixels, double min, double max)
        {// Normalizes a list of grey pixels-

            double[] result = new double[pixels.Count];
            double range = max - min;

            for (int i = 0; i < pixels.Count; i++)
            {
                result[i] = (pixels[i] - min) / range;
            }
            return result;
        }

        private char GetAsciiChar(double normalized)
        {// Converts a normalized value into a character

            string shortRange = "@%#*+=-:. ";
            string longRange = "$@B%8&WM#*oahkbdpqwmZO0QLCJUYXzcvunxrjft/\\|()1{}[]?-_+~<>i!lI;:,\u0022 ^`'. ";
            string customRange1 = "@X#%x+*:,.'";
            string customRange2 = "@#$%?*+;:,.";
            string customRange3 = "qwertyuiopasdfghjklzxcvbnm,./;'[]1234567890-=";

            // set selected to a range
            string selected;
            switch (Selection)
            {
                case 0: selected = shortRange; break;

                case 1: selected = longRange; break;

                case 2: selected = customRange1; break;

                case 3: selected = customRange2; break;

                case 4: selected = customRange3; break;

                default: selected = shortRange; break;
            }
            // number of characters in the selected string
            int numberOfChars = selected.Count() - 1;

            // get a index to a char in the string
            int index = (int)(numberOfChars * normalized);

            return selected.ElementAt(index);
        }

        private void MakeImage(string str)
        {// This creates an image from the ascii art string

            UpdateFontSize();

            // create a dummy Bitmap just to get the Graphics object
            Bitmap img = new Bitmap(1, 1);
            Graphics g = Graphics.FromImage(img);

            // set smoothing and interpolation to make the image look better
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.Bicubic;

            // The font for our text
            Font f = new Font(FontName, (int)FontSize);

            // work out how big the text will be when drawn as an image
            SizeF size = g.MeasureString(str, f);

            // create a new Bitmap of the required size
            img = new Bitmap((int)Math.Ceiling(size.Width), (int)Math.Ceiling(size.Height));
            g = Graphics.FromImage(img);

            // give it a white background
            g.Clear(Color.White);

            // draw the text in black
            g.DrawString(str, f, Brushes.Black, 0, 0);

            // Convert to image source and freeze the memory that is holding the image
            BitmapImage bmi = BitmapToImageSource(img);
            bmi.Freeze();

            // Image box is updated with image when AsciiImage is updated
            AsciiImage = bmi;
        }

        private Bitmap ReturnImage(string str) {

            UpdateFontSize();

            // create a dummy Bitmap just to get the Graphics object
            Bitmap img = new Bitmap(1, 1);
            Graphics g = Graphics.FromImage(img);

            // set smoothing and interpolation to make the image look better
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.Bicubic;

            // The font for our text
            Font f = new Font(FontName, (int)FontSize);

            // work out how big the text will be when drawn as an image
            SizeF size = g.MeasureString(str, f);

            // create a new Bitmap of the required size
            img = new Bitmap((int)Math.Ceiling(size.Width), (int)Math.Ceiling(size.Height));
            g = Graphics.FromImage(img);

            // give it a white background
            g.Clear(Color.White);

            // draw the text in black
            g.DrawString(str, f, Brushes.Black, 0, 0);

            return img;
        }
        private void UpdateFontSize()
        {// updates the font size of the ascii art based on the kernel size
            if (KernelWidth != 4)
            {
                FontSize = KernelWidth * 1.5;
            }
            else
            {
                FontSize = 6;
            }
        }

        BitmapImage BitmapToImageSource(Bitmap bitmap)
        {// this converts a bitmap to a image source so the <Image> box can display it
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
                memory.Position = 0;
                BitmapImage bitmapimage = new BitmapImage();
                bitmapimage.BeginInit();
                bitmapimage.StreamSource = memory;
                bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapimage.EndInit();

                return bitmapimage;
            }
        }

        // this event is called when a property is changed
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? caller = null)
        {// on property change tell gui to update with the new value
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(caller));
        }

        private Bitmap BitmapImage2Bitmap(BitmapImage bitmapImage) {
            // BitmapImage bitmapImage = new BitmapImage(new Uri("../Images/test.png", UriKind.Relative));

            using (MemoryStream outStream = new MemoryStream()) {
                BitmapEncoder enc = new BmpBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bitmapImage));
                enc.Save(outStream);
                System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(outStream);

                return new Bitmap(bitmap);
            }
        }

        #endregion
    }
}
